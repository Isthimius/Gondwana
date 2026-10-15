using System.Drawing;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Scenes.Editing;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

/// <summary>Regression tests for GANI-dependent GTS frame previews in the GSCN editor.</summary>
public sealed class SceneAnimationSourceTests
{
    /// <summary>Referenced GTS members resolve by logical tilesheet identity, even when not first in the file.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreviewResolvesNamedTilesheetInSingletonOrMultiDefinitionFile(bool multipleDefinitions) =>
        WithDirectory(root =>
        {
            var (gts, gani) = CreateAnimationSources(root, multipleDefinitions);
            using var source = SceneAnimationSource.LoadMember(gani, "water.flow");

            var sheet = Assert.Single(source.Tilesheets);
            Assert.Equal("terrain", sheet.Definition.Name);
            Assert.Equal(Path.GetFullPath(gts), sheet.FilePath);
            Assert.NotNull(sheet.Image);
            Assert.Empty(source.Diagnostics);

            var frame = Assert.IsType<Gondwana.Scenes.GSCN.SceneFrameDefinition>(source.FirstPreviewFrame());
            Assert.True(sheet.TryResolve(frame, out var region, out var bounds));
            Assert.NotNull(region);
            Assert.Equal(new Rectangle(0, 0, 16, 16), bounds);
            Assert.Same(sheet, source.FindTilesheet("terrain"));
            Assert.Null(source.FindTilesheet("unrelated"));
        });

    /// <summary>Missing member identities must not silently resolve to another tilesheet in the same document.</summary>
    [Fact]
    public void MissingNamedTilesheetProducesDiagnosticInsteadOfUsingFirstMember() =>
        WithDirectory(root =>
        {
            var (gts, gani) = CreateAnimationSources(root, multipleDefinitions: true);
            var animation = AnimationDefinitionSerializer.Load(gani);
            animation.TilesheetSources[0].Tilesheet = "missing";
            AnimationDefinitionSerializer.Save(gani, animation);

            using var source = SceneAnimationSource.Load(gani);
            Assert.Empty(source.Tilesheets);
            Assert.Null(source.FindTilesheet("unrelated"));
            Assert.Null(source.FindTilesheet("terrain"));
            Assert.Contains(source.Diagnostics, diagnostic =>
                diagnostic.Contains("missing", StringComparison.Ordinal) &&
                diagnostic.Contains("WARNING", StringComparison.Ordinal));
            Assert.True(File.Exists(gts));
        });

    private static (string GtsPath, string GaniPath) CreateAnimationSources(
        string root, bool multipleDefinitions)
    {
        var directory = Path.Combine(root, "tiles");
        Directory.CreateDirectory(directory);
        using (var bitmap = new Bitmap(16, 16))
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.LimeGreen);
            bitmap.Save(Path.Combine(directory, "atlas.png"));
        }

        TilesheetDefinition Sheet(string name) => new()
        {
            Name = name,
            Image = new() { FilePath = "atlas.png" },
            Regions = [new()
            {
                Name = "default",
                Area = new Rectangle(0, 0, 16, 16),
                TileSize = new Size(16, 16)
            }]
        };

        var gts = Path.Combine(directory, "world.gts");
        var terrain = Sheet("terrain");
        if (multipleDefinitions)
            TilesheetDefinitionSerializer.SaveAll(gts, [Sheet("unrelated"), terrain]);
        else
            TilesheetDefinitionSerializer.Save(gts, terrain);

        var gani = Path.Combine(root, "water.gani");
        AnimationDefinitionSerializer.Save(gani, new AnimationDefinition
        {
            Key = "water.flow",
            ThrottleTime = 0.1,
            TilesheetSources = [AnimationTilesheetSourceDefinition.Loose(
                "terrain", Path.Combine("tiles", "world.gts"))],
            Frames = [new() { Tilesheet = "terrain", RegionName = "default" }]
        });
        return (gts, gani);
    }

    private static void WithDirectory(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "GscnAnimationPreview-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try { action(root); }
        finally { Directory.Delete(root, recursive: true); }
    }
}
