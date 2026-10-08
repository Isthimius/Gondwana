using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using SkiaSharp;

namespace Gondwana.Tooling.Importers;

/// <summary>
/// Represents godot tileset importer.
/// </summary>
public sealed class GodotTilesetImporter : ExternalAssetImporter
{
    /// <inheritdoc/>
    public override string Id => "godot.tileset";
    /// <inheritdoc/>
    public override string DisplayName => "Godot 3 / 4 TileSet (.tres)";
    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedExtensions => [".tres"];

    /// <inheritdoc/>
    protected override void BuildPlan(ImportPlan plan, CancellationToken token)
    {
        var sections = GodotTextResource.Parse(File.ReadAllText(plan.Request.SourcePath));
        if (sections.Count == 0 || sections[0].Kind != "gd_resource" ||
            !sections[0].Attributes.TryGetValue("type", out var type) || GodotTextResource.String(type) != "TileSet")
            throw new InvalidDataException("Expected a Godot text TileSet resource.");
        if (!sections[0].Attributes.TryGetValue("format", out var format) || format is not ("2" or "3"))
        {
            plan.Report(ExternalImportSeverity.Error, "godot.format",
                $"Unsupported Godot text resource format '{format ?? "missing"}'. Use a Godot 3.x format=2 or Godot 4.x format=3 text TileSet (.tres).");
            return;
        }
        if (sections.Count(s => s.Kind == "resource") != 1 ||
            sections.Where(s => s.Kind is "sub_resource" or "ext_resource")
                .GroupBy(s => (s.Kind, Id: ResourceId(Get(s.Attributes, "id")))).Any(g => g.Count() != 1))
            throw new InvalidDataException("Duplicate or missing Godot resource sections/IDs.");
        var root = sections.SingleOrDefault(s => s.Kind == "resource") ?? throw new InvalidDataException("Missing resource section.");
        if (format == "2") BuildGodot3Plan(plan, sections, root, token);
        else BuildGodot4Plan(plan, sections, root, token);
    }

    private static void BuildGodot4Plan(ImportPlan plan, IReadOnlyList<GodotResourceSection> sections, GodotResourceSection root, CancellationToken token)
    {
        var sources = root.Properties.Where(p => Regex.IsMatch(p.Key, @"^sources/\d+$")).OrderBy(p => int.Parse(p.Key[8..], CultureInfo.InvariantCulture)).ToArray();
        if (sources.Length == 0) throw new InvalidDataException("No atlas sources found.");
        string basename = ImportNaming.Sanitize(Path.GetFileNameWithoutExtension(plan.Request.SourcePath));
        foreach (var property in root.Properties.Keys.Where(k => k != "tile_size" && !k.StartsWith("sources/", StringComparison.Ordinal)))
            plan.Report(ExternalImportSeverity.Warning, "godot.metadata", $"TileSet property '{property}' is omitted (terrain, physics, navigation, proxies and custom data have no import mapping).");
        foreach (var source in sources)
        {
            token.ThrowIfCancellationRequested();
            string id = source.Key[8..];
            string resourceId = Reference(source.Value, "SubResource");
            var atlas = sections.SingleOrDefault(s => s.Kind == "sub_resource" && s.Attributes.TryGetValue("id", out var v) && ResourceId(v) == resourceId)
                ?? throw new InvalidDataException($"Unresolved atlas subresource {resourceId}.");
            if (!atlas.Attributes.TryGetValue("type", out var atlasType) || GodotTextResource.String(atlasType) != "TileSetAtlasSource")
                throw new InvalidDataException("Only TileSetAtlasSource is supported; scene collection sources are unsupported.");
            var p = atlas.Properties;
            string textureId = Reference(Get(p, "texture"), "ExtResource");
            var texture = sections.SingleOrDefault(s => s.Kind == "ext_resource" && s.Attributes.TryGetValue("id", out var v) && ResourceId(v) == textureId)
                ?? throw new InvalidDataException($"Unresolved texture {textureId}.");
            string path = ResolvePath(plan.Request.SourcePath, GodotTextResource.String(texture.Attributes["path"]));
            plan.Dependencies.Add(path);
            using var image = SKBitmap.Decode(path) ?? throw new InvalidDataException($"Cannot decode Godot atlas: {path}");
            var size = Vector(Get(p, "texture_region_size", "Vector2i(16, 16)"));
            var margin = Vector(Get(p, "margins", "Vector2i(0, 0)"));
            var gap = Vector(Get(p, "separation", "Vector2i(0, 0)"));
            if (size.X <= 0 || size.Y <= 0 || margin.X < 0 || margin.Y < 0 || gap.X < 0 || gap.Y < 0)
                throw new InvalidDataException("Invalid atlas dimensions/margins/separation.");
            string file = sources.Length == 1 ? basename : $"{basename}-source-{id}";
            string name = sources.Length == 1 ? basename : $"{basename}.source.{id}";
            var region = new TilesheetRegionDefinition
            {
                Area = new Rectangle(0, 0, image.Width, image.Height),
                TileSize = new Size(size.X, size.Y),
                TilePadding = new Spacing { Right = gap.X, Bottom = gap.Y },
                RegionMargin = new Spacing { Left = margin.X, Top = margin.Y, Right = -gap.X, Bottom = -gap.Y }
            };
            var grid = TilesheetDefinitionValidator.GridSize(region);
            if (Vector(Get(root.Properties, "tile_size", "Vector2i(16, 16)")) != size)
                plan.Report(ExternalImportSeverity.Info, "godot.cellsize", "Preserving atlas texture-region frame size; TileSet logical map cell size differs.");
            var definition = new TilesheetDefinition { Name = name, Image = new() { FilePath = ImportNaming.RelativePath(plan.Request.OutputDirectory, path) }, Regions = [region] };
            plan.Add(file + ".gts", definition);
            var coords = p.Keys.Where(k => Regex.IsMatch(k, @"^\d+:\d+/0$")).Select(k => k[..^2]).OrderBy(k => k, StringComparer.Ordinal).ToArray();
            if (coords.Length < grid.Columns * grid.Rows)
                plan.Report(ExternalImportSeverity.Info, "godot.sparse", "GTS exposes all atlas grid cells; generated animation references use only declared tile animation frames.");
            foreach (var property in p.Keys)
            {
                if (Regex.IsMatch(property, @"^\d+:\d+/[1-9]\d*(/|$)"))
                    plan.Report(ExternalImportSeverity.Warning, "godot.alternative", $"Alternative tile '{property}' is not imported.");
                else if (Regex.IsMatch(property, @"^\d+:\d+/0/"))
                    plan.Report(ExternalImportSeverity.Warning, "godot.tiledata", $"TileData '{property}' is omitted; polygons are not converted to collision rectangles.");
                else if (!Regex.IsMatch(property, @"^\d+:\d+/") && property is not ("texture" or "margins" or "separation" or "texture_region_size"))
                    plan.Report(ExternalImportSeverity.Info, "godot.metadata", $"Atlas metadata '{property}' is omitted.");
            }
            foreach (string coord in coords)
            {
                token.ThrowIfCancellationRequested();
                var xy = coord.Split(':').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                if (xy[0] >= grid.Columns || xy[1] >= grid.Rows) throw new InvalidDataException($"Tile {coord} is outside atlas.");
                if (Vector(Get(p, coord + "/size_in_atlas", "Vector2i(1, 1)")) != new Point(1, 1))
                { plan.Report(ExternalImportSeverity.Error, "godot.multicell", $"Tile {coord} spans multiple atlas cells and is unsupported."); continue; }
                var durationKeys = p.Keys.Select(k => Regex.Match(k, "^" + Regex.Escape(coord) + @"/animation_frame_(\d+)/duration$")).Where(m => m.Success).ToArray();
                int count = durationKeys.Length == 0 ? 1 : durationKeys.Max(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) + 1;
                count = checked((int)Number(Get(p, coord + "/animation_frames_count", count.ToString(CultureInfo.InvariantCulture))));
                if (count <= 0 || count > 100000) throw new InvalidDataException("Invalid animation frame count.");
                if (count == 1) continue;
                int columns = checked((int)Number(Get(p, coord + "/animation_columns", "0")));
                var separation = Vector(Get(p, coord + "/animation_separation", "Vector2i(0, 0)"));
                double speed = Number(Get(p, coord + "/animation_speed", "1"));
                if (columns < 0 || separation.X < 0 || separation.Y < 0 || speed <= 0) throw new InvalidDataException("Invalid Godot animation layout/speed.");
                if (Get(p, coord + "/animation_mode", "0") != "0") plan.Report(ExternalImportSeverity.Warning, "godot.randomstart", "Random animation start times are not represented.");
                var gani = new AnimationDefinition
                {
                    Key = $"{name}.tile.{xy[0]}.{xy[1]}",
                    CycleType = CycleType.Repeating,
                    TilesheetSources = [AnimationTilesheetSourceDefinition.Loose(name, file + ".gts")]
                };
                for (int frame = 0; frame < count; frame++)
                {
                    int x = checked(xy[0] + (1 + separation.X) * (columns > 0 ? frame % columns : frame));
                    int y = checked(xy[1] + (1 + separation.Y) * (columns > 0 ? frame / columns : 0));
                    if (x >= grid.Columns || y >= grid.Rows) throw new InvalidDataException($"Animation frame {frame} of {coord} cannot map to the GTS grid.");
                    gani.Frames.Add(new() { Tilesheet = name, XTile = x, YTile = y, DurationSeconds = Number(Get(p, $"{coord}/animation_frame_{frame}/duration", "1")) / speed });
                }
                plan.Add($"{file}-tile-{xy[0]}-{xy[1]}.gani", gani);
            }
        }
    }

    private static void BuildGodot3Plan(ImportPlan plan, IReadOnlyList<GodotResourceSection> sections, GodotResourceSection root, CancellationToken token)
    {
        var tiles = new SortedDictionary<int, Dictionary<string, string>>();
        foreach (var property in root.Properties)
        {
            var match = Regex.Match(property.Key, @"^(-?\d+)/(.+)$");
            if (!match.Success)
            {
                plan.Report(ExternalImportSeverity.Info, "godot.metadata", $"TileSet metadata '{property.Key}' is omitted.");
                continue;
            }
            int id = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (id < 0) throw new InvalidDataException($"Invalid Godot tile ID {id}.");
            if (!tiles.TryGetValue(id, out var properties)) tiles.Add(id, properties = new(StringComparer.Ordinal));
            properties.Add(match.Groups[2].Value, property.Value);
        }
        if (tiles.Count == 0) throw new InvalidDataException("No Godot 3 tiles found.");
        string basename = ImportNaming.Sanitize(Path.GetFileNameWithoutExtension(plan.Request.SourcePath));
        var imageDimensions = new Dictionary<string, Size>(StringComparer.Ordinal);
        foreach (var (id, p) in tiles)
        {
            token.ThrowIfCancellationRequested();
            int mode = Pixel(Get(p, "tile_mode", "0"));
            if (mode is not (0 or 1 or 2)) throw new InvalidDataException($"Unsupported Godot 3 tile mode {mode} for tile {id}; expected SINGLE_TILE (0), AUTO_TILE (1), or ATLAS_TILE (2).");
            string textureId = Reference(Get(p, "texture"), "ExtResource");
            var texture = sections.SingleOrDefault(s => s.Kind == "ext_resource" && ResourceId(Get(s.Attributes, "id")) == textureId)
                ?? throw new InvalidDataException($"Unresolved texture ExtResource {textureId} for tile {id}.");
            string path = ResolvePath(plan.Request.SourcePath, GodotTextResource.String(Get(texture.Attributes, "path")));
            plan.Dependencies.Add(path);
            if (!File.Exists(path)) throw new InvalidDataException($"Missing Godot texture: {path}");
            if (!imageDimensions.TryGetValue(path, out var imageSize))
            {
                using var image = SKBitmap.Decode(path) ?? throw new InvalidDataException($"Cannot decode Godot texture: {path}");
                imageSize = new Size(image.Width, image.Height);
                imageDimensions.Add(path, imageSize);
            }
            var rect = Geometry(Get(p, "region"), "Rect2", 4);
            var area = new Rectangle(rect[0], rect[1], rect[2], rect[3]);
            if (area.X < 0 || area.Y < 0 || area.Width <= 0 || area.Height <= 0 ||
                (long)area.X + area.Width > imageSize.Width || (long)area.Y + area.Height > imageSize.Height)
                throw new InvalidDataException($"Godot tile {id} region must have positive dimensions and lie within the source texture.");
            var size = mode == 0 ? new Point(area.Width, area.Height) : Godot3Vector(Get(p, "autotile/tile_size"));
            int spacing = mode == 0 ? 0 : Pixel(Get(p, "autotile/spacing", "0"));
            if (size.X <= 0 || size.Y <= 0 || spacing < 0)
                throw new InvalidDataException($"Godot tile {id} requires positive tile dimensions and nonnegative spacing.");
            if (area.Width < size.X || area.Height < size.Y ||
                ((long)area.Width + spacing) % ((long)size.X + spacing) != 0 ||
                ((long)area.Height + spacing) % ((long)size.Y + spacing) != 0)
                throw new InvalidDataException($"Godot tile {id} region cannot form an integral atlas grid with its tile size and spacing.");
            var region = new TilesheetRegionDefinition
            {
                Area = area,
                TileSize = new Size(size.X, size.Y),
                TilePadding = new Spacing { Right = spacing, Bottom = spacing },
                // Godot has gaps between cells, but none after the final cell.
                RegionMargin = new Spacing { Right = -spacing, Bottom = -spacing }
            };
            string suffix = id.ToString(CultureInfo.InvariantCulture);
            string file = tiles.Count == 1 ? basename : $"{basename}-tile-{suffix}";
            string name = tiles.Count == 1 ? basename : $"{basename}.tile.{suffix}";
            plan.Add(file + ".gts", new TilesheetDefinition
            {
                Name = name,
                Image = new() { FilePath = ImportNaming.RelativePath(plan.Request.OutputDirectory, path) },
                Regions = [region]
            });
            ReportGodot3Metadata(plan, id, mode, p);
        }
    }

    private static void ReportGodot3Metadata(ImportPlan plan, int id, int mode, IReadOnlyDictionary<string, string> properties)
    {
        if (mode == 1)
            plan.Report(ExternalImportSeverity.Warning, "godot.autotile",
                $"Godot 3 tile {id}: only atlas frames are imported; automatic autotile/bitmask selection behavior is not preserved.");
        var categories = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string property in properties.Keys)
        {
            if (property is "texture" or "region" or "tile_mode" ||
                (mode != 0 && property is "autotile/tile_size" or "autotile/spacing")) continue;
            string category = property switch
            {
                "name" => "name",
                _ when property.Contains("shape", StringComparison.Ordinal) || property.Contains("one_way", StringComparison.Ordinal) => "collision",
                _ when property.Contains("navpoly", StringComparison.Ordinal) || property.Contains("navigation", StringComparison.Ordinal) => "navigation",
                _ when property.Contains("occluder", StringComparison.Ordinal) => "occlusion",
                _ when property.Contains("bitmask", StringComparison.Ordinal) || property.Contains("priority", StringComparison.Ordinal) ||
                    property.Contains("fallback", StringComparison.Ordinal) || property.Contains("icon", StringComparison.Ordinal) => "selection",
                "normal_map" or "material" or "modulate" or "tex_offset" or "z_index" or "autotile/z_index_map" => "rendering",
                _ => "metadata"
            };
            if (!categories.TryGetValue(category, out var keys)) categories.Add(category, keys = []);
            keys.Add(property);
        }
        foreach (var (category, keys) in categories)
            plan.Report(category == "name" ? ExternalImportSeverity.Info : ExternalImportSeverity.Warning, "godot." + category,
                $"Godot 3 tile {id}: {category} properties ({string.Join(", ", keys)}) are omitted." +
                (category == "name" ? " Logical names use the source basename and tile ID." : ""));
    }

    private static int Pixel(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || !double.IsFinite(number))
            throw new InvalidDataException($"Expected a finite pixel value, found {value}.");
        if (number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue)
            throw new InvalidDataException($"Expected an integral pixel value, found {value}.");
        return checked((int)number);
    }

    private static int[] Geometry(string value, string kind, int count)
    {
        var match = Regex.Match(value, "^" + kind + @"\(\s*([^()]*)\s*\)$");
        if (!match.Success) throw new InvalidDataException($"Expected {kind}, found {value}.");
        var parts = match.Groups[1].Value.Split(',');
        if (parts.Length != count) throw new InvalidDataException($"Expected {count} components in {kind}.");
        return parts.Select(Pixel).ToArray();
    }

    private static Point Godot3Vector(string value)
    {
        var parts = Geometry(value, "Vector2", 2);
        return new(parts[0], parts[1]);
    }

    private static string ResourceId(string value)
    {
        if (value.StartsWith('"')) return GodotTextResource.String(value);
        if (!Regex.IsMatch(value, @"^\d+$")) throw new InvalidDataException($"Invalid Godot resource ID '{value}'.");
        return int.Parse(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key, string? fallback = null) =>
        values.TryGetValue(key, out var value) ? value : fallback ?? throw new InvalidDataException($"Missing Godot property {key}.");
    private static double Number(string value)
    {
        double number = double.Parse(value, CultureInfo.InvariantCulture);
        return double.IsFinite(number) ? number : throw new InvalidDataException("Non-finite Godot number.");
    }
    private static Point Vector(string value)
    {
        var match = Regex.Match(value, @"^Vector2i\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)$");
        if (!match.Success) throw new InvalidDataException($"Expected Vector2i, found {value}.");
        return new(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
    }
    private static string Reference(string value, string kind)
    {
        var match = Regex.Match(value, "^" + kind + @"\(\s*(""(?:\\.|[^""])*""|\d+)\s*\)$");
        return match.Success ? ResourceId(match.Groups[1].Value) : throw new InvalidDataException($"Expected {kind} reference.");
    }
    private static string ResolvePath(string source, string dependency)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(source))!;
        if (!dependency.StartsWith("res://", StringComparison.Ordinal)) return Path.GetFullPath(dependency, directory);
        var root = new DirectoryInfo(directory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "project.godot"))) root = root.Parent;
        if (root is null) throw new InvalidDataException("Cannot resolve res:// texture: put this resource under a Godot project containing project.godot.");
        return Path.GetFullPath(dependency[6..], root.FullName);
    }
}
