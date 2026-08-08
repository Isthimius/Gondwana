using System.Drawing;
using Gondwana.Assets;
using Gondwana.Drawing;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>
/// Verifies parity between runtime tilesheet metadata and the GTS definition model.
/// </summary>
public sealed class TilesheetGtsParityTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        $"GondwanaGtsParityTests_{Guid.NewGuid():N}");

    public TilesheetGtsParityTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        AssetsFile.ClearAll();

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>
    /// Verifies that all persistent file-backed tilesheet and region metadata survives
    /// runtime-to-definition-to-runtime conversion.
    /// </summary>
    [Fact]
    public void FileBackedTilesheet_RoundTripsAllPersistentMetadata()
    {
        var imagePath = CreateImageFile("sheet.png", 42, 22);

        using var original = TilesheetFactory.FromImageFile(
            "Logical Sheet Name",
            imagePath);

        original.RemoveRegion(TilesheetRegion.DefaultRegionName);

        var regionAdjust = new CollisionAdjust(
            top: 1,
            bottom: -2,
            left: 3,
            right: -4);

        var frameAdjust = new CollisionAdjust(
            top: 5,
            bottom: -6,
            left: 7,
            right: -8);

        var originalRegion = original.AddRegion(
            "Actors",
            new Rectangle(0, 0, 42, 22),
            new Size(16, 16),
            tilePadding: new Spacing(0, 0, 2, 0),
            regionMargin: new Spacing(2, 2, 4, 4),
            overhangPixels: new Spacing(1, 2, 3, 4),
            collisionAdjust: regionAdjust);

        originalRegion.SetFrameCollisionAdjust(1, 0, frameAdjust);
        original.ApplyMask(new SKColor(10, 20, 30, 255), tolerance: 7);

        var definition = TilesheetDefinitionSerializer.FromTilesheet(original);
        using var restored = TilesheetFactory.FromDefinition(definition);

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(Path.GetFullPath(original.ImageFilePath), Path.GetFullPath(restored.ImageFilePath));
        Assert.Equal(original.MaskColor, restored.MaskColor);
        Assert.Equal(original.MaskTolerance, restored.MaskTolerance);
        Assert.True(restored.Premultiplied);
        Assert.Null(restored.AssetIdentifier);

        var restoredRegion = Assert.Single(restored.Regions);
        Assert.Equal(originalRegion.Name, restoredRegion.Name);
        Assert.Equal(originalRegion.Area, restoredRegion.Area);
        Assert.Equal(originalRegion.TileSize, restoredRegion.TileSize);
        Assert.Equal(originalRegion.TilePadding, restoredRegion.TilePadding);
        Assert.Equal(originalRegion.RegionMargin, restoredRegion.RegionMargin);
        Assert.Equal(originalRegion.Overhang, restoredRegion.Overhang);
        Assert.Equal(originalRegion.CollisionAdjust, restoredRegion.CollisionAdjust);
        Assert.Equal(
            originalRegion.GetFrameCollisionAdjust(0, 0),
            restoredRegion.GetFrameCollisionAdjust(0, 0));
        Assert.Equal(
            originalRegion.GetFrameCollisionAdjust(1, 0),
            restoredRegion.GetFrameCollisionAdjust(1, 0));
        Assert.False(
            restoredRegion.TryGetFrameCollisionAdjustOverride(0, 0, out _));
        Assert.True(
            restoredRegion.TryGetFrameCollisionAdjustOverride(
                1,
                0,
                out var restoredFrameAdjust));
        Assert.Equal(frameAdjust, restoredFrameAdjust);
    }

    /// <summary>
    /// Verifies that an asset entry name does not replace the logical name declared by GTS.
    /// </summary>
    [Fact]
    public void AssetBackedDefinition_UsesDefinitionNameRatherThanAssetEntryName()
    {
        var assetsPath = Path.Combine(_tempDir, "tiles.gaf");
        using var assetsFile = AssetsFile.LoadOrCreate(assetsPath);
        using var imageStream = CreateImageStream(16, 16);
        assetsFile.Add(AssetTypes.Image, "images/player.png", imageStream);

        var definition = new TilesheetDefinition
        {
            Name = "Player Sprites",
            Image = new TilesheetImageDefinition
            {
                AssetEntryName = "images/player.png"
            },
            Regions =
            [
                new TilesheetRegionDefinition
                {
                    Name = TilesheetRegion.DefaultRegionName,
                    Area = new Rectangle(0, 0, 16, 16),
                    TileSize = new Size(16, 16)
                }
            ]
        };

        using var tilesheet = TilesheetFactory.FromDefinition(
            definition,
            defaultAssetsFile: assetsFile);

        Assert.Equal("Player Sprites", tilesheet.Name);
        Assert.NotNull(tilesheet.AssetIdentifier);
        Assert.Equal("images/player.png", tilesheet.AssetIdentifier.AssetName);
        Assert.Same(assetsFile, tilesheet.AssetIdentifier.AssetsFile);
    }

    /// <summary>
    /// Verifies that premultiplication without a mask survives a GTS round trip.
    /// </summary>
    [Fact]
    public void PremultipliedTilesheet_WithoutMask_RoundTrips()
    {
        var imagePath = CreateImageFile("premultiplied.png", 16, 16);

        using var original = TilesheetFactory.FromImageFile("Premultiplied", imagePath);
        original.ApplyPremultiplyAlpha();

        var definition = TilesheetDefinitionSerializer.FromTilesheet(original);
        using var restored = TilesheetFactory.FromDefinition(definition);

        Assert.True(definition.PremultiplyAlpha);
        Assert.Null(definition.Mask);
        Assert.True(restored.Premultiplied);
        Assert.Null(restored.MaskColor);
    }

    /// <summary>
    /// Verifies that mutually exclusive image sources are rejected instead of being
    /// resolved by undocumented precedence.
    /// </summary>
    [Fact]
    public void Definition_WithFileAndAssetImageSources_Throws()
    {
        var definition = new TilesheetDefinition
        {
            Name = "Ambiguous",
            Image = new TilesheetImageDefinition
            {
                FilePath = "sheet.png",
                AssetsFilePath = "tiles.gaf",
                AssetEntryName = "sheet.png"
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => TilesheetFactory.FromDefinition(definition, _tempDir));

        Assert.Contains("ambiguous", exception.Message.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that an assets-file path without an entry name is rejected clearly.
    /// </summary>
    [Fact]
    public void Definition_WithAssetsFileButNoEntryName_Throws()
    {
        var definition = new TilesheetDefinition
        {
            Name = "Incomplete",
            Image = new TilesheetImageDefinition
            {
                AssetsFilePath = "tiles.gaf"
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => TilesheetFactory.FromDefinition(definition, _tempDir));

        Assert.Contains(
            nameof(TilesheetImageDefinition.AssetEntryName),
            exception.Message);
    }

    /// <summary>
    /// Documents that a runtime-only bitmap has no persistent image source to write to GTS.
    /// </summary>
    [Fact]
    public void BitmapBackedTilesheet_CannotBeConvertedWithoutPersistentImageSource()
    {
        using var tilesheet = TilesheetFactory.FromBitmap(
            "In Memory",
            new SKBitmap(16, 16));

        var exception = Assert.Throws<InvalidOperationException>(
            () => TilesheetDefinitionSerializer.FromTilesheet(tilesheet));

        Assert.Contains(
            "no ImageFilePath or AssetIdentifier",
            exception.Message);
    }

    /// <summary>
    /// Verifies that explicitly persisting a bitmap-backed tilesheet writes its source
    /// image and allows ordinary GTS conversion afterward.
    /// </summary>
    [Fact]
    public void PersistImageToFile_PromotesBitmapBackedTilesheetToFileBacked()
    {
        var imagePath = Path.Combine(_tempDir, "persisted", "generated.png");
        var bitmap = new SKBitmap(24, 12);
        bitmap.Erase(new SKColor(10, 20, 30, 255));

        using var tilesheet = TilesheetFactory.FromBitmap("Generated", bitmap);
        tilesheet.PersistImageToFile(imagePath);

        Assert.Equal(Path.GetFullPath(imagePath), tilesheet.ImageFilePath);
        Assert.Null(tilesheet.AssetIdentifier);
        Assert.True(File.Exists(imagePath));

        using var persistedBitmap = SKBitmap.Decode(imagePath);
        Assert.NotNull(persistedBitmap);
        Assert.Equal(24, persistedBitmap.Width);
        Assert.Equal(12, persistedBitmap.Height);

        var definition = TilesheetDefinitionSerializer.FromTilesheet(tilesheet);
        Assert.Equal(Path.GetFullPath(imagePath).Replace('\\', '/'), definition.Image.FilePath);
    }

    /// <summary>
    /// Verifies that saving a bitmap-backed tilesheet as GTS automatically persists a
    /// sibling PNG and records that image using a relative path.
    /// </summary>
    [Fact]
    public void Save_BitmapBackedTilesheet_AutomaticallyPersistsSiblingImage()
    {
        var gtsPath = Path.Combine(_tempDir, "generated", "terrain.gts");
        var expectedImagePath = Path.ChangeExtension(gtsPath, ".png");

        var bitmap = new SKBitmap(32, 16);
        bitmap.Erase(new SKColor(40, 80, 120, 255));

        using var tilesheet = TilesheetFactory.FromBitmap("Terrain", bitmap);
        tilesheet.DefaultRegion.TileSize = new Size(16, 16);

        TilesheetDefinitionSerializer.Save(gtsPath, tilesheet);

        Assert.True(File.Exists(gtsPath));
        Assert.True(File.Exists(expectedImagePath));
        Assert.Equal(
            Path.GetFullPath(expectedImagePath),
            tilesheet.ImageFilePath);

        var definition = TilesheetDefinitionSerializer.Load(gtsPath);
        Assert.Equal("terrain.png", definition.Image.FilePath);

        using var restored = TilesheetFactory.FromDefinition(
            definition,
            Path.GetDirectoryName(gtsPath));

        Assert.Equal(tilesheet.Name, restored.Name);
        Assert.Equal(tilesheet.DefaultRegion.TileSize, restored.DefaultRegion.TileSize);
    }

    /// <summary>
    /// Verifies that automatic image persistence does not replace or duplicate an
    /// existing file-backed source.
    /// </summary>
    [Fact]
    public void Save_FileBackedTilesheet_DoesNotCreateSiblingImage()
    {
        var sourceImagePath = CreateImageFile("existing-source.png", 16, 16);
        var gtsPath = Path.Combine(_tempDir, "definitions", "sheet.gts");
        var siblingImagePath = Path.ChangeExtension(gtsPath, ".png");

        using var tilesheet = TilesheetFactory.FromImageFile(
            "Existing Source",
            sourceImagePath);

        TilesheetDefinitionSerializer.Save(gtsPath, tilesheet);

        Assert.False(File.Exists(siblingImagePath));
        Assert.Equal(
            Path.GetFullPath(sourceImagePath),
            tilesheet.ImageFilePath);

        var definition = TilesheetDefinitionSerializer.Load(gtsPath);
        Assert.Equal(
            Path.GetRelativePath(
                    Path.GetDirectoryName(gtsPath)!,
                    sourceImagePath)
                .Replace('\\', '/'),
            definition.Image.FilePath);
    }

    /// <summary>
    /// Verifies that persisting a transformed tilesheet writes its unprocessed source
    /// bitmap so GTS loading can reapply the stored mask exactly once.
    /// </summary>
    [Fact]
    public void PersistImageToFile_WithMask_PersistsOriginalSourceBitmap()
    {
        var imagePath = Path.Combine(_tempDir, "masked-source.png");
        var bitmap = new SKBitmap(16, 16);
        bitmap.Erase(SKColors.White);

        using var tilesheet = TilesheetFactory.FromBitmap("Masked", bitmap);
        tilesheet.ApplyMask(SKColors.White, tolerance: 0);
        Assert.Equal(0, tilesheet.SkBitmap.GetPixel(0, 0).Alpha);

        tilesheet.PersistImageToFile(imagePath);

        using var persistedBitmap = SKBitmap.Decode(imagePath);
        Assert.NotNull(persistedBitmap);
        Assert.Equal(255, persistedBitmap.GetPixel(0, 0).Alpha);
    }

    private string CreateImageFile(string fileName, int width, int height)
    {
        var path = Path.Combine(_tempDir, fileName);
        using var stream = CreateImageStream(width, height);
        using var file = File.Create(path);
        stream.CopyTo(file);
        return path;
    }

    private static MemoryStream CreateImageStream(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(40, 80, 120, 200));

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return new MemoryStream(data.ToArray());
    }
}
