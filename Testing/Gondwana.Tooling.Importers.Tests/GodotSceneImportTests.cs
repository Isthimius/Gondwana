using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Importers.Tests;

/// <summary>Imports engine-generated Godot fixtures through the public transactional pipeline.</summary>
public sealed class GodotSceneImportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "GondwanaSceneImport-" + Guid.NewGuid().ToString("N"));

    public GodotSceneImportTests()
    {
        Directory.CreateDirectory(directory);
        foreach (string file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Godot4")))
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
    }

    public void Dispose() => Directory.Delete(directory, true);
    private ExternalImportRequest Request(string name = "legacy") => new(Path.Combine(directory, name + ".tscn"), Path.Combine(directory, "output"));
    private void Replace(string file, string oldValue, string newValue)
    {
        string path = Path.Combine(directory, file);
        string text = File.ReadAllText(path);
        Assert.Contains(oldValue, text);
        File.WriteAllText(path, text.Replace(oldValue, newValue));
    }

    [Theory]
    [InlineData("legacy", 2)]
    [InlineData("layers", 4)]
    [InlineData("layers-43", 4)]
    public void GeneratedScenesRoundTripAndMaterialize(string name, int sheets)
    {
        var request = Request(name);
        var importer = new GodotSceneImporter();
        Assert.IsType<GodotSceneImporter>(ExternalImporterRegistry.CreateProviders().Single(p => p.CanImport(request.SourcePath)));
        var analysis = importer.Analyze(request);
        Assert.True(analysis.CanImport, string.Join(";", analysis.Diagnostics));
        Assert.False(Directory.Exists(request.OutputDirectory));
        Assert.Contains(Path.Combine(directory, "terrain.tres"), analysis.Dependencies);
        Assert.Contains(Path.Combine(directory, "atlas.png"), analysis.Dependencies);
        Assert.Equal(sheets, Assert.Single(analysis.Artifacts, a => a.Kind == "GTS").LogicalKeys.Count);
        var result = importer.Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        string scenePath = Path.Combine(request.OutputDirectory, name + ".gscn");
        var scene = SceneDefinitionSerializer.Load(scenePath);
        Assert.Empty(SceneDefinitionValidator.Validate(scene));
        Assert.Equal(2, scene.Layers.Count);
        Assert.False(scene.Layers[0].Visible);
        Assert.True(scene.Layers[1].Visible);
        Assert.Equal(new[] { 0, 1 }, scene.Layers.Select(l => l.ZOrder));
        var ground = scene.Layers[1];
        Assert.Equal(new Point(9, -7), ground.OriginPx);
        Assert.Equal(4, ground.Rows);
        Assert.Equal(name == "legacy" ? 4 : 8, ground.Columns);
        var animated = Assert.Single(ground.Tiles, t => t.X == 0 && t.Y == 0);
        Assert.Equal($"{name}.tileset.0.source.3", animated.Frame!.Tilesheet);
        Assert.Equal(0, animated.Frame.XTile);
        Assert.Equal(0, animated.Frame.YTile);
        Assert.Equal($"{name}.tileset.0.source.3.tile.0.0", animated.AnimationKey);
        Assert.True(animated.StartAnimation);
        var otherSource = Assert.Single(ground.Tiles, t => t.Y == 3);
        Assert.Equal(3, otherSource.X);
        Assert.Equal($"{name}.tileset.0.source.9", otherSource.Frame!.Tilesheet);
        Assert.Equal(2, otherSource.Frame.XTile);
        Assert.Equal(1, otherSource.Frame.YTile);
        Assert.All(scene.TilesheetSources, s => Assert.Equal(name + ".gts", s.GtsPath));
        if (name != "legacy")
        {
            var expected = new[] { TileTransform.Identity, TileTransform.FlipHorizontal, TileTransform.FlipVertical,
                TileTransform.Rotate180, TileTransform.FlipDiagonal, TileTransform.Rotate90, TileTransform.Rotate270, TileTransform.FlipAntiDiagonal };
            Assert.Equal(expected, ground.Tiles.Where(t => t.Y == 1).OrderBy(t => t.X).Select(t => t.Transform));
        }
        else Assert.Equal(TileTransform.FlipHorizontal, otherSource.Transform);

        var runtimeSheets = TilesheetRegistry.Instance.LoadAllFromDefinitionFile(Path.Combine(request.OutputDirectory, name + ".gts"));
        var cycles = new List<Cycle>();
        try
        {
            foreach (string gani in result.WrittenFiles.Where(f => f.EndsWith(".gani", StringComparison.Ordinal)))
            {
                foreach (var animation in AnimationDefinitionSerializer.LoadAll(gani))
                {
                    Assert.Equal(name + ".gts", Assert.Single(animation.TilesheetSources).GtsPath);
                    Assert.Equal(new double?[] { 0.25, 0.75 }, animation.Frames.Select(f => f.DurationSeconds));
                    Assert.Contains(scene.AnimationSources, s => s.AnimationKey == animation.Key);
                }
                cycles.AddRange(AnimationDefinitionSerializer.LoadAllCycles(gani));
            }
            using var runtime = SceneDefinitionSerializer.LoadScene(scenePath);
            var layer = runtime.SceneLayers[1];
            Assert.Equal(ground.OriginPx, layer.OriginPx);
            Assert.Equal(animated.Frame.Tilesheet, layer[0, 0]!.CurrentFrame.Tilesheet.Name);
            Assert.Equal(new Rectangle(-9, 7, 16, 16), layer[0, 0]!.DrawLocationWorld);
            Assert.True(layer[0, 0]!.EnableAnimator);
            Assert.Equal(otherSource.Transform, layer[3, 3]!.Transform);
            var roundTrip = SceneDefinitionSerializer.FromScene(runtime);
            Assert.Equal(ground.OriginPx, roundTrip.Layers[1].OriginPx);
            Assert.Equal(ground.Tiles.Count, roundTrip.Layers[1].Tiles.Count(t => t.Frame is not null));
        }
        finally
        {
            foreach (var cycle in cycles) cycle.Dispose();
            foreach (var sheet in runtimeSheets) sheet.Dispose();
        }
        var before = result.WrittenFiles.ToDictionary(f => f, File.ReadAllBytes);
        Assert.Empty(importer.Import(request).WrittenFiles);
        var overwritten = importer.Import(request with { Overwrite = true });
        Assert.True(overwritten.Analysis.CanImport);
        foreach (var (path, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("terrain.tres")]
    [InlineData("atlas.png")]
    public void MissingDependenciesBlockWithoutWrites(string dependency)
    {
        File.Delete(Path.Combine(directory, dependency));
        var result = new GodotSceneImporter().Import(Request());
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Code == "dependency.missing");
        Assert.False(Directory.Exists(Request().OutputDirectory));
    }

    [Theory]
    [InlineData("position = Vector2(16, 32)", "position = Vector2(0.5, 32)", "Fractional")]
    [InlineData("position = Vector2(16, 32)", "rotation = 1.0", "rotation")]
    [InlineData("position = Vector2(16, 32)", "scale = Vector2(2, 1)", "scale")]
    [InlineData("position = Vector2(16, 32)", "y_sort_enabled = true", "y_sort_enabled")]
    [InlineData("position = Vector2(16, 32)", "z_index = 4097", "z-index")]
    [InlineData("format = 2", "format = 1", "format=2")]
    [InlineData("PackedInt32Array(-2, 3, 0, 131073, 131081, 268435457)", "PackedInt32Array(-2, 3)", "Truncated")]
    [InlineData("PackedInt32Array(-2, 3, 0, 131073, 131081, 268435457)", "PackedInt32Array(-2, 3, 65536)", "Alternative tile")]
    [InlineData("PackedInt32Array(-2, 3, 0, 131073, 131081, 268435457)", "PackedInt32Array(-2, 8, 0)", "no declared")]
    [InlineData("PackedInt32Array(-2, 3, 0, 131073, 131081, 268435457)", "PackedInt32Array(-2, 65539, 1)", "no declared")]
    [InlineData("PackedInt32Array(-2, 3, 0, 131073, 131081, 268435457)", "PackedInt32Array(-2, 3, 0, -2, 3, 0)", "Duplicate")]
    [InlineData("type=\"Node2D\"", "type=\"CanvasLayer\"", "unsupported ancestor")]
    public void UnsafePlacementsBlockWholeTransaction(string from, string to, string diagnostic)
    {
        var importer = new GodotSceneImporter();
        var request = Request();
        var initial = importer.Import(request);
        Assert.True(initial.Analysis.CanImport);
        var before = initial.WrittenFiles.ToDictionary(f => f, File.ReadAllBytes);
        Replace("legacy.tscn", from, to);
        var failed = importer.Import(request with { Overwrite = true });
        Assert.False(failed.Analysis.CanImport);
        Assert.Empty(failed.WrittenFiles);
        Assert.Contains(failed.Analysis.Diagnostics, d => d.Severity == ExternalImportSeverity.Error && d.Message.Contains(diagnostic));
        foreach (var (path, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("[resource]", "[resource]\ntile_shape = 1", "tile_shape")]
    [InlineData("[resource]", "[resource]\nsource_level_tile_proxies = [3, 9]", "proxies")]
    [InlineData("0:0/0 = 0", "0:0/0 = 0\n0:0/0/texture_origin = Vector2i(1, 0)", "texture_origin")]
    [InlineData("[resource]", "[resource]\ntile_size = Vector2i(32, 32)", "dimensions differ")]
    [InlineData("TileSetAtlasSource", "TileSetScenesCollectionSource", "only TileSetAtlasSource")]
    public void UnsupportedTilesetGeometryBlocks(string from, string to, string diagnostic)
    {
        Replace("terrain.tres", from, to);
        var result = new GodotSceneImporter().Import(Request());
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Message.Contains(diagnostic));
        Assert.False(Directory.Exists(Request().OutputDirectory));
    }

    [Theory]
    [InlineData("PackedByteArray(1, 0)", "version")]
    [InlineData("PackedByteArray(0)", "Truncated")]
    [InlineData("PackedByteArray(0, 0, 1)", "Truncated")]
    public void InvalidByteDataIsNotPartiallyImported(string data, string diagnostic)
    {
        string path = Request("layers").SourcePath;
        string text = File.ReadAllText(path);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"PackedByteArray\([^)]*\)", data);
        File.WriteAllText(path, text);
        var result = new GodotSceneImporter().Import(Request("layers"));
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Message.Contains(diagnostic));
    }

    [Fact]
    public void UnsupportedLeafNodesAndScriptsAreDiagnosed()
    {
        File.AppendAllText(Request().SourcePath, "\n[node name=\"Camera\" type=\"Camera2D\" parent=\".\"]\n");
        Replace("legacy.tscn", "position = Vector2(7, -9)", "position = Vector2(7, -9)\nscript = null");
        var analysis = new GodotSceneImporter().Analyze(Request());
        Assert.True(analysis.CanImport);
        Assert.Contains(analysis.Diagnostics, d => d.Code == "godot.node.unsupported");
        Assert.Contains(analysis.Diagnostics, d => d.Code == "godot.scene.property" && d.Message.Contains("script"));
    }

    [Fact]
    public void NumericByteArraysDecodeTheSameGeneratedRecords()
    {
        var request = Request("layers");
        string text = File.ReadAllText(request.SourcePath);
        text = System.Text.RegularExpressions.Regex.Replace(text, "PackedByteArray\\(\"([^\"]*)\"\\)",
            m => "PackedByteArray(" + string.Join(", ", Convert.FromBase64String(m.Groups[1].Value)) + ")");
        File.WriteAllText(request.SourcePath, text.Replace("format=4", "format=3"));
        var result = new GodotSceneImporter().Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        var scene = SceneDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "layers.gscn"));
        Assert.Equal(10, scene.Layers[1].Tiles.Count);
        Assert.Equal(new Point(9, -7), scene.Layers[1].OriginPx);
    }

    [Fact]
    public void SharedTilesetIsConvertedOnceAcrossNodes()
    {
        string text = File.ReadAllText(Request().SourcePath);
        string reference = GodotTextResource.Parse(text).Single(s => s.Kind == "node" && s.Properties.ContainsKey("tile_set")).Properties["tile_set"];
        File.AppendAllText(Request().SourcePath, $"\n[node name=\"Other\" type=\"TileMapLayer\" parent=\".\"]\ntile_set = {reference}\n");
        var result = new GodotSceneImporter().Import(Request());
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        Assert.Equal(2, Assert.Single(result.Analysis.Artifacts, a => a.Kind == "GTS").LogicalKeys.Count);
        var scene = SceneDefinitionSerializer.Load(Path.Combine(Request().OutputDirectory, "legacy.gscn"));
        Assert.Equal(3, scene.Layers.Count);
        Assert.Empty(scene.Layers[2].Tiles);
    }

    [Fact]
    public void LegacyLayerAndSameNamedChildHaveDistinctStableNativeIds()
    {
        var request = Request();
        string text = File.ReadAllText(request.SourcePath);
        string reference = GodotTextResource.Parse(text).Single(s => s.Kind == "node" && s.Properties.ContainsKey("tile_set")).Properties["tile_set"];
        File.AppendAllText(request.SourcePath, $"\n[node name=\"layer_0\" type=\"TileMapLayer\" parent=\"Legacy\"]\ntile_set = {reference}\n");
        var importer = new GodotSceneImporter();
        var result = importer.Import(request);
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        string output = Path.Combine(request.OutputDirectory, "legacy.gscn");
        var scene = SceneDefinitionSerializer.Load(output);
        Assert.Empty(SceneDefinitionValidator.Validate(scene));
        Assert.Equal(new[] { "legacy.layer.1", "legacy.layer.0", "legacy.layer.2" }, scene.Layers.Select(l => l.ID));
        Assert.Single(scene.Layers[0].Tiles);
        Assert.Equal(2, scene.Layers[1].Tiles.Count);
        Assert.Empty(scene.Layers[2].Tiles);
        byte[] original = File.ReadAllBytes(output);
        Assert.True(importer.Import(request with { Overwrite = true }).Analysis.CanImport);
        Assert.Equal(original, File.ReadAllBytes(output));
    }

    [Theory]
    [InlineData("texture_origin", "Vector2i(1, 0)")]
    [InlineData("flip_h", "true")]
    [InlineData("flip_v", "true")]
    [InlineData("transpose", "true")]
    [InlineData("z_index", "1")]
    [InlineData("y_sort_origin", "1")]
    [InlineData("material", "null")]
    [InlineData("modulate", "Color(1, 0, 0, 1)")]
    public void ExactTileDataLookupsStillBlockEveryUnsupportedField(string field, string value)
    {
        Replace("terrain.tres", "0:0/0 = 0", $"0:0/0 = 0\n0:0/0/{field} = {value}");
        var result = new GodotSceneImporter().Import(Request());
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Severity == ExternalImportSeverity.Error &&
            d.Message.Contains($"TileData '{field}'") && d.Message.Contains("cell"));
        Assert.Empty(result.WrittenFiles);
    }

    [Fact]
    public void NativeKeyConflictsAndCancellationRemainProtected()
    {
        var request = Request();
        var importer = new GodotSceneImporter();
        Assert.True(importer.Import(request).Analysis.CanImport);
        string gts = Path.Combine(request.OutputDirectory, "legacy.gts");
        File.Copy(gts, Path.Combine(request.OutputDirectory, "conflict.gts"));
        var failed = importer.Import(request with { Overwrite = true });
        Assert.False(failed.Analysis.CanImport);
        Assert.Contains(failed.Analysis.Diagnostics, d => d.Code == "key.exists");
        Assert.Throws<OperationCanceledException>(() => importer.Analyze(request, new CancellationToken(true)));
    }

    [Fact]
    public void IntermediateNonCanvasNodeBlocksInsteadOfInheritingIncorrectTransforms()
    {
        Replace("layers.tscn", "name=\"Group\" type=\"Node2D\"", "name=\"Group\" type=\"Node\"");
        var result = new GodotSceneImporter().Import(Request("layers"));
        Assert.False(result.Analysis.CanImport);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Message.Contains("unsupported ancestor"));
    }

    [Fact]
    public void RelativeTexturesResolveFromTheExternalTilesetDirectory()
    {
        Replace("legacy.tscn", "res://terrain.tres", "tiles/terrain.tres");
        Replace("terrain.tres", "res://atlas.png", "../images/atlas.png");
        Directory.CreateDirectory(Path.Combine(directory, "tiles"));
        Directory.CreateDirectory(Path.Combine(directory, "images"));
        File.Move(Path.Combine(directory, "terrain.tres"), Path.Combine(directory, "tiles", "terrain.tres"));
        File.Move(Path.Combine(directory, "atlas.png"), Path.Combine(directory, "images", "atlas.png"));
        var result = new GodotSceneImporter().Import(Request());
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        Assert.Contains(Path.Combine(directory, "images", "atlas.png"), result.Analysis.Dependencies);
        var definitions = Gondwana.Drawing.Tilesheets.GTS.TilesheetDefinitionSerializer.LoadAll(
            Path.Combine(Request().OutputDirectory, "legacy.gts"));
        Assert.All(definitions, d => Assert.Equal("../images/atlas.png", d.Image.FilePath));
    }

    [Fact]
    public void SeveralAnimationsInOneAtlasRetainGroupedSelectors()
    {
        Replace("terrain.tres", "2:1/0 = 0", "2:1/0 = 0\n2:1/animation_frame_0/duration = 1.0\n2:1/animation_frame_1/duration = 2.0");
        var result = new GodotSceneImporter().Import(Request());
        Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
        Assert.Equal(2, result.Analysis.Artifacts.Count(a => a.Kind == "GANI"));
        Assert.All(result.Analysis.Artifacts.Where(a => a.Kind == "GANI"), a => Assert.Equal(2, a.LogicalKeys.Count));
        var scene = SceneDefinitionSerializer.Load(Path.Combine(Request().OutputDirectory, "legacy.gscn"));
        Assert.Equal(4, scene.AnimationSources.Count);
        foreach (var source in scene.AnimationSources)
        {
            var animation = AnimationDefinitionSerializer.Load(Path.Combine(Request().OutputDirectory, source.GaniPath!), source.AnimationKey);
            Assert.Equal("legacy.gts", Assert.Single(animation.TilesheetSources).GtsPath);
        }
        Assert.All(scene.Layers.SelectMany(l => l.Tiles), t => Assert.NotNull(t.AnimationKey));
    }
}
