using System.Drawing;
using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;

namespace Gondwana.Tests;

public sealed class SpotAssetPackageTests
{
    private static string PackagePath => Path.Combine(AppContext.BaseDirectory, "assets", "spot.gaf");

    [Theory]
    [InlineData("spot_defaults", "spots", 93, 96, 1, 5)]
    [InlineData("spot_selected", "selected", 64, 64, 5, 1)]
    public void PackagedDefinitionsPreserveEveryGameplayFrame(
        string name, string sheetName, int width, int height, int columns, int rows)
    {
        AssetsFile.Validate(PackagePath);
        using var packageStream = File.OpenRead(PackagePath);
        using var assets = AssetsFile.Load(packageStream, register: false);
        using var definitionStream = assets.Get(AssetTypes.TilesheetDefinition, name + ".gts");
        Assert.NotNull(definitionStream);
        var definition = TilesheetDefinitionSerializer.Load(definitionStream);
        Assert.Equal(name + ".png", definition.Image.AssetEntryName);
        Assert.True(string.IsNullOrEmpty(definition.Image.FilePath));
        Assert.True(string.IsNullOrEmpty(definition.Image.AssetsFilePath));

        using var sheet = TilesheetRegistry.Instance.LoadFromDefinitionAsset(assets, name + ".gts");
        using var imageStream = assets.Get(AssetTypes.Image, name + ".png");
        Assert.NotNull(imageStream);
        using var original = TilesheetFactory.FromStream("original", imageStream);
        original.DefaultRegion.TileSize = new Size(width, height);

        Assert.Equal(sheetName, sheet.Name);
        Assert.Equal(columns, sheet.DefaultRegion.Columns);
        Assert.Equal(rows, sheet.DefaultRegion.Rows);
        Assert.Equal(original.DefaultRegion.Area, sheet.DefaultRegion.Area);
        Assert.Equal(original.DefaultRegion.TileSize, sheet.DefaultRegion.TileSize);
        Assert.Equal(original.DefaultRegion.Overhang, sheet.DefaultRegion.Overhang);
        Assert.Equal(original.DefaultRegion.CollisionAdjust, sheet.DefaultRegion.CollisionAdjust);
        Assert.Equal(original.DefaultRegion.CollisionType, sheet.DefaultRegion.CollisionType);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                var expected = original.DefaultRegion.GetBitmap(x, y)!;
                var actual = sheet.DefaultRegion.GetBitmap(x, y)!;
                Assert.Equal(expected.Width, actual.Width);
                Assert.Equal(expected.Height, actual.Height);
                Assert.Equal(expected.Pixels, actual.Pixels);
            }
        }
    }

    [Theory]
    [InlineData(AssetTypes.Image, "spot.png")]
    [InlineData(AssetTypes.Image, "clouds.png")]
    [InlineData(AssetTypes.Image, "gondwana-logo-text.png")]
    [InlineData(AssetTypes.Font, "ArchitectsDaughter-Regular.ttf")]
    [InlineData(AssetTypes.Audio, "sounovamusic-puzzle-amp-casual-game-music-460543.mp3")]
    public void PackageContainsSupportingRuntimeAssets(AssetTypes type, string name)
    {
        using var packageStream = File.OpenRead(PackagePath);
        using var assets = AssetsFile.Load(packageStream, register: false);
        using var stream = assets.Get(type, name);
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
    }
}
