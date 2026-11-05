using System.Drawing;
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

    private string OversizedMap(string orientation = "orthogonal")
    {
        string path = Path.Combine(directory, "oversized.tmx");
        File.WriteAllText(path, $"""
            <map orientation="{orientation}" width="2" height="2" tilewidth="32" tileheight="24">
              <tileset firstgid="1" source="oversized.tsx"/>
              <layer width="2" height="2"><data encoding="csv">1,0,0,1</data></layer>
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
        Rectangle cell = runtimeLayer.CoordinateSystem.GetPixelRangeForTile(tile, false);
        Rectangle visual = tile.DrawLocationWorld;
        Assert.Equal(cell.Left - left, visual.Left);
        Assert.Equal(cell.Top - top, visual.Top);
        Assert.Equal(cell.Right + right, visual.Right);
        Assert.Equal(cell.Bottom, visual.Bottom);
        Assert.Equal(new Size(frameWidth, frameHeight), visual.Size);
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

    /// <summary>Nonzero Tiled tileoffset remains explicitly unsupported instead of being discarded.</summary>
    [Fact]
    public void MapRejectsNonzeroTileOffset()
    {
        OversizedTileset(40, 40, tileOffset: "<tileoffset x='3' y='-2'/>");
        var request = new ExternalImportRequest(OversizedMap(), Path.Combine(directory, "output"));
        var result = new TiledMapImporter().Import(request);
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
