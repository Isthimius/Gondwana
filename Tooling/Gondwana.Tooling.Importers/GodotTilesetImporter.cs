using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using SkiaSharp;

namespace Gondwana.Tooling.Importers;

public sealed class GodotTilesetImporter : ExternalAssetImporter
{
    public override string Id => "godot.tileset";
    public override string DisplayName => "Godot 4 TileSet (.tres)";
    public override IReadOnlyList<string> SupportedExtensions => [".tres"];

    protected override void BuildPlan(ImportPlan plan, CancellationToken token)
    {
        var sections = GodotTextResource.Parse(File.ReadAllText(plan.Request.SourcePath));
        if (sections.Count == 0 || sections[0].Kind != "gd_resource" ||
            !sections[0].Attributes.TryGetValue("type", out var type) || GodotTextResource.String(type) != "TileSet")
            throw new InvalidDataException("Expected a Godot text TileSet resource.");
        if (!sections[0].Attributes.TryGetValue("format", out var format) || format != "3")
            throw new InvalidDataException("Only Godot 4 text resource format 3 is supported.");
        if (sections.Count(s => s.Kind == "resource") != 1 ||
            sections.Where(s => s.Kind is "sub_resource" or "ext_resource")
                .GroupBy(s => (s.Kind, Id: Get(s.Attributes, "id"))).Any(g => g.Count() != 1))
            throw new InvalidDataException("Duplicate or missing Godot resource sections/IDs.");
        var root = sections.SingleOrDefault(s => s.Kind == "resource") ?? throw new InvalidDataException("Missing resource section.");
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
            var atlas = sections.SingleOrDefault(s => s.Kind == "sub_resource" && s.Attributes.TryGetValue("id", out var v) && GodotTextResource.String(v) == resourceId)
                ?? throw new InvalidDataException($"Unresolved atlas subresource {resourceId}.");
            if (!atlas.Attributes.TryGetValue("type", out var atlasType) || GodotTextResource.String(atlasType) != "TileSetAtlasSource")
                throw new InvalidDataException("Only TileSetAtlasSource is supported; scene collection sources are unsupported.");
            var p = atlas.Properties;
            string textureId = Reference(Get(p, "texture"), "ExtResource");
            var texture = sections.SingleOrDefault(s => s.Kind == "ext_resource" && s.Attributes.TryGetValue("id", out var v) && GodotTextResource.String(v) == textureId)
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
            var region = new TilesheetRegionDefinition { Area = new Rectangle(0, 0, image.Width, image.Height), TileSize = new Size(size.X, size.Y),
                TilePadding = new Spacing { Right = gap.X, Bottom = gap.Y },
                RegionMargin = new Spacing { Left = margin.X, Top = margin.Y, Right = -gap.X, Bottom = -gap.Y } };
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
                var gani = new AnimationDefinition { Key = $"{name}.tile.{xy[0]}.{xy[1]}", CycleType = CycleType.Repeating,
                    TilesheetSources = [AnimationTilesheetSourceDefinition.Loose(name, file + ".gts")] };
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
        var match = Regex.Match(value, "^" + kind + @"\(\s*(""(?:\\.|[^""])*"")\s*\)$");
        return match.Success ? GodotTextResource.String(match.Groups[1].Value) : throw new InvalidDataException($"Expected {kind} reference.");
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
