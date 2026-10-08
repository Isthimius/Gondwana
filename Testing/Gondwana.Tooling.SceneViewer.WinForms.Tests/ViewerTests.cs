using Gondwana.Assets;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using SkiaSharp;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Gondwana.Tooling.SceneViewer.WinForms.Tests;

/// <summary>
/// Contains regression tests for viewer.
/// </summary>
public sealed partial class ViewerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "GondwanaViewerTests", Guid.NewGuid().ToString("N"));
    private string ScenePath => Path.Combine(_directory, "scene with spaces.gscn");

    /// <summary>
    /// Initializes a new instance of the <c>ViewerTests</c> class.
    /// </summary>
    public ViewerTests() => Directory.CreateDirectory(_directory);

    /// <summary>
    /// Verifies arguments normalize relative path and preserve spaces.
    /// </summary>
    [Fact]
    public void ArgumentsNormalizeRelativePathAndPreserveSpaces()
    {
        File.WriteAllText(ScenePath, "{}");
        Assert.Equal(ScenePath, SceneViewerArguments.Parse(["--scene", "scene with spaces.gscn"], _directory));
        Assert.Equal(ScenePath, SceneViewerArguments.Parse(["--scene", ScenePath], _directory));
        Assert.Null(SceneViewerArguments.Parse([], _directory));
    }

    /// <summary>
    /// Verifies stress arguments parse tile count and projection.
    /// </summary>
    [Fact]
    public void StressArgumentsParseTileCountAndProjection()
    {
        var defaultProjection = SceneViewerArguments.ParseStress(["--stress", "50000"]);
        Assert.NotNull(defaultProjection);
        Assert.Equal(50_000, defaultProjection.TileCount);
        Assert.Equal(CoordinateSystemTypes.Orthogonal, defaultProjection.Projection);

        var iso = SceneViewerArguments.ParseStress(
            ["--stress", "20000", "--projection", "IsometricRhombic"]);
        Assert.NotNull(iso);
        Assert.Equal(20_000, iso.TileCount);
        Assert.Equal(CoordinateSystemTypes.IsometricRhombic, iso.Projection);

        Assert.Null(SceneViewerArguments.ParseStress(["--scene", "anything.gscn"]));
    }

    /// <summary>
    /// Verifies invalid stress tile counts are rejected.
    /// </summary>
    /// <param name="value">The value value for this test case.</param>
    [Theory]
    [InlineData("0")]
    [InlineData("100001")]
    [InlineData("not-a-number")]
    public void InvalidStressTileCountsAreRejected(string value) =>
        Assert.Throws<ArgumentException>(() =>
            SceneViewerArguments.ParseStress(["--stress", value]));

    /// <summary>
    /// Verifies invalid stress projection is rejected.
    /// </summary>
    [Fact]
    public void InvalidStressProjectionIsRejected() =>
        Assert.Throws<ArgumentException>(() =>
            SceneViewerArguments.ParseStress(
                ["--stress", "50000", "--projection", "NotAProjection"]));

    /// <summary>
    /// Verifies missing or unknown argument is rejected.
    /// </summary>
    /// <param name="argument">The argument value for this test case.</param>
    [Theory]
    [InlineData("--scene")]
    [InlineData("--unknown")]
    public void MissingOrUnknownArgumentIsRejected(string argument) =>
        Assert.Throws<ArgumentException>(() => SceneViewerArguments.Parse([argument], _directory));

    /// <summary>
    /// Verifies invalid and missing paths are rejected.
    /// </summary>
    [Fact]
    public void InvalidAndMissingPathsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => SceneViewerArguments.Parse(["--scene", "bad\0.gscn"], _directory));
        Assert.Throws<ArgumentException>(() => SceneViewerArguments.Parse(["--scene", "file.txt"], _directory));
        Assert.Throws<FileNotFoundException>(() => SceneViewerArguments.Parse(["--scene", "missing.gscn"], _directory));
    }

    /// <summary>
    /// Verifies loads scene dependencies and real runtime animation.
    /// </summary>
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

    /// <summary>
    /// Verifies missing dependency identifies source.
    /// </summary>
    /// <param name="file">The file value for this test case.</param>
    /// <param name="message">The message value for this test case.</param>
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

    /// <summary>
    /// Verifies gani own tilesheets are resolved relative to gani.
    /// </summary>
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

    /// <summary>
    /// Verifies packed definitions and image use runtime archive loaders.
    /// </summary>
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
        File.Delete(Path.Combine(_directory, "sheet.gts"));
        File.Delete(Path.Combine(_directory, "animation.gani"));
        using var scene = new ViewerSceneLoader().Load(ScenePath);
        Assert.True(scene[0]![0, 0]!.EnableAnimator);
    }

    /// <summary>
    /// Verifies invalid scene fails before materialization.
    /// </summary>
    [Fact]
    public void InvalidSceneFailsBeforeMaterialization()
    {
        File.WriteAllText(ScenePath, "{\"Layers\":[{\"Columns\":-1}]}");
        Assert.Contains("validation", Assert.Throws<InvalidDataException>(() => new ViewerSceneLoader().Load(ScenePath)).Message);
    }

    /// <summary>
    /// Verifies cross cycle links are wired after registration.
    /// </summary>
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
        var cycle = Assert.Single(Cycle.GetAnimationCycles(), candidate => candidate.CycleKey == "walk");
        Assert.Equal("return", cycle.NextCycle.CycleKey);
        Assert.Same(cycle, cycle.NextCycle.NextCycle);
    }

    /// <summary>
    /// Verifies conflicting logical tilesheet sources fail clearly.
    /// </summary>
    [Fact]
    public void ConflictingLogicalTilesheetSourcesFailClearly()
    {
        WriteContent();
        File.Copy(Path.Combine(_directory, "sheet.gts"), Path.Combine(_directory, "other.gts"));
        var animation = AnimationDefinitionSerializer.Load(Path.Combine(_directory, "animation.gani"));
        animation.TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("sheet", "other.gts")];
        AnimationDefinitionSerializer.Save(Path.Combine(_directory, "animation.gani"), animation);
        var error = Assert.Throws<InvalidDataException>(() => new ViewerSceneLoader().Load(ScenePath));
        Assert.Contains("Multiple loose GTS", error.Message);
        Assert.Contains("sheet.gts", error.Message);
        Assert.Contains("other.gts", error.Message);
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
            Key = "walk",
            ThrottleTime = .1,
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

    /// <inheritdoc/>
    public void Dispose()
    {
        Gondwana.Drawing.Sprites.SpriteManager.Instance.Clear();
        // Headless tests have no engine update loop to sweep the queued removals.
        typeof(Gondwana.Drawing.Sprites.SpriteManager).GetMethod("SweepDisposedSprites",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(Gondwana.Drawing.Sprites.SpriteManager.Instance, null);
        Scene.ClearAllScenes();
        Cycle.ClearAllAnimationCycles();
        TilesheetRegistry.Instance.Clear();
        AssetsFile.ClearAll();
        Directory.Delete(_directory, true);
    }
}
