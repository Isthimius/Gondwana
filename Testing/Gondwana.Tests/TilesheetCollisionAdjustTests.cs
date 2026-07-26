using System.Drawing;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes;

namespace Gondwana.Tests;

/// <summary>
/// Verifies region, frame, tile, cache-facing, and GTS collision-adjust behavior.
/// </summary>
public sealed class TilesheetCollisionAdjustTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        $"GondwanaCollisionAdjustTests_{Guid.NewGuid():N}");

    /// <summary>
    /// Initializes a new test fixture.
    /// </summary>
    public TilesheetCollisionAdjustTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>
    /// Verifies that collision rectangles preserve the engine's existing edge-adjustment calculation.
    /// </summary>
    [Fact]
    public void ApplyTo_DerivesExpectedRectangle()
    {
        var adjust = new CollisionAdjust(
            top: 3,
            bottom: -2,
            left: 4,
            right: -1);

        var result = adjust.ApplyTo(new Rectangle(10, 20, 30, 40));

        Assert.Equal(new Rectangle(14, 23, 25, 35), result);
    }

    /// <summary>
    /// Verifies that assigning the region default overwrites every frame and its cached metadata.
    /// </summary>
    [Fact]
    public void RegionCollisionAdjust_PropagatesToAllFramesAndCache()
    {
        using var tilesheet = CreateRuntimeTilesheet();
        var region = tilesheet.DefaultRegion;

        var initial = new CollisionAdjust(1, -1, 2, -2);
        region.CollisionAdjust = initial;

        Assert.Equal(initial, tilesheet.GetFrame(0, 0).CollisionAdjust);
        Assert.Equal(initial, tilesheet.GetFrame(1, 0).CollisionAdjust);

        var frameOverride = new CollisionAdjust(3, -3, 4, -4);
        var secondFrame = tilesheet.GetFrame(1, 0);
        secondFrame.CollisionAdjust = frameOverride;

        Assert.Equal(frameOverride, tilesheet.GetFrame(1, 0).CollisionAdjust);
        Assert.Equal(
            frameOverride.ApplyTo(new Rectangle(0, 0, 16, 16)),
            tilesheet.GetFrame(1, 0).CollisionArea);

        var replacementDefault = new CollisionAdjust(5, -5, 6, -6);
        region.CollisionAdjust = replacementDefault;

        Assert.Equal(replacementDefault, tilesheet.GetFrame(0, 0).CollisionAdjust);
        Assert.Equal(replacementDefault, tilesheet.GetFrame(1, 0).CollisionAdjust);
    }

    /// <summary>
    /// Verifies that a tile initially inherits its frame adjustment, remains static by default,
    /// and follows subsequent frame adjustments when explicitly enabled.
    /// </summary>
    [Fact]
    public void Tile_FrameCollisionMode_CanRemainStaticOrFollowFrames()
    {
        using var tilesheet = CreateRuntimeTilesheet();
        var region = tilesheet.DefaultRegion;

        var firstAdjust = new CollisionAdjust(1, -1, 2, -2);
        var secondAdjust = new CollisionAdjust(3, -3, 4, -4);
        region.SetFrameCollisionAdjust(0, 0, firstAdjust);
        region.SetFrameCollisionAdjust(1, 0, secondAdjust);

        using var layer = new SceneLayer(1, 1, 16, 16);
        using var sprite = new Sprite(layer, tilesheet.GetFrame(0, 0));

        Assert.Equal(firstAdjust, sprite.AdjustCollisionArea);
        Assert.False(sprite.AdjustCollisionAreaByFrame);

        sprite.CurrentFrame = tilesheet.GetFrame(1, 0);
        Assert.Equal(firstAdjust, sprite.AdjustCollisionArea);

        sprite.AdjustCollisionAreaByFrame = true;
        Assert.Equal(secondAdjust, sprite.AdjustCollisionArea);

        sprite.CurrentFrame = tilesheet.GetFrame(0, 0);
        Assert.Equal(firstAdjust, sprite.AdjustCollisionArea);
    }

    /// <summary>
    /// Verifies GTS region and per-frame values round-trip through JSON.
    /// </summary>
    [Fact]
    public void GtsJson_RoundTripsRegionAndFrameCollisionAdjustments()
    {
        var regionAdjust = new CollisionAdjust(1, -1, 2, -2);
        var frameAdjust = new CollisionAdjust(3, -3, 4, -4);
        var definition = new TilesheetDefinition
        {
            Name = "Sheet",
            Image = new TilesheetImageDefinition { FilePath = "sheet.png" },
            Regions =
            [
                new TilesheetRegionDefinition
                {
                    Name = TilesheetRegion.DefaultRegionName,
                    Area = new Rectangle(0, 0, 32, 16),
                    TileSize = new Size(16, 16),
                    CollisionAdjust = regionAdjust,
                    Frames =
                    [
                        new TilesheetFrameDefinition
                        {
                            XTile = 0,
                            YTile = 0,
                            CollisionAdjust = regionAdjust
                        },
                        new TilesheetFrameDefinition
                        {
                            XTile = 1,
                            YTile = 0,
                            CollisionAdjust = frameAdjust
                        }
                    ]
                }
            ]
        };

        var json = TilesheetDefinitionSerializer.ToJson(definition);
        var loaded = TilesheetDefinitionSerializer.FromJson(json);
        var loadedRegion = Assert.Single(loaded.Regions);

        Assert.Equal(regionAdjust, loadedRegion.CollisionAdjust);
        Assert.Equal(2, loadedRegion.Frames.Count);
        Assert.Equal(
            frameAdjust,
            loadedRegion.Frames[1].CollisionAdjust.GetValueOrDefault());
    }

    /// <summary>
    /// Verifies older GTS JSON without collision properties receives zero-value defaults.
    /// </summary>
    [Fact]
    public void GtsJson_WithoutCollisionMetadata_UsesCompatibleDefaults()
    {
        var definition = new TilesheetDefinition
        {
            Name = "Legacy",
            Image = new TilesheetImageDefinition
            {
                FilePath = "legacy.png"
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

        // Generate structurally valid GTS JSON using the real serializer, then
        // remove metadata that would not exist in an older definition file.
        var jsonObject = JObject.Parse(
            TilesheetDefinitionSerializer.ToJson(definition));

        var regionObject = Assert.IsType<JObject>(
            jsonObject[nameof(TilesheetDefinition.Regions)]![0]);

        regionObject.Remove(
            nameof(TilesheetRegionDefinition.CollisionAdjust));

        regionObject.Remove(
            nameof(TilesheetRegionDefinition.Frames));

        var loaded = TilesheetDefinitionSerializer.FromJson(
            jsonObject.ToString());

        var region = Assert.Single(loaded.Regions);

        Assert.Equal(
            CollisionAdjust.None,
            region.CollisionAdjust);

        Assert.Empty(region.Frames);
    }

    /// <summary>
    /// Verifies runtime-to-GTS conversion writes every frame's final effective value.
    /// </summary>
    [Fact]
    public void FromTilesheet_SerializesEveryEffectiveFrameAdjustment()
    {
        var imagePath = Path.Combine(_tempDir, "sheet.png");
        using (var bitmap = new SKBitmap(32, 16))
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        {
            File.WriteAllBytes(imagePath, data.ToArray());
        }

        using var tilesheet = TilesheetFactory.FromImageFile("Sheet", imagePath);
        var region = tilesheet.DefaultRegion;
        region.TileSize = new Size(16, 16);
        region.CollisionAdjust = new CollisionAdjust(1, -1, 2, -2);
        region.SetFrameCollisionAdjust(1, 0, new CollisionAdjust(3, -3, 4, -4));

        var definition = TilesheetDefinitionSerializer.FromTilesheet(tilesheet);
        var serializedRegion = Assert.Single(definition.Regions);

        Assert.Equal(2, serializedRegion.Frames.Count);
        Assert.Equal(
            region.CollisionAdjust,
            serializedRegion.Frames[0].CollisionAdjust.GetValueOrDefault());
        Assert.Equal(
            region.GetFrameCollisionAdjust(1, 0),
            serializedRegion.Frames[1].CollisionAdjust.GetValueOrDefault());
    }

    private static Tilesheet CreateRuntimeTilesheet()
    {
        var bitmap = new SKBitmap(32, 16);
        var tilesheet = TilesheetFactory.FromBitmap("CollisionSheet", bitmap);
        tilesheet.DefaultRegion.TileSize = new Size(16, 16);
        return tilesheet;
    }
}
