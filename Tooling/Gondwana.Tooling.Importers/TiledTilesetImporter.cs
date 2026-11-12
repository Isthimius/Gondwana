using System.Drawing;
using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using SkiaSharp;

namespace Gondwana.Tooling.Importers;

/// <summary>Converts single-atlas Tiled TSX tilesets and tile animations into native definitions.</summary>
public sealed class TiledTilesetImporter : ExternalAssetImporter
{
    /// <inheritdoc/>
    public override string Id => "tiled.tsx";
    /// <inheritdoc/>
    public override string DisplayName => "Tiled Tileset (.tsx)";
    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedExtensions => [".tsx"];
    /// <inheritdoc/>
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

    internal sealed record TileCollision(CollisionAdjust Adjust, TileCollisionType Type);

    internal sealed record Tileset(string Name, string GtsFilename, int Columns, int Count, Size TileSize,
        Spacing Overhang, Dictionary<int, AnimationDefinition> Animations,
        Dictionary<int, TileCollision> Collisions);

    private const int MaxEmbeddedImageBytes = 64 * 1024 * 1024;

    // Embedded Tiled image data contains encoded image bytes, not a raw pixel buffer.
    // Normalize them into a generated PNG so native GTS files have a stable dependency.
    private static (SKBitmap Bitmap, string ImageReference) LoadAtlasImage(
        XElement image, string source, string filename, ImportPlan plan, CancellationToken token)
    {
        string? imageSource = (string?)image.Attribute("source");
        var dataElements = image.Elements("data").ToArray();
        if (dataElements.Length > 1)
            throw new InvalidDataException("Tiled atlas image must have only one data element.");
        XElement? data = dataElements.SingleOrDefault();
        if (imageSource is not null)
        {
            if (string.IsNullOrWhiteSpace(imageSource) || data is not null)
                throw new InvalidDataException("Tiled atlas image must use either a source path or embedded data, not both.");
            string path = Path.GetFullPath(imageSource, Path.GetDirectoryName(Path.GetFullPath(source))!);
            plan.Dependencies.Add(path);
            return (SKBitmap.Decode(path) ?? throw new InvalidDataException($"Cannot decode atlas image: {path}"),
                ImportNaming.RelativePath(plan.Request.OutputDirectory, path));
        }
        if (data is null)
            throw new InvalidDataException("Tiled atlas image is missing both a source path and embedded data.");
        string format = ((string?)image.Attribute("format") ?? "").TrimStart('.').ToLowerInvariant();
        if (format is not ("png" or "jpg" or "jpeg" or "bmp" or "gif" or "webp"))
            throw new InvalidDataException($"Unsupported embedded Tiled image format '{format}'; use a Skia-decodable raster format.");
        if ((string?)data.Attribute("encoding") != "base64")
            throw new InvalidDataException("Embedded Tiled atlas data must use base64 encoding.");
        token.ThrowIfCancellationRequested();
        if (data.Value.Length > MaxEmbeddedImageBytes * 2)
            throw new InvalidDataException("Embedded Tiled atlas image exceeds the size limit.");
        byte[] bytes = Convert.FromBase64String(data.Value);
        if (bytes.Length > MaxEmbeddedImageBytes)
            throw new InvalidDataException("Embedded Tiled atlas image exceeds the size limit.");
        string? compression = (string?)data.Attribute("compression");
        if (compression is not (null or "" or "zlib" or "gzip"))
            throw new InvalidDataException($"Unsupported embedded Tiled atlas compression '{compression}'.");
        if (compression is "zlib" or "gzip")
        {
            using var input = new MemoryStream(bytes);
            using Stream decompressor = compression == "zlib"
                ? new ZLibStream(input, CompressionMode.Decompress)
                : new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = decompressor.Read(buffer)) > 0)
            {
                token.ThrowIfCancellationRequested();
                if (output.Length + count > MaxEmbeddedImageBytes)
                    throw new InvalidDataException("Decompressed Tiled atlas image exceeds the size limit.");
                output.Write(buffer, 0, count);
            }
            bytes = output.ToArray();
        }
        token.ThrowIfCancellationRequested();
        // Query encoded dimensions before allocating a decoded bitmap. Small compressed
        // images can otherwise expand into enormous allocations during analysis.
        using var codec = SKCodec.Create(new MemoryStream(bytes)) ??
            throw new InvalidDataException("Embedded Tiled atlas image cannot be decoded.");
        if (codec.Info.Width <= 0 || codec.Info.Height <= 0 ||
            (long)codec.Info.Width * codec.Info.Height > 64_000_000)
            throw new InvalidDataException("Embedded Tiled atlas dimensions exceed the 64-million-pixel limit.");
        var bitmap = SKBitmap.Decode(codec) ??
            throw new InvalidDataException("Embedded Tiled atlas image cannot be decoded.");
        try
        {
            using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100) ??
                throw new InvalidDataException("Cannot encode embedded Tiled atlas as PNG.");
            string imageFilename = filename + "-atlas.png";
            plan.Add(imageFilename, "PNG", null, png.ToArray());
            return (bitmap, imageFilename);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    // Tiled's tile-local rectangle coordinates are measured from the atlas
    // artwork's top-left corner. GTS collision adjustments are signed insets
    // from the same artwork rectangle; negative insets preserve overhanging shapes.
    private static TileCollision? ReadTileCollision(
        XElement tile, string tilesetName, int tileId, Size tileSize,
        bool sourceSizeIsDrawnSize, ImportPlan plan)
    {
        var groups = tile.Elements("objectgroup").ToArray();
        if (groups.Length == 0) return null;

        void Omit(string reason) =>
            plan.Report(ExternalImportSeverity.Warning, "tiled.collision.unsupported",
                $"Tileset '{tilesetName}', tile {tileId}: {reason}; collision shapes are omitted.");

        if (groups.Length != 1)
        {
            Omit("multiple collision object groups are unsupported");
            return null;
        }
        var group = groups[0];
        if (!sourceSizeIsDrawnSize)
        {
            Omit("grid-rendered artwork changes source dimensions and collision scaling cannot be preserved");
            return null;
        }
        if (group.Elements().Any(e => e.Name.LocalName is not ("object" or "properties")) ||
            group.Element("properties") is not null ||
            group.Attributes().Any(a => a.Name.LocalName is "class" or "type" &&
                !string.IsNullOrWhiteSpace(a.Value)))
        {
            Omit("collision object-group metadata cannot be represented");
            return null;
        }
        foreach (string attribute in new[] { "x", "y", "offsetx", "offsety" })
        {
            if (Number(group, attribute, 0) != 0)
            {
                Omit($"collision object-group {attribute} offsets are unsupported");
                return null;
            }
        }
        var objects = group.Elements("object").ToArray();
        if (objects.Length != 1)
        {
            Omit($"expected one rectangle, found {objects.Length} collision objects");
            return null;
        }
        var rectangle = objects[0];
        if (rectangle.Elements().Any(e => e.Name.LocalName != "properties") ||
            rectangle.Element("properties") is not null ||
            rectangle.Attribute("gid") is not null ||
            rectangle.Attribute("template") is not null ||
            (string?)rectangle.Attribute("class") is { Length: > 0 } ||
            (string?)rectangle.Attribute("type") is { Length: > 0 } ||
            Number(rectangle, "rotation", 0) != 0)
        {
            Omit("rotated, non-rectangular or custom collision objects are unsupported");
            return null;
        }

        double x = Number(rectangle, "x", 0), y = Number(rectangle, "y", 0);
        double width = Number(rectangle, "width", 0), height = Number(rectangle, "height", 0);
        if (width <= 0 || height <= 0 ||
            x != Math.Truncate(x) || y != Math.Truncate(y) ||
            width != Math.Truncate(width) || height != Math.Truncate(height) ||
            x < int.MinValue || x > int.MaxValue ||
            y < int.MinValue || y > int.MaxValue ||
            width > int.MaxValue || height > int.MaxValue ||
            x + width < int.MinValue || x + width > int.MaxValue ||
            y + height < int.MinValue || y + height > int.MaxValue)
        {
            Omit("rectangle must have positive, integral dimensions and representable pixel coordinates");
            return null;
        }

        var adjust = new CollisionAdjust(
            top: (int)y, bottom: (int)(tileSize.Height - (y + height)),
            left: (int)x, right: (int)(tileSize.Width - (x + width)));
        return new TileCollision(adjust, TileCollisionType.Blocking);
    }

    private static double Number(XElement element, string attribute, double fallback)
    {
        double number = element.Attribute(attribute) is { } a
            ? double.Parse(a.Value, CultureInfo.InvariantCulture)
            : fallback;
        if (!double.IsFinite(number))
            throw new InvalidDataException($"Invalid Tiled collision {attribute}: must be finite.");
        return number;
    }

    internal static Tileset ConvertTileset(XElement root, string source, string filename, ImportPlan plan, CancellationToken token, string? sharedGtsFilename = null, Size? mapCellSize = null)
    {
        token.ThrowIfCancellationRequested();
        if (root.Name != "tileset") throw new InvalidDataException("Expected a Tiled tileset XML root.");
        if (root.Elements("image").Count() != 1 || root.Elements("tile").Any(t => t.Element("image") is not null))
            throw new InvalidDataException("Collection-of-images tilesets are unsupported; use a single atlas image.");
        var image = root.Element("image")!;
        var (decoded, imageReference) = LoadAtlasImage(image, source, filename, plan, token);
        using var bitmap = decoded;
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
        // Tiled tile-layer images are bottom-left aligned to the map cell for
        // orthogonal and isometric maps (tile objects have different alignment rules).
        // Tileset drawing offsets shift that image; only shifts keeping the logical
        // cell inside the drawn image can be expressed with nonnegative GTS overhang.
        // Preserve the original atlas slices and express the visual extension
        // beyond the map cell as Gondwana's region overhang.
        XElement? tileOffset = root.Element("tileoffset");
        int offsetX = tileOffset is null ? 0 : Int(tileOffset, "x", 0);
        int offsetY = tileOffset is null ? 0 : Int(tileOffset, "y", 0);
        if (mapCellSize is null && (offsetX != 0 || offsetY != 0))
            throw new InvalidDataException("Non-zero tileset tile offsets are unsupported without a TMX map cell.");

        if (mapCellSize is { } cell)
        {
            string renderSize = (string?)root.Attribute("tilerendersize") ?? "tile";
            if (renderSize is not ("tile" or "grid"))
                throw new InvalidDataException($"Unsupported Tiled tile render size '{renderSize}'.");
            if (renderSize == "grid" &&
                (string?)root.Attribute("fillmode") is { } fill && fill != "stretch")
                throw new InvalidDataException($"Unsupported Tiled grid tile fill mode '{fill}'.");

            int drawnWidth = renderSize == "grid" ? cell.Width : width;
            int drawnHeight = renderSize == "grid" ? cell.Height : height;
            // Grid render sizing still requires a supported source footprint.
            if (width < cell.Width || height < cell.Height)
                throw new InvalidDataException("Tileset frame is smaller than the map cell; negative overhang cannot be represented.");

            long left = -(long)offsetX;
            long top = (long)drawnHeight - cell.Height - offsetY;
            long right = (long)drawnWidth - cell.Width + offsetX;
            long bottom = offsetY;
            if (left < 0 || top < 0 || right < 0 || bottom < 0 ||
                left > int.MaxValue || top > int.MaxValue ||
                right > int.MaxValue || bottom > int.MaxValue)
                throw new InvalidDataException(
                    "Tile drawing offset cannot be represented by nonnegative GTS overhang.");
            region.Overhang = new Spacing((int)left, (int)top, (int)right, (int)bottom);
            if (!region.Overhang.IsEmpty)
                plan.Report(ExternalImportSeverity.Info, "tiled.overhang",
                    $"Tileset '{name}' extends beyond the map cell; the extension was imported as GTS overhang.");
        }

        var grid = TilesheetDefinitionValidator.GridSize(region);
        if (grid.Columns != columns || count > grid.Columns * grid.Rows)
            throw new InvalidDataException("Tileset columns/tilecount do not fit the atlas geometry.");
        var definition = new TilesheetDefinition { Name = name, Image = new() { FilePath = imageReference }, Regions = [region] };
        if (image.Attribute("trans") is { } transparent)
        {
            var color = transparent.Value.TrimStart('#');
            if (color.Length != 6) throw new InvalidDataException("Transparent color must contain exactly six hexadecimal digits.");
            uint rgb = uint.Parse(color, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            definition.Mask = new() { Red = (byte)(rgb >> 16), Green = (byte)(rgb >> 8), Blue = (byte)rgb, Alpha = 255, Tolerance = 0 };
        }
        foreach (var feature in root.Descendants().Where(e => e.Name.LocalName is "wangsets" or "terraintypes" or "properties" or "transformations"))
            plan.Report(ExternalImportSeverity.Warning, "tiled.metadata", $"{feature.Name} is not represented in native tilesheets and will be omitted.");
        if (root.Elements("tile").Any(t => t.Attributes().Any(a => a.Name.LocalName is "x" or "y" or "width" or "height")))
            throw new InvalidDataException("Per-tile atlas subrectangles are unsupported.");
        if (grid.Columns * grid.Rows > count)
            plan.Report(ExternalImportSeverity.Info, "tiled.unused", "The atlas exposes unused grid cells; generated references use only valid tile IDs.");
        string gts = sharedGtsFilename ?? filename + ".gts";
        plan.Add(gts, definition);
        var animations = new Dictionary<int, AnimationDefinition>();
        var collisions = new Dictionary<int, TileCollision>();
        bool collisionDrawsAtSourceSize = mapCellSize is null ||
            (string?)root.Attribute("tilerendersize") != "grid" ||
            (mapCellSize.Value.Width == width && mapCellSize.Value.Height == height);
        var tileIds = new HashSet<int>();
        foreach (var tile in root.Elements("tile"))
        {
            token.ThrowIfCancellationRequested();
            int id = Int(tile, "id");
            if (id < 0 || id >= count) throw new InvalidDataException($"Tile ID {id} is outside tilecount.");
            if (!tileIds.Add(id)) throw new InvalidDataException($"Duplicate tile ID {id}.");
            if (ReadTileCollision(tile, name, id, new Size(width, height),
                collisionDrawsAtSourceSize, plan) is { } collision)
            {
                region.Frames.Add(new TilesheetFrameDefinition
                {
                    XTile = id % columns,
                    YTile = id / columns,
                    CollisionAdjust = collision.Adjust,
                    CollisionType = collision.Type
                });
                collisions.Add(id, collision);
            }
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
            plan.Add($"{filename}-animations.gani", gani);
        }
        return new(name, gts, columns, count, new(width, height), region.Overhang, animations, collisions);
    }
}
