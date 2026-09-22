using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using SkiaSharp;

namespace Gondwana.Tooling.Importers.Tests;

// All fixture XML and pixels are purpose-built here; no external tools or artwork.
public sealed class TiledImportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "GondwanaImportTests-" + Guid.NewGuid().ToString("N"));
    public TiledImportTests() => Directory.CreateDirectory(directory);
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
}
