using System.Drawing;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using SkiaSharp;

namespace Gondwana.Tooling.Importers;

public sealed class TiledTilesetImporter : ExternalAssetImporter
{
    public override string Id => "tiled.tsx";
    public override string DisplayName => "Tiled Tileset (.tsx)";
    public override IReadOnlyList<string> SupportedExtensions => [".tsx"];
    protected override void BuildPlan(ImportPlan plan, CancellationToken cancellationToken) =>
        ConvertTileset(ReadXml(plan.Request.SourcePath), plan.Request.SourcePath,
            ImportNaming.Sanitize(Path.GetFileNameWithoutExtension(plan.Request.SourcePath)), plan, cancellationToken);

    internal static XElement ReadXml(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader).Root ?? throw new InvalidDataException("Missing XML root.");
    }

    internal static int Int(XElement element, string name, int? fallback = null) =>
        element.Attribute(name) is { } attribute ? int.Parse(attribute.Value, CultureInfo.InvariantCulture)
        : fallback ?? throw new InvalidDataException($"Missing {element.Name}.{name}.");

    internal sealed record Tileset(string Name, string GtsFilename, int Columns, int Count, Size TileSize,
        Dictionary<int, AnimationDefinition> Animations);

    internal static Tileset ConvertTileset(XElement root, string source, string filename, ImportPlan plan, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (root.Name != "tileset") throw new InvalidDataException("Expected a Tiled tileset XML root.");
        if (root.Elements("image").Count() != 1 || root.Elements("tile").Any(t => t.Element("image") is not null))
            throw new InvalidDataException("Collection-of-images tilesets are unsupported; use a single atlas image.");
        var image = root.Element("image")!;
        string imageSource = (string?)image.Attribute("source") ?? throw new InvalidDataException("Embedded tileset image data is unsupported.");
        string imagePath = Path.GetFullPath(imageSource, Path.GetDirectoryName(Path.GetFullPath(source))!);
        plan.Dependencies.Add(imagePath);
        using var bitmap = SKBitmap.Decode(imagePath) ?? throw new InvalidDataException($"Cannot decode atlas image: {imagePath}");
        int width = Int(root, "tilewidth"), height = Int(root, "tileheight");
        int spacing = Int(root, "spacing", 0), margin = Int(root, "margin", 0);
        int columns = Int(root, "columns"), count = Int(root, "tilecount");
        if (width <= 0 || height <= 0 || columns <= 0 || count <= 0 || spacing < 0 || margin < 0)
            throw new InvalidDataException("Tileset dimensions/count must be positive and spacing/margin non-negative.");
        if (Int(image, "width", bitmap.Width) != bitmap.Width || Int(image, "height", bitmap.Height) != bitmap.Height)
            throw new InvalidDataException("Declared image dimensions differ from the source image.");
        string name = ImportNaming.Sanitize((string?)root.Attribute("name") ?? filename);
        var region = new TilesheetRegionDefinition
        {
            Area = new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            TileSize = new Size(width, height),
            TilePadding = new Spacing { Right = spacing, Bottom = spacing },
            RegionMargin = new Spacing { Left = margin, Top = margin, Right = margin - spacing, Bottom = margin - spacing }
        };
        var grid = TilesheetDefinitionValidator.GridSize(region);
        if (grid.Columns != columns || count > grid.Columns * grid.Rows)
            throw new InvalidDataException("Tileset columns/tilecount do not fit the atlas geometry.");
        var definition = new TilesheetDefinition { Name = name, Image = new() { FilePath = ImportNaming.RelativePath(plan.Request.OutputDirectory, imagePath) }, Regions = [region] };
        if (image.Attribute("trans") is { } transparent)
        {
            var color = transparent.Value.TrimStart('#');
            if (color.Length != 6) throw new InvalidDataException("Transparent color must contain exactly six hexadecimal digits.");
            uint rgb = uint.Parse(color, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            definition.Mask = new() { Red = (byte)(rgb >> 16), Green = (byte)(rgb >> 8), Blue = (byte)rgb, Alpha = 255, Tolerance = 0 };
        }
        foreach (var feature in root.Descendants().Where(e => e.Name.LocalName is "wangsets" or "terraintypes" or "properties" or "objectgroup" or "transformations"))
            plan.Report(ExternalImportSeverity.Warning, "tiled.metadata", $"{feature.Name} is not represented in native tilesheets and will be omitted.");
        if (root.Element("tileoffset") is { } offset && (Int(offset, "x", 0) != 0 || Int(offset, "y", 0) != 0))
            throw new InvalidDataException("Non-zero tileset tile offsets are unsupported.");
        if (root.Elements("tile").Any(t => t.Attributes().Any(a => a.Name.LocalName is "x" or "y" or "width" or "height")))
            throw new InvalidDataException("Per-tile atlas subrectangles are unsupported.");
        if (grid.Columns * grid.Rows > count)
            plan.Report(ExternalImportSeverity.Info, "tiled.unused", "The atlas exposes unused grid cells; generated references use only valid tile IDs.");
        string gts = filename + ".gts";
        plan.Add(gts, definition);
        var animations = new Dictionary<int, AnimationDefinition>();
        var tileIds = new HashSet<int>();
        foreach (var tile in root.Elements("tile"))
        {
            token.ThrowIfCancellationRequested();
            int id = Int(tile, "id");
            if (id < 0 || id >= count) throw new InvalidDataException($"Tile ID {id} is outside tilecount.");
            if (!tileIds.Add(id)) throw new InvalidDataException($"Duplicate tile ID {id}.");
            if (tile.Element("animation") is not { } animation) continue;
            var gani = new AnimationDefinition
            {
                Key = $"{name}.tile.{id}",
                CycleType = CycleType.Repeating,
                TilesheetSources = [AnimationTilesheetSourceDefinition.Loose(name, gts)]
            };
            foreach (var frame in animation.Elements("frame"))
            {
                token.ThrowIfCancellationRequested();
                int frameId = Int(frame, "tileid"), duration = Int(frame, "duration");
                if (frameId < 0 || frameId >= count || duration <= 0) throw new InvalidDataException("Animation frame ID or duration is invalid.");
                gani.Frames.Add(new() { Tilesheet = name, XTile = frameId % columns, YTile = frameId / columns, DurationSeconds = duration / 1000.0 });
            }
            if (gani.Frames.Count == 0) throw new InvalidDataException($"Animation for tile {id} has no frames.");
            if (!animations.TryAdd(id, gani)) throw new InvalidDataException($"Duplicate animated tile ID {id}.");
            plan.Add($"{filename}-tile-{id}.gani", gani);
        }
        return new(name, gts, columns, count, new(width, height), animations);
    }
}
