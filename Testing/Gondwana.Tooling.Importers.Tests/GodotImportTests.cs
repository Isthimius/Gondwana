using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using SkiaSharp;

namespace Gondwana.Tooling.Importers.Tests;

/// <summary>
/// Contains regression tests for godot import.
/// </summary>
public sealed class GodotImportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "GondwanaGodotTests-" + Guid.NewGuid().ToString("N"));
    /// <summary>
    /// Initializes a new instance of the <c>GodotImportTests</c> class.
    /// </summary>
    public GodotImportTests() => Directory.CreateDirectory(directory);
    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(directory, true);

    /// <summary>
    /// Verifies converts atlas sources and mixed animation timing.
    /// </summary>
    /// <param name="multiple">The multiple value for this test case.</param>
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

    /// <summary>
    /// Verifies parser preserves unknown multiline values.
    /// </summary>
    [Fact]
    public void ParserPreservesUnknownMultilineValues()
    {
        var parsed = GodotTextResource.Parse("[resource]\nmetadata = {\n\"x\": [true, 3]\n}\nname = \"semi;colon\" ; comment\n");
        Assert.Contains("true", parsed[0].Properties["metadata"]);
        Assert.Equal("\"semi;colon\"", parsed[0].Properties["name"]);
    }

    /// <summary>
    /// Verifies unsupported tiles are explicitly diagnosed.
    /// </summary>
    /// <param name="property">The property value for this test case.</param>
    /// <param name="code">The code value for this test case.</param>
    /// <param name="canImport">The can import value for this test case.</param>
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

    /// <summary>
    /// Verifies uniform animation and missing project root.
    /// </summary>
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

    private ExternalImportRequest Godot3Fixture(string properties, string texturePath = "atlas.png", string extraResources = "")
    {
        using var image = new SKBitmap(32, 24);
        image.Erase(SKColors.Green);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(directory, "atlas.png"), png.ToArray());
        string source = Path.Combine(directory, "terrain.tres");
        File.WriteAllText(source, "[gd_resource type=\"TileSet\" format=2]\n" +
            $"[ext_resource path=\"{texturePath}\" type=\"Texture\" id=1]\n" +
            extraResources + "\n[resource]\n" + properties);
        return new(source, Path.Combine(directory, "output"));
    }

    private const string SingleTile = """
        0/name = "arbitrary display name"
        0/texture = ExtResource( 1 )
        0/region = Rect2( 3, 4, 8, 6 )
        0/tile_mode = 0
        """;

    private const string AtlasTile = """
        0/texture = ExtResource( 1 )
        0/region = Rect2( 3, 4, 14, 9 )
        0/tile_mode = 2
        0/autotile/tile_size = Vector2( 4, 4 )
        0/autotile/spacing = 1
        """;

    /// <summary>
    /// Verifies godot3 single tile preserves region and integral float pixels.
    /// </summary>
    /// <param name="reference">The reference value for this test case.</param>
    [Theory]
    [InlineData("ExtResource( 1 )")]
    [InlineData("ExtResource(\"1\")")]
    public void Godot3SingleTilePreservesRegionAndIntegralFloatPixels(string reference)
    {
        var request = Godot3Fixture(SingleTile.Replace("ExtResource( 1 )", reference).Replace("8, 6", "8.0, 6.0"));
        var result = new GodotTilesetImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        Assert.Single(result.WrittenFiles);
        var gts = TilesheetDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "terrain.gts"));
        Assert.Equal("terrain", gts.Name);
        var region = Assert.Single(gts.Regions);
        Assert.Equal(new System.Drawing.Rectangle(3, 4, 8, 6), region.Area);
        Assert.Equal(new System.Drawing.Size(8, 6), region.TileSize);
        Assert.Equal((1L, 1L), TilesheetDefinitionValidator.GridSize(region));
        Assert.Equal(0, region.TilePadding.Right);
        Assert.Equal(0, region.TilePadding.Bottom);
        Assert.Equal("../atlas.png", gts.Image.FilePath);
        Assert.Empty(TilesheetDefinitionValidator.Validate(gts, 32, 24));
    }

    /// <summary>
    /// Verifies godot3 atlas compensates trailing spacing and warns about selection.
    /// </summary>
    /// <param name="mode">The mode value for this test case.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Godot3AtlasCompensatesTrailingSpacingAndWarnsAboutSelection(int mode)
    {
        var request = Godot3Fixture(AtlasTile.Replace("tile_mode = 2", $"tile_mode = {mode}") + """

            0/autotile/bitmask_flags = [Vector2(0, 0), 511]
            0/autotile/priority_map = [Vector3(0, 0, 2)]
            0/autotile/icon_coordinate = Vector2(0, 0)
            0/autotile/fallback_mode = 1
            """);
        var result = new GodotTilesetImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        Assert.Single(result.WrittenFiles); // Native Godot 3 TileSets do not produce GANI.
        var gts = TilesheetDefinitionSerializer.Load(result.WrittenFiles[0]);
        var region = Assert.Single(gts.Regions);
        Assert.Equal((3L, 2L), TilesheetDefinitionValidator.GridSize(region));
        Assert.Equal(-1, region.RegionMargin.Right);
        Assert.Equal(-1, region.RegionMargin.Bottom);
        Assert.Equal(1, region.TilePadding.Right);
        Assert.Equal(1, region.TilePadding.Bottom);
        Assert.Empty(TilesheetDefinitionValidator.Validate(gts, 32, 24));
        Assert.Equal(mode == 1, result.Analysis.Diagnostics.Any(d => d.Code == "godot.autotile"));
        Assert.Single(result.Analysis.Diagnostics, d => d.Code == "godot.selection");
    }

    /// <summary>
    /// Verifies godot3 multiple tiles have stable names and separate textures.
    /// </summary>
    [Fact]
    public void Godot3MultipleTilesHaveStableNamesAndSeparateTextures()
    {
        var request = Godot3Fixture(SingleTile.Replace("0/", "3/").Replace("ExtResource( 1 )", "ExtResource( 2 )") + "\n" + SingleTile,
            extraResources: "[ext_resource path=\"second.png\" type=\"Texture\" id=2]");
        File.Copy(Path.Combine(directory, "atlas.png"), Path.Combine(directory, "second.png"));
        var result = new GodotTilesetImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        Assert.Equal(new[] { "terrain-tile-0.gts", "terrain-tile-3.gts" }, result.WrittenFiles.Select(Path.GetFileName));
        var definitions = result.WrittenFiles.Select(f => TilesheetDefinitionSerializer.Load(f)).ToArray();
        Assert.Equal(new[] { "terrain.tile.0", "terrain.tile.3" }, definitions.Select(d => d.Name));
        Assert.Equal(new[] { "../atlas.png", "../second.png" }, definitions.Select(d => d.Image.FilePath));
    }

    /// <summary>
    /// Verifies godot3 res paths use nearest project and fail without one.
    /// </summary>
    [Fact]
    public void Godot3ResPathsUseNearestProjectAndFailWithoutOne()
    {
        var request = Godot3Fixture(SingleTile, "res://atlas.png");
        Assert.Contains(new GodotTilesetImporter().Analyze(request).Diagnostics, d => d.Message.Contains("project.godot"));
        File.WriteAllText(Path.Combine(directory, "project.godot"), "");
        string nested = Path.Combine(directory, "nested");
        string resources = Path.Combine(nested, "resources");
        Directory.CreateDirectory(resources);
        File.WriteAllText(Path.Combine(nested, "project.godot"), "");
        File.Copy(Path.Combine(directory, "atlas.png"), Path.Combine(nested, "atlas.png"));
        string source = Path.Combine(resources, "terrain.tres");
        File.Copy(request.SourcePath, source);
        request = new(source, request.OutputDirectory);
        var result = new GodotTilesetImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        Assert.Equal("../nested/atlas.png", TilesheetDefinitionSerializer.Load(result.WrittenFiles.Single()).Image.FilePath);
    }

    /// <summary>
    /// Verifies godot3 malformed tiles fail without writing.
    /// </summary>
    /// <param name="before">The before value for this test case.</param>
    /// <param name="after">The after value for this test case.</param>
    /// <param name="message">The message value for this test case.</param>
    [Theory]
    [InlineData("ExtResource( 1 )", "ExtResource( 99 )", "Unresolved")]
    [InlineData("ExtResource( 1 )", "ExtResource( nope )", "reference")]
    [InlineData("ExtResource( 1 )", "ExtResource( 1.5 )", "reference")]
    [InlineData("Rect2( 3, 4, 8, 6 )", "Rect2(0, 0, 1)", "components")]
    [InlineData("Rect2( 3, 4, 8, 6 )", "Rect2(0, 0, -1, 1)", "positive")]
    [InlineData("Rect2( 3, 4, 8, 6 )", "Rect2(0, 0, 0, 1)", "positive")]
    [InlineData("Rect2( 3, 4, 8, 6 )", "Rect2(0, 0, 1.5, 1)", "integral")]
    [InlineData("Rect2( 3, 4, 8, 6 )", "Rect2(-1, 0, 1, 1)", "within")]
    [InlineData("Rect2( 3, 4, 8, 6 )", "Rect2(31, 0, 2, 1)", "within")]
    [InlineData("Rect2( 3, 4, 8, 6 )", "Rect2(0, 23, 1, 2)", "within")]
    [InlineData("0/texture", "0/unused_texture", "Missing Godot property texture")]
    [InlineData("tile_mode = 0", "tile_mode = 9", "mode")]
    [InlineData("tile_mode = 0", "tile_mode = 1.5", "integral")]
    public void Godot3MalformedTilesFailWithoutWriting(string before, string after, string message)
    {
        var result = new GodotTilesetImporter().Import(Godot3Fixture(SingleTile.Replace(before, after)));
        Assert.False(result.Analysis.CanImport);
        Assert.Empty(result.WrittenFiles);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Message.Contains(message));
    }

    /// <summary>
    /// Verifies godot3 invalid atlas geometry is rejected.
    /// </summary>
    /// <param name="before">The before value for this test case.</param>
    /// <param name="after">The after value for this test case.</param>
    /// <param name="message">The message value for this test case.</param>
    [Theory]
    [InlineData("Vector2( 4, 4 )", "Vector2(0, 4)", "positive")]
    [InlineData("Vector2( 4, 4 )", "Vector2(4, -4)", "positive")]
    [InlineData("Vector2( 4, 4 )", "Vector2(4.5, 4)", "integral")]
    [InlineData("spacing = 1", "spacing = -1", "nonnegative")]
    [InlineData("spacing = 1", "spacing = 1.5", "integral")]
    [InlineData("14, 9", "13, 9", "integral atlas")]
    [InlineData("14, 9", "14, 8", "integral atlas")]
    [InlineData("14, 9", "3, 4", "integral atlas")]
    public void Godot3InvalidAtlasGeometryIsRejected(string before, string after, string message)
    {
        var analysis = new GodotTilesetImporter().Analyze(Godot3Fixture(AtlasTile.Replace(before, after)));
        Assert.False(analysis.CanImport);
        Assert.Contains(analysis.Diagnostics, d => d.Message.Contains(message));
    }

    /// <summary>
    /// Verifies godot headers are validated.
    /// </summary>
    /// <param name="before">The before value for this test case.</param>
    /// <param name="after">The after value for this test case.</param>
    /// <param name="code">The code value for this test case.</param>
    [Theory]
    [InlineData("format=2", "format=1", "godot.format")]
    [InlineData("format=2", "format=4", "godot.format")]
    [InlineData("format=2", "", "godot.format")]
    [InlineData("type=\"TileSet\"", "type=\"Material\"", "source.invalid")]
    [InlineData("[resource]", "[unused]", "source.invalid")]
    [InlineData("id=1", "id=invalid", "source.invalid")]
    public void GodotHeadersAreValidated(string before, string after, string code)
    {
        var request = Godot3Fixture(SingleTile);
        File.WriteAllText(request.SourcePath, File.ReadAllText(request.SourcePath).Replace(before, after));
        var analysis = new GodotTilesetImporter().Analyze(request);
        Assert.False(analysis.CanImport);
        Assert.Contains(analysis.Diagnostics, d => d.Code == code);
    }

    /// <summary>
    /// Verifies godot duplicate resource ids are rejected.
    /// </summary>
    /// <param name="id">The id value for this test case.</param>
    [Theory]
    [InlineData("1")]
    [InlineData("\"1\"")]
    public void GodotDuplicateResourceIdsAreRejected(string id)
    {
        var request = Godot3Fixture(SingleTile, extraResources: $"[ext_resource path=\"atlas.png\" type=\"Texture\" id={id}]");
        var analysis = new GodotTilesetImporter().Analyze(request);
        Assert.False(analysis.CanImport);
        Assert.Contains(analysis.Diagnostics, d => d.Message.Contains("Duplicate"));
    }

    /// <summary>
    /// Verifies godot missing or undecodable textures fail.
    /// </summary>
    /// <param name="exists">The exists value for this test case.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GodotMissingOrUndecodableTexturesFail(bool exists)
    {
        var request = Godot3Fixture(SingleTile, "bad.png");
        if (exists) File.WriteAllText(Path.Combine(directory, "bad.png"), "not an image");
        var analysis = new GodotTilesetImporter().Analyze(request);
        Assert.False(analysis.CanImport);
        Assert.Contains(analysis.Diagnostics, d => d.Message.Contains(exists ? "decode" : "Missing"));
    }

    /// <summary>
    /// Verifies godot3 metadata warnings are grouped and do not change geometry.
    /// </summary>
    [Fact]
    public void Godot3MetadataWarningsAreGroupedAndDoNotChangeGeometry()
    {
        var request = Godot3Fixture(SingleTile + """

            0/shapes = [{"shape": SubResource( 2 ), "one_way": true}]
            0/shape_offset = Vector2(2, 2)
            0/shape_one_way = true
            0/navigation = SubResource( 3 )
            0/occluder = SubResource( 4 )
            0/normal_map = ExtResource( 2 )
            0/material = SubResource( 5 )
            0/modulate = Color(0, 1, 0, 1)
            0/tex_offset = Vector2(5, 5)
            0/z_index = 2
            0/custom/new_property = {"x": [true, 3]}
            """);
        var result = new GodotTilesetImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        foreach (string code in new[] { "collision", "navigation", "occlusion", "rendering", "metadata" })
            Assert.Single(result.Analysis.Diagnostics, d => d.Code == "godot." + code);
        Assert.Equal(new System.Drawing.Rectangle(3, 4, 8, 6), TilesheetDefinitionSerializer.Load(result.WrittenFiles.Single()).Regions[0].Area);
    }

    /// <summary>
    /// Verifies parser preserves both godot dialects.
    /// </summary>
    /// <param name="value">The value value for this test case.</param>
    [Theory]
    [InlineData("ExtResource( 1 )")]
    [InlineData("ExtResource(\"1\")")]
    [InlineData("Vector2( 32, 32 )")]
    [InlineData("Vector2i(32, 32)")]
    [InlineData("Rect2( 0, 0, 256, 128 )")]
    public void ParserPreservesBothGodotDialects(string value)
    {
        var sections = GodotTextResource.Parse("[gd_resource type=\"TileSet\" format=2]\n[resource]\n0/value = " + value);
        Assert.Equal(value, sections[1].Properties["0/value"]);
    }
}
