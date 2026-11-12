using System.Drawing;
using System.IO.Compression;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using SkiaSharp;

namespace Gondwana.Tooling.Importers.Tests;

// All fixture XML and pixels are purpose-built here; no external tools or artwork.
/// <summary>
/// Contains regression tests for tiled import.
/// </summary>
public sealed class TiledImportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "GondwanaImportTests-" + Guid.NewGuid().ToString("N"));
    /// <summary>
    /// Initializes a new instance of the <c>TiledImportTests</c> class.
    /// </summary>
    public TiledImportTests() => Directory.CreateDirectory(directory);
    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(directory, true);

    private string Tileset()
    {
        using var bitmap = new SKBitmap(7, 4);
        bitmap.Erase(SKColors.Red);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(directory, "atlas.png"), data.ToArray());
        string path = Path.Combine(directory, "terrain.tsx");
        File.WriteAllText(path, """
            <tileset name="terrain" tilewidth="2" tileheight="2" tilecount="2" columns="2" margin="1" spacing="1">
              <image source="atlas.png" width="7" height="4" trans="ff00ff"/>
              <tile id="0"><animation><frame tileid="1" duration="100"/><frame tileid="0" duration="250"/></animation></tile>
            </tileset>
            """);
        return path;
    }


    private void EmbedAtlasInTileset(string tsx, string compression = "")
    {
        byte[] pixels = File.ReadAllBytes(Path.Combine(directory, "atlas.png"));
        if (compression.Length != 0)
        {
            using var stream = new MemoryStream();
            using (Stream compressor = compression == "zlib"
                ? new ZLibStream(stream, CompressionLevel.Optimal, true)
                : new GZipStream(stream, CompressionLevel.Optimal, true))
                compressor.Write(pixels);
            pixels = stream.ToArray();
        }
        const string external = "<image source=\"atlas.png\" width=\"7\" height=\"4\" trans=\"ff00ff\"/>";
        string embedded = $"<image format=\"png\" width=\"7\" height=\"4\" trans=\"ff00ff\"><data encoding=\"base64\"{(compression.Length == 0 ? "" : $" compression=\"{compression}\"")}>{Convert.ToBase64String(pixels)}</data></image>";
        string contents = File.ReadAllText(tsx);
        Assert.Contains(external, contents);
        File.WriteAllText(tsx, contents.Replace(external, embedded));
    }

    private static string ChunkPayload(string encoding, string compression, params uint[] gids)
    {
        if (encoding == "xml")
            return string.Concat(gids.Select(gid => $"<tile gid='{gid}'/>"));
        if (encoding == "csv") return string.Join(",", gids);

        var bytes = new byte[gids.Length * 4];
        for (int i = 0; i < gids.Length; i++)
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), gids[i]);
        if (compression.Length != 0)
        {
            using var output = new MemoryStream();
            using (Stream compressor = compression == "zlib"
                ? new ZLibStream(output, CompressionLevel.Optimal, true)
                : new GZipStream(output, CompressionLevel.Optimal, true))
                compressor.Write(bytes);
            bytes = output.ToArray();
        }
        return Convert.ToBase64String(bytes);
    }

    private string ChunkedMap(string chunks, string encoding = "csv", string compression = "",
        string orientation = "orthogonal", int tileWidth = 2, int tileHeight = 2,
        string layerAttributes = "", string mapAttributes = "")
    {
        string source = Path.Combine(directory, "chunked.tmx");
        string attributes = encoding == "xml" ? "" : $" encoding='{encoding}'";
        if (compression.Length != 0) attributes += $" compression='{compression}'";
        File.WriteAllText(source, $"""
            <map orientation='{orientation}' infinite='1' width='0' height='0' tilewidth='{tileWidth}' tileheight='{tileHeight}' {mapAttributes}>
              <tileset firstgid='1' source='{(tileWidth == 2 ? "terrain.tsx" : "oversized.tsx")}'/>
              <layer width='0' height='0' {layerAttributes}><data{attributes}>{chunks}</data></layer>
            </map>
            """);
        return source;
    }

    /// <summary>Chunk payloads use the existing TMX decoders and retain original world positions.</summary>
    [Theory]
    [InlineData("xml", "")]
    [InlineData("csv", "")]
    [InlineData("base64", "")]
    [InlineData("base64", "zlib")]
    [InlineData("base64", "gzip")]
    public void ChunkedLayersBecomeFiniteScenesWithoutImplicitWrapping(string encoding, string compression)
    {
        Tileset();
        string chunks = $"<chunk x='-2' y='1' width='2' height='2'>{ChunkPayload(encoding, compression, 1, 0, 0, 0)}</chunk>" +
            $"<chunk x='2' y='-1' width='2' height='2'>{ChunkPayload(encoding, compression, 0, 0, 0, 2)}</chunk>";
        var request = new ExternalImportRequest(
            ChunkedMap(chunks, encoding, compression, layerAttributes: "offsetx='6' offsety='-4'"),
            Path.Combine(directory, "output"));
        var importer = new TiledMapImporter();
        var analysis = importer.Analyze(request);
        Assert.True(analysis.CanImport, string.Join(";", analysis.Diagnostics));
        Assert.False(Directory.Exists(request.OutputDirectory));

        var result = importer.Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "chunked.gscn"));
        Assert.Empty(SceneDefinitionValidator.Validate(scene));
        var layer = Assert.Single(scene.Layers);
        Assert.Equal(6, layer.Columns);
        Assert.Equal(4, layer.Rows);
        Assert.Equal(new Point(-2, 6), layer.OriginPx);
        Assert.False(layer.WrapHorizontally);
        Assert.False(layer.WrapVertically);
        Assert.Equal(2, layer.Tiles.Count);
        var animated = Assert.Single(layer.Tiles, tile => tile.X == 0 && tile.Y == 2);
        Assert.Equal("terrain.tile.0", animated.AnimationKey);
        Assert.True(animated.StartAnimation);
        Assert.Equal("terrain", animated.Frame!.Tilesheet);
        var staticTile = Assert.Single(layer.Tiles, tile => tile.X == 5 && tile.Y == 1);
        Assert.Equal(1, staticTile.Frame!.XTile);

        using var sheet = Gondwana.Drawing.Tilesheets.TilesheetRegistry.Instance
            .LoadFromDefinitionFile(Path.Combine(request.OutputDirectory, "chunked.gts"), "terrain");
        var cycles = AnimationDefinitionSerializer.LoadAllCycles(
            Path.Combine(request.OutputDirectory, "terrain-animations.gani"));
        try
        {
            using var runtime = SceneDefinitionSerializer.ToScene(scene);
            var loaded = Assert.Single(runtime.SceneLayers);
            Assert.Equal(new Rectangle(2, -2, 2, 2), loaded[0, 2]!.DrawLocationWorld);
            Assert.Equal(new Rectangle(12, -4, 2, 2), loaded[5, 1]!.DrawLocationWorld);
            Assert.DoesNotContain(layer.Tiles, tile => tile.X == 1 && tile.Y == 1);
        }
        finally { foreach (var cycle in cycles) cycle.Dispose(); }
    }

    /// <summary>Rhombic layers retain the projected positions of negative chunk coordinates.</summary>
    [Fact]
    public void ChunkedIsometricLayerPreservesWorldAnchors()
    {
        Tileset();
        var chunks = "<chunk x='-3' y='-2' width='2' height='2'>1,0,0,2</chunk>";
        var request = new ExternalImportRequest(
            ChunkedMap(chunks, orientation: "isometric"), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var layer = Assert.Single(SceneDefinitionSerializer.Load(
            Path.Combine(request.OutputDirectory, "chunked.gscn")).Layers);
        Assert.Equal(new Point(1, 5), layer.OriginPx);
        Assert.Equal(2, layer.Columns);
        Assert.Equal(2, layer.Rows);
        Assert.False(layer.WrapHorizontally || layer.WrapVertically);
        Assert.Contains(layer.Tiles, tile => tile.X == 0 && tile.Y == 0);
        Assert.Contains(layer.Tiles, tile => tile.X == 1 && tile.Y == 1);
    }

    /// <summary>Odd isometric dimensions use a blank column to preserve integer artwork anchors.</summary>
    [Fact]
    public void ChunkedOddIsometricCellsPadOriginInsteadOfMovingTiles()
    {
        OversizedTileset(3, 3);
        var chunks = "<chunk x='-2' y='1' width='1' height='1'>1</chunk>";
        var request = new ExternalImportRequest(
            ChunkedMap(chunks, orientation: "isometric", tileWidth: 3, tileHeight: 3),
            Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var layer = Assert.Single(SceneDefinitionSerializer.Load(
            Path.Combine(request.OutputDirectory, "chunked.gscn")).Layers);
        Assert.Equal(2, layer.Columns);
        Assert.Equal(1, layer.Rows);
        Assert.Equal(new Point(6, 3), layer.OriginPx);
        var tile = Assert.Single(layer.Tiles);
        Assert.Equal(1, tile.X);
        Assert.Equal(0, tile.Y);
        Assert.False(layer.WrapHorizontally || layer.WrapVertically);
        using var sheet = Gondwana.Drawing.Tilesheets.TilesheetRegistry.Instance
            .LoadFromDefinitionFile(Path.Combine(request.OutputDirectory, "chunked.gts"), "oversized");
        using var runtime = SceneDefinitionSerializer.ToScene(
            SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "chunked.gscn")));
        Assert.Equal(new Rectangle(-6, -2, 3, 3), runtime.SceneLayers[0][1, 0]!.DrawLocationWorld);
    }

    /// <summary>Each chunked layer has independent finite bounds and world-space origin.</summary>
    [Fact]
    public void MultipleChunkedLayersKeepIndependentBoundsAndOrder()
    {
        Tileset();
        var source = ChunkedMap("<chunk x='-1' y='-1' width='1' height='1'>1</chunk>");
        string xml = File.ReadAllText(source);
        xml = xml.Replace("</map>", """
            <group offsetx='4' offsety='2' visible='0'>
              <layer width='0' height='0'>
                <data encoding='csv'><chunk x='8' y='7' width='1' height='1'>2</chunk></data>
              </layer>
            </group>
            </map>
            """);
        File.WriteAllText(source, xml);
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "chunked.gscn"));
        Assert.Equal(2, scene.Layers.Count);
        Assert.Equal(new Point(2, 2), scene.Layers[0].OriginPx);
        Assert.Equal(new Point(-20, -16), scene.Layers[1].OriginPx);
        Assert.Equal(1, scene.Layers[1].Columns);
        Assert.Equal(1, scene.Layers[1].Rows);
        Assert.False(scene.Layers[1].Visible);
        Assert.Equal(new[] { 0, 1 }, scene.Layers.Select(l => l.ZOrder));
    }

    /// <summary>Empty infinite layers materialize as an empty, nonwrapping 1x1 grid.</summary>
    [Fact]
    public void EmptyChunkedLayerImportsWithoutInventingTiles()
    {
        Tileset();
        var request = new ExternalImportRequest(ChunkedMap(""), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var layer = Assert.Single(SceneDefinitionSerializer.Load(
            Path.Combine(request.OutputDirectory, "chunked.gscn")).Layers);
        Assert.Equal(1, layer.Columns);
        Assert.Equal(1, layer.Rows);
        Assert.Empty(layer.Tiles);
        Assert.False(layer.WrapHorizontally || layer.WrapVertically);
    }

    /// <summary>Invalid chunks fail before installing any generated GTS/GANI/GSCN outputs.</summary>
    [Theory]
    [InlineData("<chunk x='0' y='0' width='2' height='2'>1,0,0</chunk>", "length")]
    [InlineData("<chunk x='0' y='0' width='1' height='1'>1</chunk><chunk x='0' y='0' width='1' height='1'>2</chunk>", "Overlapping")]
    [InlineData("<chunk x='-10000000' y='0' width='1' height='1'>1</chunk><chunk x='10000000' y='0' width='1' height='1'>1</chunk>", "bounds exceed")]
    [InlineData("<chunk x='0' y='0' width='0' height='2'></chunk>", "sizes exceed")]
    [InlineData("<chunk x='2147483647' y='0' width='2' height='1'>1,1</chunk>", "integer grid range")]
    [InlineData("<chunk x='0' y='0' width='1' height='1'><tile gid='1'/></chunk><tile gid='1'/>", "cannot mix")]
    public void InvalidChunkStructuresRejectTransaction(string chunks, string diagnostic)
    {
        Tileset();
        var request = new ExternalImportRequest(ChunkedMap(chunks), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Message.Contains(diagnostic));
        Assert.Empty(result.WrittenFiles);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    /// <summary>Computes logical cell coordinates without relying on internal engine geometry APIs.</summary>
    private static Rectangle LogicalCellBounds(SceneLayer layer, string orientation, int x = 0, int y = 0)
    {
        int cellX = orientation == "isometric"
            ? (int)Math.Floor((x - y) * (layer.TileWidth / 2f)) - layer.OriginPx.X - layer.TileWidth / 2
            : x * layer.TileWidth - layer.OriginPx.X;
        int cellY = orientation == "isometric"
            ? (int)Math.Floor((x + y) * (layer.TileHeight / 2f)) - layer.OriginPx.Y
            : y * layer.TileHeight - layer.OriginPx.Y;
        return new Rectangle(cellX, cellY, layer.TileWidth, layer.TileHeight);
    }

    private string OversizedTileset(int frameWidth, int frameHeight, string attributes = "", string tileOffset = "")
    {
        using var bitmap = new SKBitmap(frameWidth, frameHeight);
        bitmap.Erase(SKColors.ForestGreen);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(directory, "oversized.png"), data.ToArray());
        string path = Path.Combine(directory, "oversized.tsx");
        File.WriteAllText(path, $"""
            <tileset name="oversized" tilewidth="{frameWidth}" tileheight="{frameHeight}" tilecount="1" columns="1" {attributes}>
              <image source="oversized.png" width="{frameWidth}" height="{frameHeight}"/>
              {tileOffset}
            </tileset>
            """);
        return path;
    }

    private string CollisionTileset(string objects, string groupAttributes = "", string tilesetAttributes = "")
    {
        string path = OversizedTileset(40, 40, attributes: tilesetAttributes);
        string source = File.ReadAllText(path);
        Assert.Contains("</tileset>", source);
        File.WriteAllText(path, source.Replace("</tileset>",
            $"<tile id='0'><objectgroup {groupAttributes}>{objects}</objectgroup></tile></tileset>"));
        return path;
    }

    private string OversizedMap(string orientation = "orthogonal", uint gid = 1)
    {
        string path = Path.Combine(directory, "oversized.tmx");
        File.WriteAllText(path, $"""
            <map orientation="{orientation}" width="2" height="2" tilewidth="32" tileheight="24">
              <tileset firstgid="1" source="oversized.tsx"/>
              <layer width="2" height="2"><data encoding="csv">{gid},0,0,{gid}</data></layer>
            </map>
            """);
        return path;
    }

    /// <summary>
    /// Tiled atlas artwork larger than the map grid becomes source-preserving GTS
    /// overhang. Orthogonal and isometric tile layers both anchor bottom-left.
    /// </summary>
    [Theory]
    [InlineData("orthogonal", 32, 32, 0, 8, 0)]
    [InlineData("orthogonal", 40, 40, 0, 16, 8)]
    [InlineData("isometric", 32, 32, 0, 8, 0)]
    [InlineData("isometric", 40, 40, 0, 16, 8)]
    public void MapImportsOversizedAtlasTilesAsOverhang(
        string orientation, int frameWidth, int frameHeight,
        int left, int top, int right)
    {
        OversizedTileset(frameWidth, frameHeight);
        var request = new ExternalImportRequest(OversizedMap(orientation), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        Assert.Contains(result.Analysis.Diagnostics, d => d.Code == "tiled.overhang");

        string gtsPath = Path.Combine(request.OutputDirectory, "oversized.gts");
        var gts = TilesheetDefinitionSerializer.Load(gtsPath);
        var region = Assert.Single(gts.Regions);
        Assert.Equal(new Size(frameWidth, frameHeight), region.TileSize);
        Assert.Equal(new Spacing(left, top, right, 0), region.Overhang);
        Assert.Empty(TilesheetDefinitionValidator.Validate(gts, frameWidth, frameHeight));

        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "oversized.gscn"));
        Assert.Empty(SceneDefinitionValidator.Validate(scene));
        Assert.Equal(new Size(32, 24),
            new Size(Assert.Single(scene.Layers).TileWidth, scene.Layers[0].TileHeight));

        using var sheet = Gondwana.Drawing.Tilesheets.TilesheetRegistry.Instance
            .LoadFromDefinitionFile(gtsPath, "oversized");
        using var runtime = SceneDefinitionSerializer.ToScene(scene);
        var runtimeLayer = Assert.Single(runtime.SceneLayers);
        var tile = runtimeLayer[0, 0]!;
        Rectangle cell = LogicalCellBounds(runtimeLayer, orientation);
        Rectangle visual = tile.DrawLocationWorld;
        Assert.Equal(cell.Left - left, visual.Left);
        Assert.Equal(cell.Top - top, visual.Top);
        Assert.Equal(cell.Right + right, visual.Right);
        Assert.Equal(cell.Bottom, visual.Bottom);
        Assert.Equal(new Size(frameWidth, frameHeight), visual.Size);
    }


    /// <summary>
    /// A grid-sized destination does not make diagonal flips of oversized source
    /// frames compatible with Tiled's bottom-left tile-layer anchoring.
    /// </summary>
    [Fact]
    public void MapRejectsDiagonalGridRenderedOversizedTile()
    {
        OversizedTileset(40, 40, "tilerendersize='grid'");
        var request = new ExternalImportRequest(
            OversizedMap(gid: 0x20000001u), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics,
            d => d.Message.Contains("transformed oversized tile cannot preserve Tiled artwork anchoring"));
        Assert.Empty(result.WrittenFiles);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    /// <summary>Safe horizontal flips remain supported with grid render sizing.</summary>
    [Fact]
    public void MapAllowsHorizontalFlipOfGridRenderedOversizedTile()
    {
        OversizedTileset(40, 40, "tilerendersize='grid'");
        var request = new ExternalImportRequest(
            OversizedMap(gid: 0x80000001u), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "oversized.gscn"));
        Assert.All(Assert.Single(scene.Layers).Tiles,
            tile => Assert.Equal(TileTransform.FlipHorizontal, tile.Transform));
    }

    /// <summary>
    /// Reject imagery reaching beyond the engine's fixed render-candidate fringe:
    /// a row-four tile can otherwise overlap a row-zero viewport without being queried.
    /// </summary>
    [Theory]
    [InlineData("orthogonal", 32, 128)]
    [InlineData("isometric", 32, 128)]
    [InlineData("isometric", 96, 24)]
    public void MapRejectsOverhangOutsideRuntimeCullingFringe(
        string orientation, int frameWidth, int frameHeight)
    {
        OversizedTileset(frameWidth, frameHeight);
        var request = new ExternalImportRequest(
            OversizedMap(orientation), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics,
            d => d.Message.Contains("overhang exceeds the runtime render-culling fringe"));
        Assert.Empty(result.WrittenFiles);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    /// <summary>Flipped oversized tiles are supported when the visual footprint stays anchored.</summary>
    [Theory]
    [InlineData("orthogonal")]
    [InlineData("isometric")]
    public void MapAllowsHorizontalFlipOfTopOnlyOverhang(string orientation)
    {
        OversizedTileset(32, 32);
        var request = new ExternalImportRequest(
            OversizedMap(orientation, 0x80000001u), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));

        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "oversized.gscn"));
        Assert.All(Assert.Single(scene.Layers).Tiles,
            tile => Assert.Equal(TileTransform.FlipHorizontal, tile.Transform));

        using var sheet = Gondwana.Drawing.Tilesheets.TilesheetRegistry.Instance
            .LoadFromDefinitionFile(Path.Combine(request.OutputDirectory, "oversized.gts"), "oversized");
        using var runtime = SceneDefinitionSerializer.ToScene(scene);
        var layer = Assert.Single(runtime.SceneLayers);
        var cell = LogicalCellBounds(layer, orientation);
        Assert.Equal(new Rectangle(cell.X, cell.Y - 8, 32, 32), layer[0, 0]!.DrawLocationWorld);
    }

    /// <summary>Reject flips which would rotate or shift oversized artwork relative to Tiled.</summary>
    [Theory]
    [InlineData("orthogonal", 40, 40, 0x80000001u)] // H flip relocates right overhang.
    [InlineData("orthogonal", 32, 32, 0x40000001u)] // V flip relocates top overhang.
    [InlineData("isometric", 32, 32, 0x20000001u)] // Diagonal flip swaps a rectangular cell's axes.
    public void MapRejectsFlippedOversizedTileWhoseAnchorWouldMove(
        string orientation, int width, int height, uint gid)
    {
        OversizedTileset(width, height);
        var request = new ExternalImportRequest(OversizedMap(orientation, gid), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics,
            d => d.Message.Contains("transformed oversized tile cannot preserve Tiled artwork anchoring"));
        Assert.Empty(result.WrittenFiles);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    /// <summary>Grid sizing does not relax the existing restriction on undersized source frames.</summary>
    [Fact]
    public void MapRejectsUndersizedSourceWhenTiledRenderSizeIsGrid()
    {
        OversizedTileset(16, 16, "tilerendersize='grid'");
        var request = new ExternalImportRequest(OversizedMap(), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics,
            d => d.Message.Contains("Tileset frame is smaller than the map cell"));
        Assert.Empty(result.WrittenFiles);
    }

    /// <summary>A Tiled grid-rendered tile fits the map cell even when the source frame is larger.</summary>
    [Fact]
    public void MapGridRenderSizeKeepsLargerSourceWithoutOverhang()
    {
        OversizedTileset(40, 40, "tilerendersize='grid'");
        var request = new ExternalImportRequest(OversizedMap(), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var gts = TilesheetDefinitionSerializer.Load(
            Path.Combine(request.OutputDirectory, "oversized.gts"));
        var region = Assert.Single(gts.Regions);
        Assert.Equal(new Size(40, 40), region.TileSize);
        Assert.Equal(Spacing.None, region.Overhang);
        Assert.DoesNotContain(result.Analysis.Diagnostics, d => d.Code == "tiled.overhang");
    }

    /// <summary>Smaller atlas frames cannot map to unsigned Gondwana overhang geometry.</summary>
    [Fact]
    public void MapRejectsFrameSmallerThanCellWithoutWriting()
    {
        OversizedTileset(16, 16);
        var request = new ExternalImportRequest(OversizedMap(), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d =>
            d.Message.Contains("negative overhang", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.WrittenFiles);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }


    /// <summary>Representable Tiled tile drawing offsets redistribute overhang across edges.</summary>
    [Theory]
    [InlineData("orthogonal", -3, 2, 3, 14, 5, 2)]
    [InlineData("isometric", 0, 6, 0, 10, 8, 6)]
    public void MapImportsRepresentableTileOffsetsAsOverhang(
        string orientation, int offsetX, int offsetY,
        int left, int top, int right, int bottom)
    {
        OversizedTileset(40, 40, tileOffset: $"<tileoffset x='{offsetX}' y='{offsetY}'/>");
        var request = new ExternalImportRequest(OversizedMap(orientation), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var gtsPath = Path.Combine(request.OutputDirectory, "oversized.gts");
        var gts = TilesheetDefinitionSerializer.Load(gtsPath);
        Assert.Equal(new Spacing(left, top, right, bottom), Assert.Single(gts.Regions).Overhang);
        Assert.Empty(TilesheetDefinitionValidator.Validate(gts, 40, 40));

        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "oversized.gscn"));
        using var sheet = Gondwana.Drawing.Tilesheets.TilesheetRegistry.Instance
            .LoadFromDefinitionFile(gtsPath, "oversized");
        using var runtime = SceneDefinitionSerializer.ToScene(scene);
        var layer = Assert.Single(runtime.SceneLayers);
        var tile = layer[0, 0]!;
        Rectangle cell = LogicalCellBounds(layer, orientation);
        Assert.Equal(
            new Rectangle(cell.Left + offsetX, cell.Bottom - 40 + offsetY, 40, 40),
            tile.DrawLocationWorld);
    }

    /// <summary>
    /// A valid nonnegative OH may still exceed the isometric candidate fringe
    /// when bottom extension combines with right-hand extension.
    /// </summary>
    [Fact]
    public void MapRejectsCombinedIsometricBottomAndRightOverhangBeyondFringe()
    {
        OversizedTileset(40, 40, tileOffset: "<tileoffset x='0' y='16'/>");
        var request = new ExternalImportRequest(OversizedMap("isometric"), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics,
            d => d.Message.Contains("overhang exceeds the runtime render-culling fringe"));
        Assert.Empty(result.WrittenFiles);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    /// <summary>Offsets that place artwork inside a map-cell edge cannot become nonnegative OH.</summary>
    [Fact]
    public void MapRejectsUnrepresentableTileOffset()
    {
        OversizedTileset(40, 40, tileOffset: "<tileoffset x='3' y='-2'/>");
        var request = new ExternalImportRequest(OversizedMap(), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d =>
            d.Message.Contains("cannot be represented"));
        Assert.Empty(result.WrittenFiles);
    }

    /// <summary>Standalone TSX cannot resolve drawing offsets without map cell dimensions.</summary>
    [Fact]
    public void StandaloneTsxRejectsNonzeroDrawingOffset()
    {
        string source = OversizedTileset(40, 40, tileOffset: "<tileoffset x='-3' y='2'/>");
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
        var result = new TiledTilesetImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d =>
            d.Message.Contains("tile offsets are unsupported"));
        Assert.Empty(result.WrittenFiles);
    }

    /// <summary>
    /// Verifies analysis does not write and import round trips geometry timing and paths.
    /// </summary>
    [Fact]
    public void AnalysisDoesNotWriteAndImportRoundTripsGeometryTimingAndPaths()
    {
        var importer = new TiledTilesetImporter();
        var request = new ExternalImportRequest(Tileset(), Path.Combine(directory, "output"));
        var analysis = importer.Analyze(request);
        Assert.True(analysis.CanImport, string.Join(";", analysis.Diagnostics));
        Assert.Equal(2, analysis.Artifacts.Count);
        Assert.False(Directory.Exists(request.OutputDirectory));
        Assert.Equal(2, importer.Import(request).WrittenFiles.Count);
        var gts = TilesheetDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "terrain.gts"));
        Assert.Equal("../atlas.png", gts.Image.FilePath);
        Assert.Equal(1, gts.Regions[0].RegionMargin.Left);
        Assert.Equal(0, gts.Regions[0].RegionMargin.Right);
        Assert.Equal(1, gts.Regions[0].TilePadding.Right);
        Assert.Equal((2L, 1L), TilesheetDefinitionValidator.GridSize(gts.Regions[0]));
        Assert.Empty(TilesheetDefinitionValidator.Validate(gts, 7, 4));
        var gani = AnimationDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "terrain-animations.gani"));
        Assert.Equal(new double?[] { 0.1, 0.25 }, gani.Frames.Select(f => f.DurationSeconds));
        Assert.Equal(new[] { 1, 0 }, gani.Frames.Select(f => f.XTile));
        Assert.Equal("terrain.gts", gani.TilesheetSources[0].GtsPath);
        Assert.Empty(AnimationDefinitionValidator.Validate(gani));
        Assert.False(importer.Analyze(request).CanImport);
        Assert.Empty(importer.Import(request).WrittenFiles);
        Assert.Equal(2, importer.Import(request with { Overwrite = true }).WrittenFiles.Count);
    }

    /// <summary>Multiple tile animations share a document while map sources retain each logical selector.</summary>
    [Fact]
    public void MultipleTileAnimationsShareOneDocument()
    {
        string tileset = Tileset();
        File.WriteAllText(tileset, File.ReadAllText(tileset).Replace("</tileset>",
            "<tile id='1'><animation><frame tileid='0' duration='100'/><frame tileid='1' duration='200'/></animation></tile></tileset>"));
        string map = Path.Combine(directory, "level.tmx");
        File.WriteAllText(map, "<map orientation='orthogonal' width='2' height='1' tilewidth='2' tileheight='2'><tileset firstgid='1' source='terrain.tsx'/><layer><data encoding='csv'>1,2</data></layer></map>");
        var request = new ExternalImportRequest(map, Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var gtsArtifact = Assert.Single(result.Analysis.Artifacts, item => item.Kind == "GTS");
        Assert.Equal("level.gts", Path.GetFileName(gtsArtifact.OutputPath));
        Assert.Equal(new[] { "terrain" }, gtsArtifact.LogicalKeys);
        var artifact = Assert.Single(result.Analysis.Artifacts, item => item.Kind == "GANI");
        Assert.Equal(new[] { "terrain.tile.0", "terrain.tile.1" }, artifact.LogicalKeys);
        Assert.Equal(2, AnimationDefinitionSerializer.LoadAll(artifact.OutputPath).Count);
        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "level.gscn"));
        Assert.Equal(2, scene.AnimationSources.Count);
        Assert.All(scene.AnimationSources, source =>
        {
            Assert.Equal("terrain-animations.gani", source.GaniPath);
            var animation = AnimationDefinitionSerializer.Load(artifact.OutputPath, source.AnimationKey);
            Assert.Equal(source.AnimationKey, animation.Key);
            Assert.Equal("level.gts", Assert.Single(animation.TilesheetSources).GtsPath);
        });
        Assert.Equal("level.gts", Assert.Single(scene.TilesheetSources).GtsPath);
    }

    /// <summary>Legacy per-tileset GTS files block TMX re-import until manually removed.</summary>
    [Fact]
    public void MapReimportRequiresCleanupOfLegacyTilesetGts()
    {
        string tileset = Tileset();
        string map = Path.Combine(directory, "level.tmx");
        File.WriteAllText(map, """
            <map orientation="orthogonal" width="1" height="1" tilewidth="2" tileheight="2">
              <tileset firstgid="1" source="terrain.tsx"/>
              <layer><data encoding="csv">1</data></layer>
            </map>
            """);
        string output = Path.Combine(directory, "output");
        Assert.True(new TiledTilesetImporter().Import(new(tileset, output)).Analysis.CanImport);
        string legacyGts = Path.Combine(output, "terrain.gts");
        string legacyGani = Path.Combine(output, "terrain-animations.gani");
        byte[] originalGts = File.ReadAllBytes(legacyGts);
        byte[] originalGani = File.ReadAllBytes(legacyGani);

        var importer = new TiledMapImporter();
        var blocked = importer.Import(new ExternalImportRequest(map, output) { Overwrite = true });
        Assert.False(blocked.Analysis.CanImport);
        Assert.Contains(blocked.Analysis.Diagnostics,
            diagnostic => diagnostic.Code == "key.exists" && diagnostic.Message.Contains("terrain.gts"));
        Assert.Empty(blocked.WrittenFiles);
        Assert.Equal(originalGts, File.ReadAllBytes(legacyGts));
        Assert.Equal(originalGani, File.ReadAllBytes(legacyGani));
        Assert.False(File.Exists(Path.Combine(output, "level.gscn")));

        File.Delete(legacyGts);
        var migrated = importer.Import(new ExternalImportRequest(map, output) { Overwrite = true });
        Assert.True(migrated.Analysis.CanImport, string.Join(";", migrated.Analysis.Diagnostics));
        Assert.Equal("level.gts", Assert.Single(SceneDefinitionSerializer.Load(
            Path.Combine(output, "level.gscn")).TilesheetSources).GtsPath);
        Assert.Equal("level.gts", Assert.Single(AnimationDefinitionSerializer.Load(legacyGani).TilesheetSources).GtsPath);
    }

    /// <summary>Tiled's single tile-local rectangle becomes a blocking GTS frame with exact signed insets.</summary>
    [Theory]
    [InlineData(5, 6, 20, 15, 6, 19, 5, 15)]
    [InlineData(-2, -3, 44, 46, -3, -3, -2, -2)]
    public void TiledTileRectangleBecomesFrameCollisionAdjust(
        int x, int y, int width, int height, int top, int bottom, int left, int right)
    {
        string source = CollisionTileset(
            $"<object id='1' x='{x}' y='{y}' width='{width}' height='{height}'/>");
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
        var importer = new TiledTilesetImporter();
        var analysis = importer.Analyze(request);
        Assert.True(analysis.CanImport, string.Join(";", analysis.Diagnostics));
        Assert.DoesNotContain(analysis.Diagnostics, d => d.Code == "tiled.metadata" &&
            d.Message.Contains("objectgroup", StringComparison.OrdinalIgnoreCase));
        Assert.False(Directory.Exists(request.OutputDirectory));
        var result = importer.Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var gts = TilesheetDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "oversized.gts"));
        Assert.Empty(TilesheetDefinitionValidator.Validate(gts, 40, 40));
        var region = Assert.Single(gts.Regions);
        var frame = Assert.Single(region.Frames);
        Assert.Equal(0, frame.XTile);
        Assert.Equal(0, frame.YTile);
        Assert.Equal(TileCollisionType.Blocking, frame.CollisionType);
        Assert.Equal(new CollisionAdjust(top, bottom, left, right), frame.CollisionAdjust);
        using var sheet = Gondwana.Drawing.Tilesheets.TilesheetRegistry.Instance
            .LoadFromDefinitionFile(Path.Combine(request.OutputDirectory, "oversized.gts"), "oversized");
        var runtimeFrame = sheet.GetFrame(0, 0);
        Assert.Equal(TileCollisionType.Blocking, runtimeFrame.CollisionType);
        Assert.Equal(new Rectangle(x, y, width, height), runtimeFrame.CollisionArea);
    }

    /// <summary>A tile's authored rectangle survives the map-to-GSCN path and runtime collision activation.</summary>
    [Fact]
    public void TiledMapRetainsRectangleCollisionOnPlacedTileWithOverhang()
    {
        CollisionTileset("<object id='1' x='5' y='6' width='20' height='15'/>");
        var request = new ExternalImportRequest(OversizedMap(), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var gtsPath = Path.Combine(request.OutputDirectory, "oversized.gts");
        var region = Assert.Single(TilesheetDefinitionSerializer.Load(gtsPath).Regions);
        Assert.Equal(new Spacing(0, 16, 8, 0), region.Overhang);
        Assert.Equal(TileCollisionType.Blocking, Assert.Single(region.Frames).CollisionType);
        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "oversized.gscn"));
        var nativeTile = Assert.Single(scene.Layers[0].Tiles.Where(t => t.X == 0 && t.Y == 0));
        Assert.Equal(TileCollisionType.Blocking, nativeTile.CollisionType);
        Assert.Equal(new CollisionAdjust(6, 19, 5, 15), nativeTile.AdjustCollisionArea);
        Assert.False(nativeTile.AdjustCollisionAreaByFrame);
        Assert.False(nativeTile.CollisionTypeByFrame);
        using var sheet = Gondwana.Drawing.Tilesheets.TilesheetRegistry.Instance
            .LoadFromDefinitionFile(gtsPath, "oversized");
        using var runtime = SceneDefinitionSerializer.ToScene(scene);
        var tile = runtime.SceneLayers[0][0, 0]!;
        Assert.True(tile.CollisionsEnabled);
        Assert.Equal(TileCollisionType.Blocking, tile.CollisionType);
        Assert.Equal(new Rectangle(0, -16, 40, 40), tile.DrawLocationWorld);
        Assert.Equal(new Rectangle(5, -10, 20, 15), tile.CollisionArea);
    }

    /// <summary>Native tile transforms also transform the rectangle collision metadata.</summary>
    [Fact]
    public void FlippedTiledTileMirrorsCollisionRectangleInGscn()
    {
        CollisionTileset("<object id='1' x='5' y='6' width='20' height='15'/>");
        string source = Path.Combine(directory, "flipped.tmx");
        File.WriteAllText(source, """
            <map orientation='orthogonal' width='1' height='1' tilewidth='40' tileheight='40'>
              <tileset firstgid='1' source='oversized.tsx'/>
              <layer width='1' height='1'><data encoding='csv'>2147483649</data></layer>
            </map>
            """);
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "flipped.gscn"));
        using var sheet = Gondwana.Drawing.Tilesheets.TilesheetRegistry.Instance
            .LoadFromDefinitionFile(Path.Combine(request.OutputDirectory, "flipped.gts"), "oversized");
        using var runtime = SceneDefinitionSerializer.ToScene(scene);
        var tile = runtime.SceneLayers[0][0, 0]!;
        Assert.True(tile.CollisionsEnabled);
        Assert.Equal(new Rectangle(15, 6, 20, 15), tile.CollisionArea);
    }

    /// <summary>Animated TMX tiles retain collisions attached to their logical source tile ID.</summary>
    [Fact]
    public void AnimatedTiledTileRetainsOwningTileCollisionAcrossInitialFrame()
    {
        string tsx = Tileset();
        File.WriteAllText(tsx, File.ReadAllText(tsx).Replace("<tile id=\"0\">",
            "<tile id=\"0\"><objectgroup><object id='1' x='1' y='0' width='1' height='2'/></objectgroup>"));
        string source = Path.Combine(directory, "animated-collision.tmx");
        File.WriteAllText(source, """
            <map orientation='orthogonal' width='1' height='1' tilewidth='2' tileheight='2'>
              <tileset firstgid='1' source='terrain.tsx'/>
              <layer width='1' height='1'><data encoding='csv'>1</data></layer>
            </map>
            """);
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var gts = TilesheetDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "animated-collision.gts"));
        var frame = Assert.Single(Assert.Single(gts.Regions).Frames);
        Assert.Equal(0, frame.XTile);
        Assert.Equal(TileCollisionType.Blocking, frame.CollisionType);
        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "animated-collision.gscn"));
        var tile = Assert.Single(Assert.Single(scene.Layers).Tiles);
        Assert.True(tile.StartAnimation);
        Assert.Equal(1, tile.Frame!.XTile);
        Assert.Equal(TileCollisionType.Blocking, tile.CollisionType);
        Assert.Equal(new CollisionAdjust(0, 0, 1, 0), tile.AdjustCollisionArea);
        using var sheet = Gondwana.Drawing.Tilesheets.TilesheetRegistry.Instance
            .LoadFromDefinitionFile(Path.Combine(request.OutputDirectory, "animated-collision.gts"), "terrain");
        var cycles = AnimationDefinitionSerializer.LoadAllCycles(
            Path.Combine(request.OutputDirectory, "terrain-animations.gani"));
        try
        {
            using var runtime = SceneDefinitionSerializer.ToScene(scene);
            var placed = runtime.SceneLayers[0][0, 0]!;
            Assert.True(placed.CollisionsEnabled);
            Assert.Equal(new Rectangle(1, 0, 1, 2), placed.CollisionArea);
        }
        finally { foreach (var cycle in cycles) cycle.Dispose(); }
    }

    /// <summary>Unrepresentable shapes produce explicit warnings, never partial collision geometry.</summary>
    [Theory]
    [InlineData("<object id='1' x='1' y='1' width='4' height='4'/><object id='2' x='2' y='2' width='5' height='5'/>", "one rectangle")]
    [InlineData("<object id='1' x='1' y='1' width='4' height='4' rotation='30'/>", "rotated")]
    [InlineData("<object id='1' x='1' y='1' width='4' height='4'><ellipse/></object>", "non-rectangular")]
    [InlineData("<object id='1' x='1' y='1' width='4' height='4'><polygon points='0,0 4,0 4,4'/></object>", "non-rectangular")]
    [InlineData("<object id='1' x='1.5' y='1' width='4' height='4'/>", "integral")]
    [InlineData("<object id='1' x='1' y='1' width='0' height='4'/>", "positive")]
    [InlineData("<object id='1' x='1' y='1' width='4' height='4'><properties><property name='trigger' type='bool' value='true'/></properties></object>", "custom")]
    public void UnsupportedTileCollisionsAreWarnedAndOmitted(string objects, string reason)
    {
        var source = CollisionTileset(objects);
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
        var result = new TiledTilesetImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        Assert.Contains(result.Analysis.Diagnostics, d =>
            d.Severity == ExternalImportSeverity.Warning &&
            d.Code == "tiled.collision.unsupported" && d.Message.Contains(reason));
        var gts = TilesheetDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "oversized.gts"));
        Assert.Empty(Assert.Single(gts.Regions).Frames);
    }

    /// <summary>Scaled grid rendering cannot safely preserve tile-local collision pixels.</summary>
    [Fact]
    public void GridRenderedCollisionRectangleWithDifferentDrawSizeWarns()
    {
        CollisionTileset("<object id='1' x='5' y='6' width='20' height='15'/>",
            tilesetAttributes: "tilerendersize='grid'");
        var request = new ExternalImportRequest(OversizedMap(), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        Assert.Contains(result.Analysis.Diagnostics, d =>
            d.Code == "tiled.collision.unsupported" && d.Message.Contains("scaling"));
        var gts = TilesheetDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "oversized.gts"));
        Assert.Empty(Assert.Single(gts.Regions).Frames);
        Assert.All(Assert.Single(SceneDefinitionSerializer.Load(
            Path.Combine(request.OutputDirectory, "oversized.gscn")).Layers).Tiles,
            tile => Assert.Equal(TileCollisionType.None, tile.CollisionType));
    }

    /// <summary>Invalid numeric collision metadata fails analysis before writing output.</summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void NonFiniteCollisionCoordinatesBlockImportWithoutWrites(string x)
    {
        string source = CollisionTileset($"<object id='1' x='{x}' y='0' width='4' height='4'/>");
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
        var result = new TiledTilesetImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Empty(result.WrittenFiles);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    /// <summary>Embedded image bytes are normalized into a PNG dependency for standalone TSX.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("zlib")]
    [InlineData("gzip")]
    public void StandaloneTilesetImportsEmbeddedAtlasWithoutExternalDependency(string compression)
    {
        string tsx = Tileset();
        EmbedAtlasInTileset(tsx, compression);
        File.Delete(Path.Combine(directory, "atlas.png"));
        var importer = new TiledTilesetImporter();
        var request = new ExternalImportRequest(tsx, Path.Combine(directory, "output"));
        var analysis = importer.Analyze(request);
        Assert.True(analysis.CanImport, string.Join(";", analysis.Diagnostics));
        Assert.Equal(3, analysis.Artifacts.Count);
        Assert.DoesNotContain(analysis.Dependencies, p => p.EndsWith("atlas.png", StringComparison.Ordinal));
        Assert.False(Directory.Exists(request.OutputDirectory));
        var result = importer.Import(request);
        Assert.Equal(3, result.WrittenFiles.Count);
        string imagePath = Path.Combine(request.OutputDirectory, "terrain-atlas.png");
        using (var atlas = SKBitmap.Decode(imagePath))
        {
            Assert.Equal(7, atlas.Width);
            Assert.Equal(4, atlas.Height);
            Assert.Equal(SKColors.Red, atlas.GetPixel(0, 0));
        }
        var gts = TilesheetDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "terrain.gts"));
        Assert.Equal("terrain-atlas.png", gts.Image.FilePath);
        Assert.Empty(TilesheetDefinitionValidator.Validate(gts, 7, 4));
        var gani = AnimationDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "terrain-animations.gani"));
        Assert.Equal("terrain.gts", Assert.Single(gani.TilesheetSources).GtsPath);
        byte[] original = File.ReadAllBytes(imagePath);
        Assert.True(importer.Import(request with { Overwrite = true }).Analysis.CanImport);
        Assert.Equal(original, File.ReadAllBytes(imagePath));
    }

    /// <summary>TMX inline atlas data retains grouped GTS/GANI/GSCN references.</summary>
    [Fact]
    public void MapImportsEmbeddedAtlasAndSceneReferences()
    {
        string tsx = Tileset();
        EmbedAtlasInTileset(tsx);
        string inline = File.ReadAllText(tsx).Replace("<tileset name=", "<tileset firstgid=\"1\" name=");
        string tmx = Path.Combine(directory, "world.tmx");
        File.WriteAllText(tmx, $"<map orientation='orthogonal' width='1' height='1' tilewidth='2' tileheight='2'>{inline}<layer width='1' height='1'><data encoding='csv'>1</data></layer></map>");
        File.Delete(Path.Combine(directory, "atlas.png"));
        var request = new ExternalImportRequest(tmx, Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        string imagePath = Path.Combine(request.OutputDirectory, "world-tileset-1-atlas.png");
        Assert.True(File.Exists(imagePath));
        using (var atlas = SKBitmap.Decode(imagePath))
            Assert.Equal(SKColors.Red, atlas.GetPixel(0, 0));
        var gts = TilesheetDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "world.gts"), "terrain");
        Assert.Equal("world-tileset-1-atlas.png", gts.Image.FilePath);
        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "world.gscn"));
        Assert.Empty(SceneDefinitionValidator.Validate(scene));
        Assert.Equal("world.gts", Assert.Single(scene.TilesheetSources).GtsPath);
        Assert.True(Assert.Single(Assert.Single(scene.Layers).Tiles).StartAnimation);
        var animation = AnimationDefinitionSerializer.Load(
            Path.Combine(request.OutputDirectory, "world-tileset-1-animations.gani"), "terrain.tile.0");
        Assert.Equal("world.gts", Assert.Single(animation.TilesheetSources).GtsPath);
        Assert.Equal("world-tileset-1-animations.gani", Assert.Single(scene.AnimationSources).GaniPath);
    }

    /// <summary>Malformed or ambiguous embedded imagery blocks the transaction.</summary>
    [Theory]
    [InlineData("encoding=\"base64\"", "encoding=\"csv\"", "base64")]
    [InlineData("encoding=\"base64\"", "encoding=\"base64\" compression=\"zstd\"", "compression")]
    [InlineData("format=\"png\"", "format=\"svg\"", "format")]
    public void UnsupportedEmbeddedAtlasAttributesRejectWithoutWrites(string oldValue, string newValue, string error)
    {
        string tsx = Tileset();
        EmbedAtlasInTileset(tsx);
        File.WriteAllText(tsx, File.ReadAllText(tsx).Replace(oldValue, newValue));
        var request = new ExternalImportRequest(tsx, Path.Combine(directory, "output"));
        var result = new TiledTilesetImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Message.Contains(error));
        Assert.Empty(result.WrittenFiles);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    /// <summary>Image-source and embedded-data attributes must not coexist.</summary>
    [Fact]
    public void EmbeddedAtlasRejectsImageSourceAlongsideData()
    {
        string tsx = Tileset();
        EmbedAtlasInTileset(tsx);
        var xml = System.Xml.Linq.XDocument.Load(tsx);
        xml.Root!.Element("image")!.SetAttributeValue("source", "atlas.png");
        xml.Save(tsx);
        var request = new ExternalImportRequest(tsx, Path.Combine(directory, "output"));
        var result = new TiledTilesetImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d =>
            d.Message.Contains("either a source path or embedded data"));
        Assert.Empty(result.WrittenFiles);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    /// <summary>Ambiguous multiple data payloads are never silently accepted.</summary>
    [Fact]
    public void EmbeddedAtlasRejectsMultipleDataElements()
    {
        string tsx = Tileset();
        EmbedAtlasInTileset(tsx);
        var xml = System.Xml.Linq.XDocument.Load(tsx);
        var image = xml.Root!.Element("image")!;
        image.Add(new System.Xml.Linq.XElement("data", new System.Xml.Linq.XAttribute("encoding", "base64"),
            Convert.ToBase64String([1, 2, 3, 4])));
        xml.Save(tsx);
        var request = new ExternalImportRequest(tsx, Path.Combine(directory, "output"));
        var result = new TiledTilesetImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Message.Contains("only one data element"));
        Assert.Empty(result.WrittenFiles);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    /// <summary>Oversized encoded dimensions are refused before bitmap allocation.</summary>
    [Fact]
    public void EmbeddedAtlasRejectsOversizedPngDimensionsBeforeDecode()
    {
        string tsx = Tileset();
        EmbedAtlasInTileset(tsx);
        var xml = System.Xml.Linq.XDocument.Load(tsx);
        var data = xml.Root!.Element("image")!.Element("data")!;
        byte[] png = Convert.FromBase64String(data.Value);
        // PNG IHDR: 4-byte width, 4-byte height and a CRC over type + 13-byte payload.
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16, 4), 9000);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20, 4), 9000);
        uint crc = 0xffffffff;
        foreach (byte value in png.AsSpan(12, 17))
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(29, 4), ~crc);
        data.Value = Convert.ToBase64String(png);
        xml.Save(tsx);
        var request = new ExternalImportRequest(tsx, Path.Combine(directory, "output"));
        var result = new TiledTilesetImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d =>
            d.Message.Contains("64-million-pixel limit"));
        Assert.Empty(result.WrittenFiles);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    /// <summary>Corrupted base64 image bytes produce a source diagnostic without any output.</summary>
    [Fact]
    public void CorruptEmbeddedAtlasDataDoesNotWriteFiles()
    {
        string tsx = Tileset();
        EmbedAtlasInTileset(tsx);
        var xml = System.Xml.Linq.XDocument.Load(tsx);
        xml.Root!.Element("image")!.Element("data")!.Value = "this-is-not-base64";
        xml.Save(tsx);
        var request = new ExternalImportRequest(tsx, Path.Combine(directory, "output"));
        var result = new TiledTilesetImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Code == "source.invalid");
        Assert.Empty(result.WrittenFiles);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    /// <summary>
    /// Verifies collection of images produces diagnostic without writes.
    /// </summary>
    [Fact]
    public void CollectionOfImagesProducesDiagnosticWithoutWrites()
    {
        string source = Path.Combine(directory, "collection.tsx");
        File.WriteAllText(source, "<tileset><tile id='0'><image source='a.png'/></tile></tileset>");
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
        var result = new TiledTilesetImporter().Import(request);
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Message.Contains("Collection-of-images"));
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    /// <summary>
    /// Verifies empty animation is diagnosed and repeated imports are deterministic.
    /// </summary>
    [Fact]
    public void EmptyAnimationIsDiagnosedAndRepeatedImportsAreDeterministic()
    {
        string source = Tileset();
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
        var importer = new TiledTilesetImporter();
        var first = importer.Import(request).WrittenFiles.ToDictionary(p => p, File.ReadAllBytes);
        importer.Import(request with { Overwrite = true });
        foreach (var (path, bytes) in first) Assert.Equal(bytes, File.ReadAllBytes(path));
        File.WriteAllText(source, File.ReadAllText(source).Replace("<frame tileid=\"1\" duration=\"100\"/><frame tileid=\"0\" duration=\"250\"/>", ""));
        var result = importer.Import(request with { Overwrite = true });
        Assert.False(result.Analysis.CanImport); Assert.Empty(result.WrittenFiles);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Message.Contains("animation", StringComparison.OrdinalIgnoreCase));
        foreach (var (path, bytes) in first) Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    /// <summary>
    /// Verifies map imports mixed tilesets empty cells and animation.
    /// </summary>
    /// <param name="orientation">The orientation value for this test case.</param>
    [Theory]
    [InlineData("orthogonal")]
    [InlineData("isometric")]
    public void MapImportsMixedTilesetsEmptyCellsAndAnimation(string orientation)
    {
        Tileset();
        File.WriteAllText(Path.Combine(directory, "second.tsx"), File.ReadAllText(Path.Combine(directory, "terrain.tsx")).Replace("name=\"terrain\"", "name=\"second\""));
        var source = Path.Combine(directory, "level.tmx");
        File.WriteAllText(source, $"""
            <map orientation="{orientation}" width="3" height="1" tilewidth="2" tileheight="2">
              <tileset firstgid="1" source="terrain.tsx"/><tileset firstgid="5" source="second.tsx"/>
              <group visible="0" offsetx="4"><layer width="3" height="1"><data encoding="csv">1,0,6</data></layer></group>
              <objectgroup name="objects"/>
            </map>
            """);
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var gtsArtifact = Assert.Single(result.Analysis.Artifacts, item => item.Kind == "GTS");
        Assert.Equal("level.gts", Path.GetFileName(gtsArtifact.OutputPath));
        Assert.Equal(new[] { "terrain", "second" }, gtsArtifact.LogicalKeys);
        Assert.DoesNotContain(result.WrittenFiles, f => Path.GetFileName(f) is "terrain.gts" or "second.gts");
        Assert.Equal(new[] { "terrain", "second" },
            TilesheetDefinitionSerializer.LoadAll(gtsArtifact.OutputPath).Select(d => d.Name));
        Assert.Equal("../atlas.png", TilesheetDefinitionSerializer.Load(gtsArtifact.OutputPath, "terrain").Image.FilePath);
        Assert.Equal("../atlas.png", TilesheetDefinitionSerializer.Load(gtsArtifact.OutputPath, "second").Image.FilePath);
        var scene = Gondwana.Scenes.GSCN.SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "level.gscn"));
        Assert.Empty(Gondwana.Scenes.GSCN.SceneDefinitionValidator.Validate(scene));
        Assert.Equal(new[] { "terrain", "second" }, scene.TilesheetSources.Select(source => source.Tilesheet));
        Assert.All(scene.TilesheetSources, source => Assert.Equal("level.gts", source.GtsPath));
        foreach (var animationSource in scene.AnimationSources)
        {
            var animation = AnimationDefinitionSerializer.Load(
                Path.Combine(request.OutputDirectory, animationSource.GaniPath!), animationSource.AnimationKey);
            Assert.Equal("level.gts", Assert.Single(animation.TilesheetSources).GtsPath);
        }
        var layer = Assert.Single(scene.Layers);
        Assert.Equal(2, layer.Tiles.Count);
        Assert.False(layer.Visible);
        Assert.Equal(orientation == "orthogonal" ? -4 : -5, layer.OriginPx.X);
        Assert.Equal("terrain", layer.Tiles[0].Frame!.Tilesheet);
        Assert.Equal("second", layer.Tiles[1].Frame!.Tilesheet);
        Assert.True(layer.Tiles[0].StartAnimation);
        Assert.Equal(1, layer.Tiles[0].Frame!.XTile);
        Assert.Equal(2, layer.Tiles[1].X);
        Assert.Equal(2, scene.AnimationSources.Count);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Code == "tiled.layer.unsupported");
    }

    /// <summary>
    /// Verifies a TMX imports external and inline tilesets into one GTS while retaining
    /// separate logical identities, image sources and animation references.
    /// </summary>
    [Fact]
    public void MapGroupsExternalAndInlineTilesetsWithoutChangingStandaloneTsx()
    {
        string standaloneTileset = Tileset();
        string source = Path.Combine(directory, "mixed.tmx");
        File.WriteAllText(source, """
            <map orientation="orthogonal" width="2" height="1" tilewidth="2" tileheight="2">
              <tileset firstgid="1" source="terrain.tsx"/>
              <tileset firstgid="5" name="decor" tilewidth="2" tileheight="2" tilecount="2" columns="2" margin="1" spacing="1">
                <image source="atlas.png" width="7" height="4"/>
                <tile id="0">
                  <animation><frame tileid="1" duration="175"/><frame tileid="0" duration="225"/></animation>
                </tile>
              </tileset>
              <layer width="2" height="1"><data encoding="csv">1,5</data></layer>
            </map>
            """);
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var gtsArtifact = Assert.Single(result.Analysis.Artifacts, artifact => artifact.Kind == "GTS");
        Assert.Equal("mixed.gts", Path.GetFileName(gtsArtifact.OutputPath));
        Assert.Equal(new[] { "terrain", "decor" }, gtsArtifact.LogicalKeys);
        Assert.Equal(2, TilesheetDefinitionSerializer.LoadAll(gtsArtifact.OutputPath).Count);

        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "mixed.gscn"));
        Assert.Empty(SceneDefinitionValidator.Validate(scene));
        Assert.Equal(new[] { "terrain", "decor" }, scene.TilesheetSources.Select(x => x.Tilesheet));
        Assert.All(scene.TilesheetSources, x => Assert.Equal("mixed.gts", x.GtsPath));
        Assert.Equal(new[] { "terrain", "decor" },
            scene.Layers.Single().Tiles.Select(tile => tile.Frame!.Tilesheet));
        Assert.All(scene.Layers.Single().Tiles, tile => Assert.True(tile.StartAnimation));
        Assert.Equal(2, scene.AnimationSources.Count);
        foreach (var animationSource in scene.AnimationSources)
        {
            var animation = AnimationDefinitionSerializer.Load(
                Path.Combine(request.OutputDirectory, animationSource.GaniPath!), animationSource.AnimationKey);
            Assert.Equal("mixed.gts", Assert.Single(animation.TilesheetSources).GtsPath);
            Assert.All(animation.Frames, frame => Assert.Equal(
                animationSource.AnimationKey.Split(".tile.")[0], frame.Tilesheet));
        }

        // Importing the original TSX independently still produces its own single-member GTS.
        var standaloneRequest = new ExternalImportRequest(standaloneTileset, Path.Combine(directory, "tsx-output"));
        var tsxResult = new TiledTilesetImporter().Import(standaloneRequest);
        Assert.True(tsxResult.Analysis.CanImport);
        Assert.Equal("terrain.gts", Path.GetFileName(
            Assert.Single(tsxResult.Analysis.Artifacts, artifact => artifact.Kind == "GTS").OutputPath));
        Assert.Equal("terrain.gts", Assert.Single(AnimationDefinitionSerializer.Load(
            Path.Combine(standaloneRequest.OutputDirectory, "terrain-animations.gani")).TilesheetSources).GtsPath);
    }

    /// <summary>
    /// Verifies decodes base64 layer.
    /// </summary>
    /// <param name="compression">The compression value for this test case.</param>
    [Theory]
    [InlineData("")]
    [InlineData("zlib")]
    [InlineData("gzip")]
    public void DecodesBase64Layer(string compression)
    {
        byte[] raw = [1, 0, 0, 0, 0, 0, 0, 0, 6, 0, 0, 0];
        using var memory = new MemoryStream();
        if (compression.Length == 0) memory.Write(raw);
        else
        {
            using Stream compressed = compression == "zlib"
                ? new System.IO.Compression.ZLibStream(memory, System.IO.Compression.CompressionLevel.Optimal, true)
                : new System.IO.Compression.GZipStream(memory, System.IO.Compression.CompressionLevel.Optimal, true);
            compressed.Write(raw);
        }
        var data = System.Xml.Linq.XElement.Parse($"<data encoding='base64' compression='{compression}'>{Convert.ToBase64String(memory.ToArray())}</data>");
        Assert.Equal(new uint[] { 1, 0, 6 }, TiledMapImporter.DecodeLayer(data, 3));
    }

    /// <summary>
    /// Verifies xml data and transform diagnostics.
    /// </summary>
    [Fact]
    public void XmlDataAndTransformDiagnostics()
    {
        Assert.Equal(new uint[] { 1, 0 }, TiledMapImporter.DecodeLayer(System.Xml.Linq.XElement.Parse("<data><tile gid='1'/><tile gid='0'/></data>"), 2));
        Tileset();
        string source = Path.Combine(directory, "flipped.tmx");
        File.WriteAllText(source, "<map orientation='orthogonal' width='1' height='1' tilewidth='2' tileheight='2'><tileset firstgid='1' source='terrain.tsx'/><layer><data encoding='csv'>268435457</data></layer></map>");
        var result = new TiledMapImporter().Import(new(source, Path.Combine(directory, "output")));
        Assert.True(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Code == "tiled.transform.hex" && d.Severity == ExternalImportSeverity.Warning);
    }
    /// <summary>
    /// Verifies all eight gid combinations import and round trip.
    /// </summary>
    /// <param name="orientation">The orientation value for this test case.</param>
    [Theory]
    [InlineData("orthogonal")]
    [InlineData("isometric")]
    public void AllEightGidCombinationsImportAndRoundTrip(string orientation)
    {
        Tileset();
        uint[] flags = [0, 0x80000000, 0x40000000, 0xc0000000, 0x20000000, 0xa0000000, 0x60000000, 0xe0000000];
        TileTransform[] expected = [TileTransform.Identity, TileTransform.FlipHorizontal, TileTransform.FlipVertical, TileTransform.Rotate180,
            TileTransform.FlipDiagonal, TileTransform.Rotate90, TileTransform.Rotate270, TileTransform.FlipAntiDiagonal];
        string source = Path.Combine(directory, "transforms.tmx");
        File.WriteAllText(source, $"""
            <map orientation="{orientation}" width="8" height="1" tilewidth="2" tileheight="2">
              <tileset firstgid="1" source="terrain.tsx"/>
              <layer><data encoding="csv">{string.Join(",", flags.Select(flag => flag | 2u))}</data></layer>
            </map>
            """);
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        Assert.DoesNotContain(result.Analysis.Diagnostics, d => d.Code.StartsWith("tiled.transform"));
        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "transforms.gscn"));
        Assert.Empty(SceneDefinitionValidator.Validate(scene));
        scene = SceneDefinitionSerializer.FromJson(SceneDefinitionSerializer.ToJson(scene));
        var tiles = scene.Layers.Single().Tiles;
        Assert.Equal(expected, tiles.Select(tile => tile.Transform));
        using var sheet = Gondwana.Drawing.Tilesheets.TilesheetRegistry.Instance.LoadFromDefinitionFile(
            Path.Combine(request.OutputDirectory, "transforms.gts"), "terrain");
        using var runtime = SceneDefinitionSerializer.ToScene(scene);
        Assert.Equal(expected, Enumerable.Range(0, 8).Select(x => runtime.SceneLayers.Single()[x, 0]!.Transform));
        Assert.Equal(expected, SceneDefinitionSerializer.FromScene(runtime).Layers[0].Tiles.Select(tile => tile.Transform));
        for (int i = 0; i < flags.Length; i++)
        {
            var decoded = TiledTileTransform.Decode(flags[i] | 2u);
            Assert.Equal(2u, decoded.Gid);
            Assert.Equal(expected[i], decoded.Transform);
            Assert.False(decoded.HasHexRotation);
            Assert.Equal("terrain", tiles[i].Frame!.Tilesheet);
            Assert.Equal(1, tiles[i].Frame!.XTile);
            Assert.Equal(0, tiles[i].Frame!.YTile);
        }
    }
}
