using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using Gondwana.Scenes.GSCN;
using static Gondwana.Tooling.Importers.GodotTilesetImporter;

namespace Gondwana.Tooling.Importers;

/// <summary>Imports Godot 4 text tile scenes and their direct atlas dependencies.</summary>
public sealed class GodotSceneImporter : ExternalAssetImporter
{
    /// <inheritdoc/>
    public override string Id => "godot.scene";
    /// <inheritdoc/>
    public override string DisplayName => "Godot 4 Tile Scene (.tscn)";
    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedExtensions => [".tscn"];

    private sealed record TileSet(Point CellSize, Dictionary<int, ConvertedAtlas> Atlases);
    private sealed record NodeState(Point Position, bool Visible, int Z, bool Supported);
    private static readonly string[] UnsupportedTileDataFields =
        ["texture_origin", "flip_h", "flip_v", "transpose", "z_index", "y_sort_origin", "material", "modulate"];

    /// <inheritdoc/>
    protected override void BuildPlan(ImportPlan plan, CancellationToken token)
    {
        string source = Path.GetFullPath(plan.Request.SourcePath);
        var sections = GodotTextResource.Parse(File.ReadAllText(source));
        if (sections.Count == 0 || sections[0].Kind != "gd_scene" || Get(sections[0].Attributes, "format", "") is not ("3" or "4"))
            throw new InvalidDataException("Expected a Godot 4 text scene (gd_scene format=3 or format=4).");
        ValidateResources(sections);
        string name = ImportNaming.Sanitize(Path.GetFileNameWithoutExtension(source));
        string gts = name + ".gts";
        var scene = new SceneDefinition { ID = name };
        var sets = new Dictionary<string, TileSet>(StringComparer.Ordinal);

        TileSet ResolveSet(string reference)
        {
            string path = source;
            var resources = sections;
            GodotResourceSection root;
            string key;
            if (reference.StartsWith("ExtResource(", StringComparison.Ordinal))
            {
                var external = FindResource(sections, "ext_resource", Reference(reference, "ExtResource"), "TileSet");
                path = ResolvePath(source, GodotTextResource.String(Get(external.Attributes, "path")));
                plan.Dependencies.Add(path);
                key = "external:" + path;
                if (sets.TryGetValue(key, out var cached)) return cached;
                if (!string.Equals(Path.GetExtension(path), ".tres", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"TileSet {path}: only text .tres resources are supported.");
                resources = GodotTextResource.Parse(File.ReadAllText(path));
                if (resources.Count == 0 || resources[0].Kind != "gd_resource" ||
                    Get(resources[0].Attributes, "format", "") is not ("3" or "4") ||
                    GodotTextResource.String(Get(resources[0].Attributes, "type")) != "TileSet")
                    throw new InvalidDataException($"Expected a Godot 4 TileSet in {path}.");
                ValidateResources(resources);
                var roots = resources.Where(s => s.Kind == "resource").ToArray();
                if (roots.Length != 1) throw new InvalidDataException($"Expected one TileSet resource in {path}.");
                root = roots[0];
            }
            else
            {
                string id = Reference(reference, "SubResource");
                key = "embedded:" + id;
                if (sets.TryGetValue(key, out var cached)) return cached;
                root = FindResource(sections, "sub_resource", id, "TileSet");
            }
            var cell = Vector(Get(root.Properties, "tile_size", "Vector2i(16, 16)"));
            if (cell.X <= 0 || cell.Y <= 0) throw new InvalidDataException("TileSet cell dimensions must be positive.");
            foreach (var property in root.Properties)
                if ((property.Key is "tile_shape" or "tile_layout" or "tile_offset_axis" && property.Value != "0") ||
                    property.Key.Contains("proxies", StringComparison.Ordinal))
                    throw new InvalidDataException($"TileSet property '{property.Key}' cannot be mapped safely; only orthogonal tiles without proxies are supported.");
            // A scene-scoped ordinal prevents collisions between equal basenames and sanitized resource IDs.
            var atlases = BuildGodot4Plan(plan, resources, root, token, path, $"{name}.tileset.{sets.Count}", gts);
            var set = new TileSet(cell, atlases);
            sets.Add(key, set);
            foreach (var atlas in atlases.Values)
            {
                scene.TilesheetSources.Add(SceneTilesheetSourceDefinition.Loose(atlas.Name, gts));
                foreach (var tile in atlas.Tiles.Values.Where(t => t.AnimationKey is not null))
                    scene.AnimationSources.Add(SceneAnimationSourceDefinition.Loose(tile.AnimationKey!, tile.AnimationFile!));
            }
            return set;
        }

        var nodes = new Dictionary<string, NodeState>(StringComparer.Ordinal);
        int nodeCount = 0;
        foreach (var node in sections.Where(s => s.Kind == "node"))
        {
            token.ThrowIfCancellationRequested();
            string nodeName = GodotTextResource.String(Get(node.Attributes, "name"));
            string type = GodotTextResource.String(Get(node.Attributes, "type", "\"instanced scene\""));
            bool isTile = type is "TileMap" or "TileMapLayer";
            bool isRoot = !node.Attributes.ContainsKey("parent");
            string parentPath = isRoot ? "" : GodotTextResource.String(node.Attributes["parent"]);
            string nodePath = isRoot ? "." : parentPath == "." ? nodeName : parentPath + "/" + nodeName;
            NodeState parent = new(Point.Empty, true, 0, true);
            if (isRoot ? nodeCount != 0 : !nodes.TryGetValue(parentPath, out parent!))
                throw new InvalidDataException($"Node '{nodeName}': unresolved parent or multiple roots; instanced/inherited hierarchies are unsupported.");
            nodeCount++;
            // A plain Node between CanvasItems breaks Godot transform/visibility inheritance.
            // Support it only as the scene root rather than flattening an arbitrary canvas hierarchy.
            bool supported = parent.Supported && (isTile || type == "Node2D" || (isRoot && type == "Node")) && !node.Attributes.ContainsKey("instance");
            if (!supported)
            {
                plan.Report(ExternalImportSeverity.Warning, "godot.node.unsupported", $"Node '{nodePath}' ({type}) is omitted; tile descendants cannot be imported.");
                nodes.Add(nodePath, new(parent.Position, parent.Visible, parent.Z, false));
                if (isTile) throw new InvalidDataException($"Tile node '{nodePath}' has an unsupported ancestor; use a Node or Node2D root followed by Node2D parents.");
                continue;
            }
            var p = node.Properties;
            if (isTile && (p.ContainsKey("tile_data") || (type == "TileMap" && p.ContainsKey("tile_map_data")) ||
                (type == "TileMapLayer" && p.Keys.Any(k => k.StartsWith("layer_", StringComparison.Ordinal)))))
                throw new InvalidDataException($"Tile node '{nodePath}': placement property does not match its node type; resave in Godot 4.");
            CheckProperties(plan, p, nodePath, isTile, type == "TileMap");
            var offset = PixelVector(Get(p, "position", "Vector2(0, 0)"));
            var position = new Point(checked(parent.Position.X + offset.X), checked(parent.Position.Y + offset.Y));
            bool visible = parent.Visible && Boolean(Get(p, "visible", "true"));
            int z = checked((Boolean(Get(p, "z_as_relative", "true")) ? parent.Z : 0) + Integer(Get(p, "z_index", "0")));
            CheckZOrder(z, nodePath);
            nodes.Add(nodePath, new(position, visible, z, true));
            if (!isTile) continue;
            var set = ResolveSet(Get(p, "tile_set"));
            if (type == "TileMapLayer")
                AddLayer(p, "tile_map_data", false, nodePath, visible, z);
            else
            {
                if (Get(p, "format", "") != "2")
                    throw new InvalidDataException($"TileMap '{nodePath}': only Godot 4 format=2 (FORMAT_3) cell data is supported; resave in Godot 4.");
                var indices = p.Keys.Select(k => Regex.Match(k, @"^layer_(\d+)/")).Where(m => m.Success)
                    .Select(m => Integer(m.Groups[1].Value)).Distinct().Order().ToArray();
                if (indices.Length == 0) indices = [0];
                if (indices[^1] > 4095) throw new InvalidDataException("Excessive TileMap layer count.");
                for (int index = 0; index <= indices[^1]; index++)
                {
                    string prefix = $"layer_{index}/";
                    var properties = p.Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                        .ToDictionary(kv => kv.Key[prefix.Length..], kv => kv.Value, StringComparer.Ordinal);
                    CheckProperties(plan, properties, $"{nodePath}/layer_{index}", true, false);
                    AddLayer(properties, "tile_data", true, $"{nodePath}/layer_{index}", visible,
                        checked(z + Integer(Get(properties, "z_index", "0"))));
                }
            }

            void AddLayer(IReadOnlyDictionary<string, string> properties, string dataKey, bool legacy, string label, bool shown, int order)
            {
                CheckZOrder(order, label);
                var cells = GodotSceneTileData.Decode(Get(properties, dataKey, legacy ? "PackedInt32Array()" : "PackedByteArray()"), legacy);
                int minX = cells.Count == 0 ? 0 : cells.Min(c => c.Position.X);
                int minY = cells.Count == 0 ? 0 : cells.Min(c => c.Position.Y);
                int width = cells.Count == 0 ? 1 : cells.Max(c => c.Position.X) - minX + 1;
                int height = cells.Count == 0 ? 1 : cells.Max(c => c.Position.Y) - minY + 1;
                if ((long)width * height > 1_000_000) throw new InvalidDataException($"Layer '{label}': occupied bounds exceed one million native grid cells; split the scene.");
                var layer = new SceneLayerDefinition
                {
                    // Diagnostic paths can overlap (Map's layer_0 and a child named layer_0).
                    // Native identity uses a source-order ordinal, independent of node names and z sorting.
                    ID = $"{name}.layer.{scene.Layers.Count}",
                    Columns = width,
                    Rows = height,
                    TileWidth = set.CellSize.X,
                    TileHeight = set.CellSize.Y,
                    OriginPx = new(checked(-position.X - minX * set.CellSize.X), checked(-position.Y - minY * set.CellSize.Y)),
                    Visible = shown && Boolean(Get(properties, "enabled", "true")),
                    ZOrder = order
                };
                foreach (var cell in cells)
                {
                    token.ThrowIfCancellationRequested();
                    string context = $"Layer '{label}', cell {cell.Position}, source {cell.Source}, atlas {cell.Atlas}";
                    if (!set.Atlases.TryGetValue(cell.Source, out var atlas) || !atlas.Tiles.TryGetValue(cell.Atlas, out var tile))
                        throw new InvalidDataException($"{context}: no declared base atlas tile; check the TileSet dependency.");
                    if (tile.Size != new Size(set.CellSize.X, set.CellSize.Y))
                        throw new InvalidDataException($"{context}: artwork dimensions differ from logical cells; anchoring/scaling cannot be preserved.");
                    var transform = CellTransform(cell.Alternative, context);
                    // Native axis-swapped artwork is centered using integer pixel bounds.
                    // Different width/height parity needs a half-pixel anchor that would be lost.
                    bool swapsAxes = transform is Gondwana.Drawing.TileTransform.Rotate90 or
                        Gondwana.Drawing.TileTransform.Rotate270 or Gondwana.Drawing.TileTransform.FlipDiagonal or
                        Gondwana.Drawing.TileTransform.FlipAntiDiagonal;
                    if (swapsAxes && (tile.Size.Width & 1) != (tile.Size.Height & 1))
                        throw new InvalidDataException($"{context}: transposed tile artwork {tile.Size.Width}x{tile.Size.Height} has mixed-parity dimensions and requires unsupported half-pixel anchoring.");
                    string prefix = $"{cell.Atlas.X}:{cell.Atlas.Y}/0/";
                    foreach (string field in UnsupportedTileDataFields)
                    {
                        if (atlas.Resource.Properties.ContainsKey(prefix + field))
                            throw new InvalidDataException($"{context}: TileData '{field}' is unsupported for scene placement; bake it into the artwork or remove it.");
                    }
                    layer.Tiles.Add(new()
                    {
                        X = cell.Position.X - minX,
                        Y = cell.Position.Y - minY,
                        Frame = tile.Frame,
                        Transform = transform,
                        AnimationKey = tile.AnimationKey,
                        StartAnimation = tile.AnimationKey is not null
                    });
                }
                scene.Layers.Add(layer);
            }
        }
        if (scene.Layers.Count == 0) throw new InvalidDataException("No supported TileMap or TileMapLayer nodes found.");
        // Stable source order breaks equal-z ties; assign unique native orders.
        scene.Layers = scene.Layers.OrderBy(l => l.ZOrder).ToList();
        for (int i = 0; i < scene.Layers.Count; i++) scene.Layers[i].ZOrder = i;
        foreach (var section in sections.Where(s => s.Kind is not ("gd_scene" or "node" or "ext_resource" or "sub_resource")))
            plan.Report(ExternalImportSeverity.Warning, "godot.section.unsupported", $"Scene section '{section.Kind}' is omitted.");
        plan.Add(name + ".gscn", scene);
    }

    private static void ValidateResources(IReadOnlyList<GodotResourceSection> sections)
    {
        if (sections.Where(s => s.Kind is "ext_resource" or "sub_resource")
            .GroupBy(s => (s.Kind, Id: ResourceId(Get(s.Attributes, "id")))).Any(g => g.Count() != 1))
            throw new InvalidDataException("Duplicate Godot resource IDs.");
    }

    private static GodotResourceSection FindResource(IReadOnlyList<GodotResourceSection> sections, string kind, string id, string type)
    {
        var resource = sections.SingleOrDefault(s => s.Kind == kind && ResourceId(Get(s.Attributes, "id")) == id)
            ?? throw new InvalidDataException($"Missing {kind} '{id}' ({type}).");
        if (GodotTextResource.String(Get(resource.Attributes, "type")) != type)
            throw new InvalidDataException($"Resource '{id}': expected {type}.");
        return resource;
    }

    private static void CheckProperties(ImportPlan plan, IReadOnlyDictionary<string, string> properties, string context, bool tile, bool legacy)
    {
        foreach (var (key, value) in properties)
        {
            if (legacy && Regex.IsMatch(key, @"^layer_\d+/")) continue;
            if (key is "position" or "visible" or "z_index" or "z_as_relative") continue;
            if (tile && key is "tile_set" or "tile_map_data" or "tile_data" or "format" or "enabled" or "name") continue;
            bool harmlessDefault = key switch
            {
                "rotation" or "skew" or "y_sort_origin" => value == "0" || value == "0.0",
                "scale" => PixelVector(value) == new Point(1, 1),
                "y_sort_enabled" or "top_level" or "show_behind_parent" or "x_draw_order_reversed" => value == "false",
                _ => false
            };
            if (harmlessDefault) continue;
            if (key is "rotation" or "skew" or "scale" or "transform" or "y_sort_enabled" or "y_sort_origin" or "top_level" or
                "show_behind_parent" or "x_draw_order_reversed" or "clip_children")
                throw new InvalidDataException($"Node/layer '{context}': '{key} = {value}' cannot preserve placement or ordering; remove or bake this property.");
            plan.Report(ExternalImportSeverity.Warning, "godot.scene.property", $"Node/layer '{context}': property '{key}' is omitted (scripts, physics, navigation and appearance overrides are not converted).");
        }
    }

    private static int Integer(string value) => int.Parse(value, CultureInfo.InvariantCulture);
    private static void CheckZOrder(int value, string context)
    {
        if (value is < -4096 or > 4096)
            throw new InvalidDataException($"Node/layer '{context}': effective z-index exceeds Godot's canvas range; reduce relative z-index values before import.");
    }
    private static Gondwana.Drawing.TileTransform CellTransform(int alternative, string context)
    {
        try { return GodotSceneTileData.Transform(alternative); }
        catch (InvalidDataException ex) { throw new InvalidDataException($"{context}: {ex.Message}", ex); }
    }
    private static bool Boolean(string value) => value switch
    {
        "true" => true,
        "false" => false,
        _ => throw new InvalidDataException($"Expected boolean, found '{value}'.")
    };
    private static Point PixelVector(string value)
    {
        var match = Regex.Match(value, @"^Vector2\(\s*([^,]+),\s*([^,]+)\)$");
        if (!match.Success) throw new InvalidDataException($"Expected Vector2, found '{value}'.");
        int Pixel(string component)
        {
            double number = double.Parse(component, CultureInfo.InvariantCulture);
            if (!double.IsFinite(number) || number != Math.Truncate(number))
                throw new InvalidDataException($"Fractional/nonfinite translation or scale '{value}' cannot be represented.");
            return checked((int)number);
        }
        return new(Pixel(match.Groups[1].Value), Pixel(match.Groups[2].Value));
    }
}
