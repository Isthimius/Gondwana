using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using SkiaSharp;

namespace Gondwana.Tooling.Importers.Tests;

public sealed class GodotImportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "GondwanaGodotTests-" + Guid.NewGuid().ToString("N"));
    public GodotImportTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConvertsAtlasSourcesAndMixedAnimationTiming(bool multiple)
    {
        File.WriteAllText(Path.Combine(directory, "project.godot"), "; handcrafted fixture");
        using var image = new SKBitmap(10, 4);
        image.Erase(SKColors.Blue);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(directory, "atlas.png"), png.ToArray());
        string atlas = """
            texture = ExtResource("1")
            texture_region_size = Vector2i(2, 2)
            margins = Vector2i(1, 1)
            separation = Vector2i(1, 1)
            0:0/0 = 0
            0:0/animation_speed = 2.0
            0:0/animation_frame_0/duration = 1.0
            0:0/animation_frame_1/duration = 3.0
            metadata/new_field = {"unknown": [1, 2, 3]}
            """;
        string text = "[gd_resource type=\"TileSet\" format=3]\n[ext_resource type=\"Texture2D\" path=\"res://atlas.png\" id=\"1\"]\n" +
            "[sub_resource type=\"TileSetAtlasSource\" id=\"a\"]\n" + atlas + "\n";
        if (multiple) text += "[sub_resource type=\"TileSetAtlasSource\" id=\"b\"]\n" + atlas + "\n";
        text += "[resource]\ntile_size = Vector2i(2, 2)\nsources/0 = SubResource(\"a\")\n";
        if (multiple) text += "sources/2 = SubResource(\"b\")\n";
        string source = Path.Combine(directory, "terrain.tres");
        File.WriteAllText(source, text);
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
        var result = new GodotTilesetImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        Assert.Equal(multiple ? 4 : 2, result.WrittenFiles.Count);
        string file = multiple ? "terrain-source-2-tile-0-0.gani" : "terrain-tile-0-0.gani";
        var gani = AnimationDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, file));
        Assert.Equal(new double?[] { 0.5, 1.5 }, gani.Frames.Select(f => f.DurationSeconds));
        Assert.Equal(new[] { 0, 1 }, gani.Frames.Select(f => f.XTile));
        Assert.Empty(AnimationDefinitionValidator.Validate(gani));
        var gts = TilesheetDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, multiple ? "terrain-source-2.gts" : "terrain.gts"));
        Assert.Empty(TilesheetDefinitionValidator.Validate(gts, 10, 4));
        Assert.Equal((3L, 1L), TilesheetDefinitionValidator.GridSize(gts.Regions[0]));
        Assert.Equal(-1, gts.Regions[0].RegionMargin.Right);
        Assert.Equal("../atlas.png", gts.Image.FilePath);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Code == "godot.sparse");
    }

    [Fact]
    public void ParserPreservesUnknownMultilineValues()
    {
        var parsed = GodotTextResource.Parse("[resource]\nmetadata = {\n\"x\": [true, 3]\n}\nname = \"semi;colon\" ; comment\n");
        Assert.Contains("true", parsed[0].Properties["metadata"]);
        Assert.Equal("\"semi;colon\"", parsed[0].Properties["name"]);
    }

    [Theory]
    [InlineData("0:0/1 = 1", "godot.alternative", true)]
    [InlineData("0:0/size_in_atlas = Vector2i(2, 1)", "godot.multicell", false)]
    public void UnsupportedTilesAreExplicitlyDiagnosed(string property, string code, bool canImport)
    {
        ConvertsAtlasSourcesAndMixedAnimationTiming(false);
        string source = Path.Combine(directory, "terrain.tres");
        File.WriteAllText(source, File.ReadAllText(source).Replace("[resource]", property + "\n[resource]"));
        var analysis = new GodotTilesetImporter().Analyze(new(source, Path.Combine(directory, "output"), true));
        Assert.Equal(canImport, analysis.CanImport);
        Assert.Contains(analysis.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void UniformAnimationAndMissingProjectRoot()
    {
        ConvertsAtlasSourcesAndMixedAnimationTiming(false);
        string source = Path.Combine(directory, "terrain.tres");
        File.WriteAllText(source, File.ReadAllText(source).Replace("duration = 3.0", "duration = 1.0"));
        var request = new ExternalImportRequest(source, Path.Combine(directory, "output"), true);
        Assert.True(new GodotTilesetImporter().Import(request).Analysis.CanImport);
        Assert.All(AnimationDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "terrain-tile-0-0.gani")).Frames, f => Assert.Equal(0.5, f.DurationSeconds));
        File.Delete(Path.Combine(directory, "project.godot"));
        var analysis = new GodotTilesetImporter().Analyze(request);
        Assert.False(analysis.CanImport);
        Assert.Contains(analysis.Diagnostics, d => d.Message.Contains("project.godot"));
    }
}
