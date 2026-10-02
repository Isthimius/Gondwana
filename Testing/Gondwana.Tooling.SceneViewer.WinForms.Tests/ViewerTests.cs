using Gondwana.Assets;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using SkiaSharp;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Gondwana.Tooling.SceneViewer.WinForms.Tests;

public sealed class ViewerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "GondwanaViewerTests", Guid.NewGuid().ToString("N"));
    private string ScenePath => Path.Combine(_directory, "scene with spaces.gscn");

    public ViewerTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void ArgumentsNormalizeRelativePathAndPreserveSpaces()
    {
        File.WriteAllText(ScenePath, "{}");
        Assert.Equal(ScenePath, SceneViewerArguments.Parse(["--scene", "scene with spaces.gscn"], _directory));
        Assert.Equal(ScenePath, SceneViewerArguments.Parse(["--scene", ScenePath], _directory));
        Assert.Null(SceneViewerArguments.Parse([], _directory));
    }

    [Theory]
    [InlineData("--scene")]
    [InlineData("--unknown")]
    public void MissingOrUnknownArgumentIsRejected(string argument) =>
        Assert.Throws<ArgumentException>(() => SceneViewerArguments.Parse([argument], _directory));

    [Fact]
    public void InvalidAndMissingPathsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => SceneViewerArguments.Parse(["--scene", "bad\0.gscn"], _directory));
        Assert.Throws<ArgumentException>(() => SceneViewerArguments.Parse(["--scene", "file.txt"], _directory));
        Assert.Throws<FileNotFoundException>(() => SceneViewerArguments.Parse(["--scene", "missing.gscn"], _directory));
    }

    [Fact]
    public void LoadsSceneDependenciesAndRealRuntimeAnimation()
    {
        WriteContent();
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.Equal("viewer-test", scene.ID);
        Assert.True(TilesheetRegistry.Instance.TryGet("sheet", out _));
        Assert.Equal(2, scene.SceneLayers.Count);
        Assert.True(scene[0]![0, 0]!.EnableAnimator);
        Assert.True(scene[0]![1, 0]!.EnableAnimator);
        Assert.True(scene[0]![0, 0]!.TileAnimator.IsCycling);
        Assert.True(scene[0]![1, 0]!.TileAnimator.IsCycling);
        Assert.Equal("walk", scene[0]![0, 0]!.TileAnimator.CurrentCycle.CycleKey);
        Assert.Equal(TileTransform.FlipHorizontal, scene[0]![1, 0]!.Transform);
    }

    [Theory]
    [InlineData("sheet.gts", "Tilesheet source 'sheet'")]
    [InlineData("animation.gani", "Animation source 'walk'")]
    public void MissingDependencyIdentifiesSource(string file, string message)
    {
        WriteContent();
        File.Delete(Path.Combine(_directory, file));
        var error = Assert.Throws<InvalidDataException>(() => new ViewerSceneLoader().Load(ScenePath));
        Assert.Contains(message, error.Message);
        Assert.Contains(file, error.Message);
    }

    [Fact]
    public void GaniOwnTilesheetsAreResolvedRelativeToGani()
    {
        WriteContent();
        var definition = SceneDefinitionSerializer.Load(ScenePath);
        definition.TilesheetSources.Clear();
        SceneDefinitionSerializer.Save(ScenePath, definition);
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.True(scene[0]![0, 0]!.EnableAnimator);
    }

    [Fact]
    public void PackedDefinitionsAndImageUseRuntimeArchiveLoaders()
    {
        WriteContent();
        string archivePath = Path.Combine(_directory, "content.gaf");
        using (var archive = AssetsFile.LoadOrCreate(archivePath))
        {
            var sheet = TilesheetDefinitionSerializer.Load(Path.Combine(_directory, "sheet.gts"));
            sheet.Image = new TilesheetImageDefinition { AssetEntryName = "image" };
            using var sheetStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(sheet)));
            archive.Add(AssetTypes.TilesheetDefinition, "sheet", sheetStream);
            archive.Add(AssetTypes.Image, Path.Combine(_directory, "image.png"), "image");
            var animation = AnimationDefinitionSerializer.Load(Path.Combine(_directory, "animation.gani"));
            animation.TilesheetSources = [AnimationTilesheetSourceDefinition.Packed("sheet", "content.gaf", "sheet")];
            using var animationStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(AnimationDefinitionSerializer.ToJson(animation)));
            archive.Add(AssetTypes.AnimationDefinition, "walk", animationStream);
            archive.Save();
        }
        var definition = SceneDefinitionSerializer.Load(ScenePath);
        definition.TilesheetSources = [SceneTilesheetSourceDefinition.Packed("sheet", "content.gaf", "sheet")];
        definition.AnimationSources = [SceneAnimationSourceDefinition.Packed("walk", "content.gaf", "walk")];
        SceneDefinitionSerializer.Save(ScenePath, definition);
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.True(scene[0]![0, 0]!.EnableAnimator);
    }

    [Fact]
    public void InvalidSceneFailsBeforeMaterialization()
    {
        File.WriteAllText(ScenePath, "{\"Layers\":[{\"Columns\":-1}]}");
        Assert.Contains("validation", Assert.Throws<InvalidDataException>(() => new ViewerSceneLoader().Load(ScenePath)).Message);
    }

    [Fact]
    public void CrossCycleLinksAreWiredAfterRegistration()
    {
        WriteContent();
        var first = AnimationDefinitionSerializer.Load(Path.Combine(_directory, "animation.gani"));
        first.NextCycleKey = "return";
        AnimationDefinitionSerializer.Save(Path.Combine(_directory, "animation.gani"), first);
        first.Key = "return";
        first.NextCycleKey = "walk";
        AnimationDefinitionSerializer.Save(Path.Combine(_directory, "return.gani"), first);
        var definition = SceneDefinitionSerializer.Load(ScenePath);
        definition.AnimationSources.Add(SceneAnimationSourceDefinition.Loose("return", "return.gani"));
        SceneDefinitionSerializer.Save(ScenePath, definition);
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        var cycle = scene[0]![0, 0]!.TileAnimator.CurrentCycle;
        Assert.Equal("return", cycle.NextCycle.CycleKey);
        Assert.Same(cycle, cycle.NextCycle.NextCycle);
    }

    [Fact]
    public void ConflictingLogicalTilesheetSourcesFailClearly()
    {
        WriteContent();
        File.Copy(Path.Combine(_directory, "sheet.gts"), Path.Combine(_directory, "other.gts"));
        var animation = AnimationDefinitionSerializer.Load(Path.Combine(_directory, "animation.gani"));
        animation.TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("sheet", "other.gts")];
        AnimationDefinitionSerializer.Save(Path.Combine(_directory, "animation.gani"), animation);
        Assert.Contains("Conflicting sources", Assert.Throws<InvalidDataException>(() => new ViewerSceneLoader().Load(ScenePath)).Message);
    }

    private void WriteContent()
    {
        using var bitmap = new SKBitmap(32, 16);
        bitmap.Erase(SKColors.Coral);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using (var stream = File.Create(Path.Combine(_directory, "image.png")))
            data.SaveTo(stream);
        TilesheetDefinitionSerializer.Save(Path.Combine(_directory, "sheet.gts"), new TilesheetDefinition
        {
            Name = "sheet",
            Image = new TilesheetImageDefinition { FilePath = "image.png" },
            Regions = [new TilesheetRegionDefinition { Area = new Rectangle(0, 0, 32, 16), TileSize = new Size(16, 16) }]
        });
        AnimationDefinitionSerializer.Save(Path.Combine(_directory, "animation.gani"), new AnimationDefinition
        {
            Key = "walk", ThrottleTime = .1,
            TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("sheet", "sheet.gts")],
            Frames = [new AnimationFrameDefinition { Tilesheet = "sheet" }, new AnimationFrameDefinition { Tilesheet = "sheet", XTile = 1 }]
        });
        SceneDefinitionSerializer.Save(ScenePath, new SceneDefinition
        {
            ID = "viewer-test",
            TilesheetSources = [SceneTilesheetSourceDefinition.Loose("sheet", "sheet.gts")],
            AnimationSources = [SceneAnimationSourceDefinition.Loose("walk", "animation.gani")],
            Layers =
            [
                new SceneLayerDefinition
                {
                    ID = "front", Columns = 2, Rows = 1, TileWidth = 16, TileHeight = 16,
                    Tiles =
                    [
                        new SceneLayerTileDefinition { AnimationKey = "walk", StartAnimation = true },
                        new SceneLayerTileDefinition { X = 1, AnimationKey = "walk", StartAnimation = true, Transform = TileTransform.FlipHorizontal }
                    ]
                },
                new SceneLayerDefinition { ID = "back", Columns = 2, Rows = 1, ZOrder = -1, Parallax = .5f }
            ]
        });
    }

    public void Dispose()
    {
        Scene.ClearAllScenes();
        Cycle.ClearAllAnimationCycles();
        TilesheetRegistry.Instance.Clear();
        AssetsFile.ClearAll();
        Directory.Delete(_directory, true);
    }
}
