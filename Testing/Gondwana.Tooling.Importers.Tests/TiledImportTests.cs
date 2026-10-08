using Gondwana.Drawing;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
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
        var gani = AnimationDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "terrain-tile-0.gani"));
        Assert.Equal(new double?[] { 0.1, 0.25 }, gani.Frames.Select(f => f.DurationSeconds));
        Assert.Equal(new[] { 1, 0 }, gani.Frames.Select(f => f.XTile));
        Assert.Equal("terrain.gts", gani.TilesheetSources[0].GtsPath);
        Assert.Empty(AnimationDefinitionValidator.Validate(gani));
        Assert.False(importer.Analyze(request).CanImport);
        Assert.Empty(importer.Import(request).WrittenFiles);
        Assert.Equal(2, importer.Import(request with { Overwrite = true }).WrittenFiles.Count);
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
        var scene = Gondwana.Scenes.GSCN.SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "level.gscn"));
        Assert.Empty(Gondwana.Scenes.GSCN.SceneDefinitionValidator.Validate(scene));
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
            Path.Combine(request.OutputDirectory, "terrain.gts"));
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
