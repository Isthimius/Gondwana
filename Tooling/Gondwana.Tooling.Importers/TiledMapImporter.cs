using System.Buffers.Binary;
using System.Drawing;
using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes.GSCN;
using static Gondwana.Tooling.Importers.TiledTilesetImporter;

namespace Gondwana.Tooling.Importers;

public sealed class TiledMapImporter : ExternalAssetImporter
{
    public override string Id => "tiled.tmx";
    public override string DisplayName => "Tiled Map (.tmx)";
    public override IReadOnlyList<string> SupportedExtensions => [".tmx"];

    protected override void BuildPlan(ImportPlan plan, CancellationToken token)
    {
        var root = ReadXml(plan.Request.SourcePath);
        if (root.Name != "map") throw new InvalidDataException("Expected Tiled map XML root.");
        if (Int(root, "infinite", 0) != 0) throw new InvalidDataException("Infinite/chunked maps are unsupported.");
        var orientation = (string?)root.Attribute("orientation") switch
        {
            "orthogonal" => CoordinateSystemTypes.Orthogonal,
            "isometric" => CoordinateSystemTypes.IsometricRhombic,
            _ => throw new InvalidDataException("Unsupported map orientation; only orthogonal and isometric maps are supported.")
        };
        int width = Int(root, "width"), height = Int(root, "height"), tw = Int(root, "tilewidth"), th = Int(root, "tileheight");
        if (width <= 0 || height <= 0 || tw <= 0 || th <= 0) throw new InvalidDataException("Map dimensions must be positive.");
        string name = ImportNaming.Sanitize(Path.GetFileNameWithoutExtension(plan.Request.SourcePath));
        var scene = new SceneDefinition { ID = name };
        var tilesets = new List<(uint First, Tileset Set)>();
        foreach (var reference in root.Elements("tileset"))
        {
            token.ThrowIfCancellationRequested();
            uint first = uint.Parse((string?)reference.Attribute("firstgid") ?? "0", CultureInfo.InvariantCulture);
            if (first == 0 || tilesets.Any(t => t.First == first)) throw new InvalidDataException("Invalid or duplicate firstgid.");
            string source = plan.Request.SourcePath;
            string basename;
            XElement tilesetRoot;
            if (reference.Attribute("source") is { } external)
            {
                source = Path.GetFullPath(external.Value, Path.GetDirectoryName(Path.GetFullPath(source))!);
                plan.Dependencies.Add(source);
                tilesetRoot = ReadXml(source);
                basename = ImportNaming.Sanitize(Path.GetFileNameWithoutExtension(source));
            }
            else { tilesetRoot = reference; basename = $"{name}-tileset-{first}"; }
            var set = ConvertTileset(tilesetRoot, source, basename, plan, token);
            if (set.TileSize != new Size(tw, th))
                throw new InvalidDataException("Tileset frame size differs from map cell size; tile anchoring/scaling cannot be preserved in this import.");
            tilesets.Add((first, set));
            scene.TilesheetSources.Add(SceneTilesheetSourceDefinition.Loose(set.Name, set.GtsFilename));
            foreach (var (id, animation) in set.Animations)
                scene.AnimationSources.Add(SceneAnimationSourceDefinition.Loose(animation.Key, $"{basename}-tile-{id}.gani"));
        }
        tilesets.Sort((a, b) => a.First.CompareTo(b.First));
        for (int i = 1; i < tilesets.Count; i++)
            if ((ulong)tilesets[i - 1].First + (uint)tilesets[i - 1].Set.Count > tilesets[i].First)
                throw new InvalidDataException("Overlapping tileset GID ranges.");

        void Layers(XElement parent, bool visible, double ox, double oy, double px, double py)
        {
            foreach (var element in parent.Elements().Where(e => e.Name.LocalName is "layer" or "group" or "objectgroup" or "imagelayer"))
            {
                token.ThrowIfCancellationRequested();
                var kind = element.Name.LocalName;
                if (kind is "objectgroup" or "imagelayer")
                { plan.Report(ExternalImportSeverity.Warning, "tiled.layer.unsupported", $"{kind} '{(string?)element.Attribute("name")}' is omitted."); continue; }
                bool shown = visible && Int(element, "visible", 1) != 0;
                double x = ox + Number(element, "offsetx", 0), y = oy + Number(element, "offsety", 0);
                double parallaxX = px * Number(element, "parallaxx", 1), parallaxY = py * Number(element, "parallaxy", 1);
                if (Number(element, "opacity", 1) != 1 || element.Attribute("tintcolor") is not null || element.Attribute("blendmode") is not null)
                    plan.Report(ExternalImportSeverity.Warning, "tiled.layer.appearance", "Layer opacity, tint, and blend mode are not represented and are omitted.");
                if (kind == "group") { Layers(element, shown, x, y, parallaxX, parallaxY); continue; }
                if (Int(element, "x", 0) != 0 || Int(element, "y", 0) != 0)
                    throw new InvalidDataException("Nonzero legacy layer tile coordinates are unsupported; use pixel offsets.");
                if (orientation == CoordinateSystemTypes.IsometricRhombic) x += height * tw / 2.0;
                if (x != Math.Truncate(x) || y != Math.Truncate(y)) throw new InvalidDataException("Fractional pixel layer offsets cannot be represented exactly.");
                if (parallaxX != parallaxY)
                    plan.Report(ExternalImportSeverity.Warning, "tiled.parallax", "Independent X/Y parallax cannot be represented; using 1.");
                var layer = new SceneLayerDefinition { ID = $"{name}.layer.{scene.Layers.Count}", Columns = Int(element, "width", width), Rows = Int(element, "height", height),
                    TileWidth = tw, TileHeight = th, CoordinateSystemType = orientation, Visible = shown, ZOrder = scene.Layers.Count,
                    OriginPx = new Point(checked((int)-x), checked((int)-y)), Parallax = parallaxX == parallaxY ? (float)parallaxX : 1 };
                var gids = DecodeLayer(element.Element("data") ?? throw new InvalidDataException("Missing layer data."), checked(layer.Columns * layer.Rows));
                for (int i = 0; i < gids.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    uint raw = gids[i], gid = raw & 0x0fffffff;
                    if (raw != gid) { plan.Report(ExternalImportSeverity.Error, "tiled.transform", $"Layer {layer.ID}, cell {i}: flipped/rotated GID cannot be represented."); continue; }
                    if (gid == 0) continue;
                    var match = tilesets.LastOrDefault(t => t.First <= gid);
                    if (match.Set is null || gid - match.First >= match.Set.Count) throw new InvalidDataException($"Unresolved tile GID {gid}.");
                    int local = (int)(gid - match.First);
                    var tile = new SceneLayerTileDefinition { X = i % layer.Columns, Y = i / layer.Columns,
                        Frame = new() { Tilesheet = match.Set.Name, XTile = local % match.Set.Columns, YTile = local / match.Set.Columns } };
                    if (match.Set.Animations.TryGetValue(local, out var animation))
                    {
                        var frame = animation.Frames[0];
                        tile.Frame.XTile = frame.XTile; tile.Frame.YTile = frame.YTile;
                        tile.AnimationKey = animation.Key; tile.StartAnimation = true;
                    }
                    layer.Tiles.Add(tile);
                }
                scene.Layers.Add(layer);
            }
        }
        if (root.Descendants("properties").Any()) plan.Report(ExternalImportSeverity.Warning, "tiled.properties", "Custom properties are omitted.");
        Layers(root, true, 0, 0, 1, 1);
        plan.Add(name + ".gscn", scene);
    }

    private static double Number(XElement element, string attribute, double fallback)
    {
        var result = element.Attribute(attribute) is { } a ? double.Parse(a.Value, CultureInfo.InvariantCulture) : fallback;
        if (!double.IsFinite(result)) throw new InvalidDataException($"Invalid {attribute}.");
        return result;
    }

    public static uint[] DecodeLayer(XElement data, int expectedCount)
    {
        if (expectedCount <= 0 || expectedCount > 16_000_000) throw new InvalidDataException("Invalid or excessive layer size.");
        uint[] result;
        switch ((string?)data.Attribute("encoding"))
        {
            case null: result = data.Elements("tile").Select(t => uint.Parse((string?)t.Attribute("gid") ?? "0", CultureInfo.InvariantCulture)).ToArray(); break;
            case "csv": result = data.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(s => uint.Parse(s, CultureInfo.InvariantCulture)).ToArray(); break;
            case "base64":
                using (var input = new MemoryStream(Convert.FromBase64String(data.Value)))
                using (Stream stream = (string?)data.Attribute("compression") switch
                {
                    null or "" => input,
                    "zlib" => new ZLibStream(input, CompressionMode.Decompress),
                    "gzip" => new GZipStream(input, CompressionMode.Decompress),
                    var compression => throw new InvalidDataException($"Unsupported layer compression: {compression}.")
                })
                {
                    var bytes = new byte[checked(expectedCount * 4)];
                    stream.ReadExactly(bytes);
                    if (stream.ReadByte() != -1) throw new InvalidDataException("Layer data exceeds declared dimensions.");
                    result = new uint[expectedCount];
                    for (int i = 0; i < expectedCount; i++) result[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * 4, 4));
                }
                break;
            default: throw new InvalidDataException("Unsupported layer encoding.");
        }
        if (result.Length != expectedCount) throw new InvalidDataException("Layer data length differs from declared dimensions.");
        return result;
    }
}
