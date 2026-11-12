using System.Buffers.Binary;
using System.Drawing;
using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes.GSCN;
using static Gondwana.Tooling.Importers.TiledTilesetImporter;

namespace Gondwana.Tooling.Importers;

/// <summary>Converts orthogonal or isometric TMX maps, including chunked layers, into native scene, tilesheet, and animation definitions.</summary>
public sealed class TiledMapImporter : ExternalAssetImporter
{
    /// <inheritdoc/>
    public override string Id => "tiled.tmx";
    /// <inheritdoc/>
    public override string DisplayName => "Tiled Map (.tmx)";
    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedExtensions => [".tmx"];

    private sealed record ChunkedLayer(int MinX, int MinY, int Width, int Height,
        List<(int X, int Y, uint Gid)> Tiles);

    /// <inheritdoc/>
    protected override void BuildPlan(ImportPlan plan, CancellationToken token)
    {
        var root = ReadXml(plan.Request.SourcePath);
        if (root.Name != "map") throw new InvalidDataException("Expected Tiled map XML root.");
        // Tiled's "infinite" flag describes authoring/storage, not Gondwana topology.
        // It permits zero map dimensions; chunk coordinates determine native bounds.
        var orientation = (string?)root.Attribute("orientation") switch
        {
            "orthogonal" => CoordinateSystemTypes.Orthogonal,
            "isometric" => CoordinateSystemTypes.IsometricRhombic,
            _ => throw new InvalidDataException("Unsupported map orientation; only orthogonal and isometric maps are supported.")
        };
        int width = Int(root, "width"), height = Int(root, "height"), tw = Int(root, "tilewidth"), th = Int(root, "tileheight");
        if (width < 0 || height < 0 || tw <= 0 || th <= 0 ||
            (Int(root, "infinite", 0) == 0 && (width == 0 || height == 0)))
            throw new InvalidDataException("Map dimensions must be nonnegative and tile dimensions positive.");
        if ((string?)root.Attribute("renderorder") is { } order && order != "right-down")
            throw new InvalidDataException($"Unsupported Tiled render order '{order}'.");
        if (Number(root, "parallaxoriginx", 0) != 0 || Number(root, "parallaxoriginy", 0) != 0)
            throw new InvalidDataException("Nonzero Tiled parallax origins are unsupported.");
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
            // Every tileset in this map becomes a logical member of one GTS document.
            // Standalone TSX imports continue to use their individual GTS filenames.
            var set = ConvertTileset(tilesetRoot, source, basename, plan, token,
                name + ".gts", new Size(tw, th));
            // RenderTileQuery scans a fixed fringe around the viewport. Do not
            // import artwork that can intersect the viewport from farther away.
            if (!FitsRenderCandidateFringe(new Size(tw, th), set.Overhang, orientation))
                throw new InvalidDataException(
                    $"Tileset '{set.Name}' overhang exceeds the runtime render-culling fringe for this map grid.");
            tilesets.Add((first, set));
            scene.TilesheetSources.Add(SceneTilesheetSourceDefinition.Loose(set.Name, set.GtsFilename));
            foreach (var (id, animation) in set.Animations)
                scene.AnimationSources.Add(SceneAnimationSourceDefinition.Loose(animation.Key, $"{basename}-animations.gani"));
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
                var data = element.Element("data") ?? throw new InvalidDataException("Missing layer data.");
                if (element.Elements("data").Count() != 1)
                    throw new InvalidDataException("A Tiled tile layer must contain exactly one data element.");
                bool hasChunks = data.Elements("chunk").Any();
                // Tiled may serialize an entirely empty infinite layer without chunks.
                bool emptyUnbounded = Int(root, "infinite", 0) != 0 &&
                    Int(element, "width", width) == 0 && Int(element, "height", height) == 0 &&
                    !data.Elements().Any() && string.IsNullOrWhiteSpace(data.Value);
                ChunkedLayer? chunked = hasChunks || emptyUnbounded ? DecodeChunks(data, token) : null;
                int minX = chunked?.MinX ?? 0, minY = chunked?.MinY ?? 0;
                int columns = chunked?.Width ?? Int(element, "width", width);
                int rows = chunked?.Height ?? Int(element, "height", height);
                if (columns <= 0 || rows <= 0 || (long)columns * rows > 16_000_000)
                    throw new InvalidDataException("Layer bounds are empty or exceed 16 million native grid cells.");

                if (orientation == CoordinateSystemTypes.IsometricRhombic)
                {
                    x += (double)height * tw / 2.0;
                    // An odd-sized isometric cell cannot retain a half-pixel origin shift.
                    // Pad a leading empty column instead, preserving every authored anchor.
                    if (chunked is not null && ((tw | th) & 1) != 0 && (((long)minX - minY) & 1) != 0)
                    {
                        minX = checked(minX - 1);
                        columns = checked(columns + 1);
                        if ((long)columns * rows > 16_000_000)
                            throw new InvalidDataException("Isometric origin padding exceeds 16 million native grid cells.");
                    }
                }
                if (x != Math.Truncate(x) || y != Math.Truncate(y))
                    throw new InvalidDataException("Fractional pixel layer offsets cannot be represented exactly.");
                double shiftX = orientation == CoordinateSystemTypes.Orthogonal
                    ? (double)minX * tw : ((double)minX - minY) * tw / 2;
                double shiftY = orientation == CoordinateSystemTypes.Orthogonal
                    ? (double)minY * th : ((double)minX + minY) * th / 2;
                double originX = -x - shiftX, originY = -y - shiftY;
                if (originX != Math.Truncate(originX) || originY != Math.Truncate(originY))
                    throw new InvalidDataException("Chunk bounds require a fractional GSCN origin.");
                if (parallaxX != parallaxY)
                    plan.Report(ExternalImportSeverity.Warning, "tiled.parallax", "Independent X/Y parallax cannot be represented; using 1.");
                var layer = new SceneLayerDefinition
                {
                    ID = $"{name}.layer.{scene.Layers.Count}",
                    Columns = columns,
                    Rows = rows,
                    TileWidth = tw,
                    TileHeight = th,
                    CoordinateSystemType = orientation,
                    Visible = shown,
                    ZOrder = scene.Layers.Count,
                    OriginPx = new Point(checked((int)originX), checked((int)originY)),
                    Parallax = parallaxX == parallaxY ? (float)parallaxX : 1
                };

                void PlaceTile(uint rawGid, int tileX, int tileY, string context)
                {
                    token.ThrowIfCancellationRequested();
                    var (gid, transform, hasHexRotation) = TiledTileTransform.Decode(rawGid);
                    if (hasHexRotation)
                        plan.Report(ExternalImportSeverity.Warning, "tiled.transform.hex",
                            $"Layer {layer.ID}, {context}: hexagonal 120-degree flag is cleared and ignored for this orthogonal/isometric map.");
                    if (gid == 0) return;
                    var match = tilesets.LastOrDefault(t => t.First <= gid);
                    if (match.Set is null || gid - match.First >= match.Set.Count)
                        throw new InvalidDataException($"Unresolved tile GID {gid} at layer {layer.ID}, {context}.");
                    // Tiled anchors oversized tile artwork bottom-left; Gondwana rotates
                    // its overhang with the tile. Reject transforms that shift the artwork.
                    if ((match.Set.TileSize != new Size(tw, th) || !match.Set.Overhang.IsEmpty) &&
                        (TileTransformGeometry.TransformSize(new Size(tw, th), transform) != new Size(tw, th) ||
                         TileTransformGeometry.TransformSpacing(match.Set.Overhang, transform) != match.Set.Overhang))
                        throw new InvalidDataException(
                            $"Layer {layer.ID}, {context}: transformed oversized tile cannot preserve Tiled artwork anchoring.");
                    int local = (int)(gid - match.First);
                    var tile = new SceneLayerTileDefinition
                    {
                        Transform = transform,
                        X = tileX, Y = tileY,
                        Frame = new() { Tilesheet = match.Set.Name, XTile = local % match.Set.Columns, YTile = local / match.Set.Columns }
                    };
                    if (match.Set.Animations.TryGetValue(local, out var animation))
                    {
                        var frame = animation.Frames[0];
                        tile.Frame.XTile = frame.XTile; tile.Frame.YTile = frame.YTile;
                        tile.AnimationKey = animation.Key; tile.StartAnimation = true;
                    }
                    layer.Tiles.Add(tile);
                }

                if (chunked is not null)
                {
                    foreach (var tile in chunked.Tiles)
                        PlaceTile(tile.Gid, tile.X - minX, tile.Y - minY, $"cell ({tile.X}, {tile.Y})");
                }
                else
                {
                    var gids = DecodeLayer(data, checked(columns * rows));
                    for (int i = 0; i < gids.Length; i++)
                        PlaceTile(gids[i], i % columns, i / columns, $"cell {i}");
                }
                scene.Layers.Add(layer);
            }
        }
        if (root.Descendants("properties").Any()) plan.Report(ExternalImportSeverity.Warning, "tiled.properties", "Custom properties are omitted.");
        Layers(root, true, 0, 0, 1, 1);
        plan.Add(name + ".gscn", scene);
    }

    /// <summary>
    /// Conservatively bounds imported artwork to the fixed-cell candidate fringe
    /// used by RenderTileQuery. Orthogonal scanning includes one adjacent cell.
    /// For rhombic isometric scanning, each horizontal/vertical pixel offset
    /// shifts two grid axes; bounding their combined displacement preserves the
    /// same one-cell fringe without imposing a limit on ordinary source tiles.
    /// </summary>
    private static bool FitsRenderCandidateFringe(
        Size cell, Spacing oh, CoordinateSystemTypes orientation)
    {
        if (oh.IsEmpty) return true;
        if (orientation == CoordinateSystemTypes.Orthogonal)
            return oh.Left <= cell.Width && oh.Right <= cell.Width &&
                oh.Top <= cell.Height && oh.Bottom <= cell.Height;

        decimal sideInCells = (decimal)Math.Max(oh.Left, oh.Right) / cell.Width;
        // Conservative isometric bounds: top + side <= one grid step,
        // bottom + side <= half a step. Decimal avoids large-int overflow.
        return (decimal)oh.Top / cell.Height + sideInCells <= 1m &&
            (decimal)oh.Bottom / cell.Height + sideInCells <= 0.5m;
    }

    private static double Number(XElement element, string attribute, double fallback)
    {
        var result = element.Attribute(attribute) is { } a ? double.Parse(a.Value, CultureInfo.InvariantCulture) : fallback;
        if (!double.IsFinite(result)) throw new InvalidDataException($"Invalid {attribute}.");
        return result;
    }

    /// <summary>
    /// Converts Tiled's independently positioned chunks into sparse finite-layer
    /// placements; no native wrapping or dynamic grid allocation is implied.
    /// </summary>
    private static ChunkedLayer DecodeChunks(XElement data, CancellationToken token)
    {
        if (data.Elements().Any(e => e.Name != "chunk") ||
            data.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value)))
            throw new InvalidDataException("Chunked Tiled layer data cannot mix chunks with tile or inline data.");

        var chunks = data.Elements("chunk").ToArray();
        if (chunks.Length == 0) return new(0, 0, 1, 1, []);
        if (chunks.Length > 16_384)
            throw new InvalidDataException("Too many Tiled chunks in one layer (maximum 16,384).");

        var rectangles = new List<(long Left, long Top, long Right, long Bottom)>();
        var placed = new List<(int X, int Y, uint Gid)>();
        long minX = long.MaxValue, minY = long.MaxValue, maxX = long.MinValue, maxY = long.MinValue;
        long totalCells = 0;
        foreach (var chunk in chunks)
        {
            token.ThrowIfCancellationRequested();
            int x = Int(chunk, "x"), y = Int(chunk, "y");
            int width = Int(chunk, "width"), height = Int(chunk, "height");
            long cells = (long)width * height;
            if (width <= 0 || height <= 0 || cells > 16_000_000 ||
                totalCells + cells > 16_000_000)
                throw new InvalidDataException("Tiled chunk sizes exceed 16 million source cells per layer.");
            long right = (long)x + width, bottom = (long)y + height;
            if (right > (long)int.MaxValue + 1 || bottom > (long)int.MaxValue + 1)
                throw new InvalidDataException("Tiled chunk coordinates exceed the supported integer grid range.");
            if (rectangles.Any(r => x < r.Right && right > r.Left && y < r.Bottom && bottom > r.Top))
                throw new InvalidDataException($"Overlapping Tiled chunks at ({x}, {y}); tile placement is ambiguous.");
            rectangles.Add((x, y, right, bottom));
            minX = Math.Min(minX, x); minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, right); maxY = Math.Max(maxY, bottom);
            totalCells += cells;
            uint[] gids = DecodeLayer(data, chunk, checked((int)cells));
            for (int i = 0; i < gids.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                if (gids[i] != 0)
                    placed.Add((checked(x + i % width), checked(y + i / width), gids[i]));
            }
        }
        long cols = maxX - minX, rows = maxY - minY;
        if (cols > int.MaxValue || rows > int.MaxValue || cols * rows > 16_000_000)
            throw new InvalidDataException("Tiled chunk bounds exceed 16 million native grid cells; split the map.");
        return new(checked((int)minX), checked((int)minY), checked((int)cols), checked((int)rows), placed);
    }

    /// <summary>
    /// Decodes the tile identifiers in a Tiled map layer.
    /// </summary>
    /// <param name="data">The data to process.</param>
    /// <param name="expectedCount">The expected count.</param>
    /// <returns>The decoded tile identifiers in layer order.</returns>
    public static uint[] DecodeLayer(XElement data, int expectedCount) => DecodeLayer(data, data, expectedCount);

    // Encoding/compression lives on the parent <data>; each <chunk> has its own payload.
    private static uint[] DecodeLayer(XElement attributes, XElement payload, int expectedCount)
    {
        if (expectedCount <= 0 || expectedCount > 16_000_000) throw new InvalidDataException("Invalid or excessive layer size.");
        uint[] result;
        switch ((string?)attributes.Attribute("encoding"))
        {
            case null: result = payload.Elements("tile").Select(t => uint.Parse((string?)t.Attribute("gid") ?? "0", CultureInfo.InvariantCulture)).ToArray(); break;
            case "csv": result = payload.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(s => uint.Parse(s, CultureInfo.InvariantCulture)).ToArray(); break;
            case "base64":
                using (var input = new MemoryStream(Convert.FromBase64String(payload.Value)))
                using (Stream stream = (string?)attributes.Attribute("compression") switch
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
