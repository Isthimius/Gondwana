using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Physics.Collisions;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using Newtonsoft.Json;
using SkiaSharp;

namespace Gondwana.Tests;

[Collection("Global engine state")]
public sealed class SceneTileTransformTests
{
    [Theory]
    [MemberData(nameof(TileTransformGeometryTests.Orientations), MemberType = typeof(TileTransformGeometryTests))]
    public void PlacementMetadataPersistsWithoutChangingSource(TileTransform transform, int left, int top, int right, int bottom, bool swap)
    {
        using var sheet = TilesheetRegistry.Instance.LoadFromBitmap($"transform-{Guid.NewGuid():N}", new SKBitmap(100, 80));
        var region = sheet.DefaultRegion;
        region.TileSize = new Size(40, 20);
        region.TilePadding = new(1, 2, 3, 4);
        region.Overhang = new(1, 2, 3, 4);
        region.CollisionAdjust = new(2, 4, 1, 3);
        region.SetFrameCollisionAdjust(1, 0, new(4, 8, 2, 6));
        using var scene = new Scene();
        var layer = scene.AddLayer(2, 2, 40, 20, coordinateSystem: CoordinateSystemTypes.Orthogonal);
        var tile = layer[0, 0]!;
        tile.CurrentFrame = sheet.GetFrame(0, 0);
        tile.Transform = transform;
        Assert.Equal(new Spacing(left, top, right, bottom), tile.Overhang);
        Assert.Equal(new Spacing(left, top, right, bottom), tile.EffectiveTilePadding);
        Assert.Equal(swap ? new Size(20, 40) : new Size(40, 20), tile.EffectiveTileSize);
        Assert.Equal(new CollisionAdjust(top, bottom, left, right), tile.EffectiveCollisionAdjust);
        Assert.Equal(tile.EffectiveCollisionAdjust.ApplyTo(tile.DrawLocationWorld), tile.CollisionArea);
        Assert.Equal(new Rectangle(0, 0, 40, 20), layer.CoordinateSystem.GetPixelRangeForTile(tile, false));

        tile.AdjustCollisionAreaByFrame = true;
        tile.CurrentFrame = sheet.GetFrame(1, 0); // same setter used by Animator
        Assert.Equal(transform, tile.Transform);
        Assert.Equal(new CollisionAdjust(top * 2, bottom * 2, left * 2, right * 2), tile.EffectiveCollisionAdjust);
        Assert.Equal(new CollisionAdjust(4, 8, 2, 6), tile.CurrentFrame.CollisionAdjust);
        Assert.Equal(new Spacing(1, 2, 3, 4), region.TilePadding);
        Assert.Equal(new Spacing(1, 2, 3, 4), region.Overhang);
        Assert.Equal(new CollisionAdjust(2, 4, 1, 3), region.CollisionAdjust);

        var definition = SceneDefinitionSerializer.FromJson(SceneDefinitionSerializer.ToJson(SceneDefinitionSerializer.FromScene(scene)));
        Assert.Empty(SceneDefinitionValidator.Validate(definition));
        using var restored = SceneDefinitionSerializer.ToScene(definition);
        var loaded = restored.SceneLayers.Single()[0, 0]!;
        Assert.Equal(transform, loaded.Transform);
        Assert.Equal(tile.DrawLocationWorld, loaded.DrawLocationWorld);
        Assert.Equal(tile.CollisionArea, loaded.CollisionArea);
        Assert.Equal(transform, SceneDefinitionSerializer.FromScene(restored).Layers[0].Tiles.Single(t => t.X == 0 && t.Y == 0).Transform);
    }

    [Fact]
    public void OmittedTransformDefaultsToIdentityAndInvalidValuesFailValidation()
    {
        var definition = SceneDefinitionSerializer.FromJson("{\"Layers\":[{\"Columns\":1,\"Rows\":1,\"TileWidth\":16,\"TileHeight\":16,\"Tiles\":[{\"X\":0,\"Y\":0}]}]}");
        Assert.Equal(TileTransform.Identity, definition.Layers[0].Tiles[0].Transform);
        definition.Layers[0].Tiles[0].Transform = (TileTransform)99;
        Assert.Contains(SceneDefinitionValidator.Validate(definition), error => error.Contains("transform"));
    }

    [Theory]
    [InlineData(TileTransform.Identity, 0, 1, 2, 3)]
    [InlineData(TileTransform.Rotate90, 2, 0, 3, 1)]
    [InlineData(TileTransform.Rotate180, 3, 2, 1, 0)]
    [InlineData(TileTransform.Rotate270, 1, 3, 0, 2)]
    [InlineData(TileTransform.FlipHorizontal, 1, 0, 3, 2)]
    [InlineData(TileTransform.FlipVertical, 2, 3, 0, 1)]
    [InlineData(TileTransform.FlipDiagonal, 0, 2, 1, 3)]
    [InlineData(TileTransform.FlipAntiDiagonal, 3, 1, 2, 0)]
    public void RenderingOrientsFourDistinctCornersAndRestoresCanvas(TileTransform transform, int tl, int tr, int bl, int br)
    {
        SKColor[] colors = [SKColors.Red, SKColors.Green, SKColors.Blue, SKColors.Yellow];
        var bitmap = new SKBitmap(40, 20);
        for (int y = 0; y < 20; y++) for (int x = 0; x < 40; x++) bitmap.SetPixel(x, y, colors[(x >= 20 ? 1 : 0) + (y >= 10 ? 2 : 0)]);
        using var sheet = TilesheetRegistry.Instance.LoadFromBitmap($"pixels-{Guid.NewGuid():N}", bitmap);
        sheet.DefaultRegion.TileSize = new(40, 20);
        using var scene = new Scene();
        var tile = scene.AddLayer(1, 1, 40, 20)[0, 0]!;
        tile.CurrentFrame = sheet.GetFrame(0, 0);
        tile.Transform = transform;
        using var backbuffer = new BitmapBackbuffer(100, 100);
        var dest = tile.DrawLocationWorld;
        dest.Offset(20, 30);
        var matrix = backbuffer.Canvas.TotalMatrix;
        tile.Draw(backbuffer, dest);
        Assert.Equal(matrix, backbuffer.Canvas.TotalMatrix);
        using var image = backbuffer.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.Equal(colors[tl], pixels.GetPixel(dest.Left + dest.Width / 4, dest.Top + dest.Height / 4));
        Assert.Equal(colors[tr], pixels.GetPixel(dest.Left + 3 * dest.Width / 4, dest.Top + dest.Height / 4));
        Assert.Equal(colors[bl], pixels.GetPixel(dest.Left + dest.Width / 4, dest.Top + 3 * dest.Height / 4));
        Assert.Equal(colors[br], pixels.GetPixel(dest.Left + 3 * dest.Width / 4, dest.Top + 3 * dest.Height / 4));
    }

    [Fact]
    public void CullingFindsRotatedVisualFarOutsideItsCellAndSerializationRebuildsIndex()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 100, 10, coordinateSystem: CoordinateSystemTypes.Orthogonal);
        layer[0, 0]!.Transform = TileTransform.Rotate90;
        var query = new Rectangle(48, -40, 2, 2);
        Assert.Contains(layer[0, 0], layer.CoordinateSystem.GetSceneLayerTilesInPixelRange(layer, query, true));
        Assert.Empty(layer.CoordinateSystem.GetSceneLayerTilesInPixelRange(layer, query, false));
        using var restored = JsonConvert.DeserializeObject<Scene>(JsonConvert.SerializeObject(scene, EngineState.JsonSerializerSettings), EngineState.JsonSerializerSettings)!;
        var restoredLayer = restored.SceneLayers.Single();
        Assert.Contains(restoredLayer[0, 0], restoredLayer.CoordinateSystem.GetSceneLayerTilesInPixelRange(restoredLayer, query, true));
        layer[0, 0]!.Transform = TileTransform.Identity;
        Assert.Empty(layer.TransformedTiles);
    }
    [Fact]
    public void TransformInvalidatesOldAndNewBounds()
    {
        Engine.Instance.EngineDispatcher.BindToCurrentThread();
        Engine.Instance.EngineDispatcher.Drain();
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 40, 20);
        var tile = layer[0, 0]!;
        layer.RefreshQueue.ClearRefreshQueue();
        var before = tile.DrawLocationWorld;
        tile.Transform = TileTransform.Rotate90;
        Assert.Contains(before, layer.RefreshQueue.WorldRects);
        Assert.Contains(tile.DrawLocationWorld, layer.RefreshQueue.WorldRects);
        tile.AdjustCollisionArea = new(1, 2, 3, 4);
        Assert.Equal(new CollisionAdjust(3, 4, 2, 1), tile.EffectiveCollisionAdjust);
    }

    [Theory]
    [MemberData(nameof(SceneLayerWrappingTests.Projections), MemberType = typeof(SceneLayerWrappingTests))]
    public void EveryProjectionKeepsCellOutlineAndWrapsTransformedBounds(CoordinateSystemTypes projection)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 6, 100, 10, coordinateSystem: projection);
        var tile = layer[1, 2]!;
        var cell = layer.CoordinateSystem.GetPixelRangeForTile(tile, false);
        var outline = tile.OutlinePointsWorld;
        tile.Transform = TileTransform.Rotate90;
        Assert.Equal(outline, tile.OutlinePointsWorld);
        Assert.Equal(TileTransformGeometry.GetVisualBounds(cell, Spacing.None, tile.Transform), tile.DrawLocationWorld);
        Assert.True(layer.GetLayerBoundsPx().Contains(tile.DrawLocationWorld));
        var query = new Rectangle(tile.DrawLocationWorld.Left + 1, tile.DrawLocationWorld.Top + 1, 2, 2);
        Assert.Contains(tile, layer.CoordinateSystem.GetSceneLayerTilesInPixelRange(layer, query, true));
        layer.WrapHorizontally = layer.WrapVertically = true;
        var offset = layer.GetPeriod().Offset(-2, 3);
        var wrappedBounds = tile.DrawLocationWorld;
        wrappedBounds.Offset(Point.Round(offset));
        Assert.Contains(layer.GetDrawablesInWorldRect(wrappedBounds).Cast<WrappedDrawable>(),
            draw => ReferenceEquals(draw.Owner, tile) && draw.Offset == offset);
    }

    [Fact]
    public void AnimatorRetainsOrientationWhileFollowingFrameCollisionMetadata()
    {
        Gondwana.Timers.EngineSimulationClock.BeginTimerDriven(0);
        try
        {
            using var sheet = TilesheetRegistry.Instance.LoadFromBitmap($"animated-transform-{Guid.NewGuid():N}", new SKBitmap(32, 16));
            sheet.DefaultRegion.TileSize = new(16, 16);
            sheet.DefaultRegion.SetFrameCollisionAdjust(1, 0, new(1, 2, 3, 4));
            sheet.DefaultRegion.SetFrameCollisionType(1, 0, TileCollisionType.Trigger);
            using var scene = new Scene();
            var tile = scene.AddLayer(1, 1, 16, 16)[0, 0]!;
            tile.CurrentFrame = sheet.GetFrame(0, 0);
            tile.Transform = TileTransform.Rotate90;
            tile.AdjustCollisionAreaByFrame = tile.CollisionTypeByFrame = true;
            tile.EnableAnimator = true;
            using var cycle = new Gondwana.Drawing.Animation.Cycle(
                new Gondwana.Drawing.Animation.FrameSequence([sheet.GetFrame(0, 0), sheet.GetFrame(1, 0)]), .1, Guid.NewGuid().ToString());
            tile.TileAnimator.CurrentCycle = cycle;
            tile.TileAnimator.StartAnimation();
            tile.TileAnimator.CycleAnimation((long)(.11 * Gondwana.Timers.HighResTimer.TicksPerSecond));
            Assert.Equal(1, tile.CurrentFrame.XTile);
            Assert.Equal(TileTransform.Rotate90, tile.Transform);
            Assert.Equal(new CollisionAdjust(3, 4, 2, 1), tile.EffectiveCollisionAdjust);
            Assert.Equal(TileCollisionType.Trigger, tile.CollisionType);
        }
        finally { Gondwana.Timers.EngineSimulationClock.UseWallClock(); }
    }}
