using System.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>Regression coverage for persisted sprite-to-Cycle associations.</summary>
[Collection("Global engine state")]
public sealed class SpriteAnimationGsprTests
{
    [Fact]
    public void CycleCloneRetainsHideOnCompletion()
    {
        using var tilesheet = TilesheetRegistry.Instance.LoadFromBitmap(
            "cycle-clone-" + Guid.NewGuid(), new SKBitmap(16, 16));
        using var registered = new Cycle(
            new FrameSequence(tilesheet.GetFrame(0, 0)), .1,
            "cycle-clone-" + Guid.NewGuid(), hideTileOnCycleEnd: true);

        var copy = Cycle.GetAnimationCycle(registered.CycleKey);
        Assert.NotNull(copy);
        Assert.NotSame(registered, copy);
        Assert.True(copy.HideTileOnCycleEnd);
        Assert.Equal(registered.CycleKey, copy.CycleKey);
        Assert.Same(copy, copy.NextCycle);
    }

    [Fact]
    public void RoundTripAssignsIndependentCycleCopiesAndRestoresPlaybackIntent()
    {
        using var sheet = TilesheetRegistry.Instance.LoadFromBitmap(
            "gspr-animation-" + Guid.NewGuid(), new SKBitmap(32, 16));
        sheet.DefaultRegion.TileSize = new Size(16, 16);
        using var scene = new Scene();
        var layer = scene.AddLayer(2, 2, 16, 16);
        var key = "walk-" + Guid.NewGuid();
        using var registered = new Cycle(new FrameSequence([
            sheet.GetFrame(0, 0), sheet.GetFrame(1, 0)]),
            .1, key, hideTileOnCycleEnd: true);
        var originalA = SpriteManager.Instance.CreateSprite(layer, sheet.GetFrame(0, 0), "animated-a");
        var originalB = SpriteManager.Instance.CreateSprite(layer, sheet.GetFrame(0, 0), "animated-b");
        var restored = new List<Sprite>();
        try
        {
            originalA.TileAnimator.StartAnimation(key);
            originalB.TileAnimator.SetCurrentCycle(key);
            Assert.True(originalA.TileAnimator.IsCycling);
            Assert.False(originalB.TileAnimator.IsCycling);

            var definition = SpriteDefinitionSerializer.FromSprites([originalA, originalB]);
            var json = SpriteDefinitionSerializer.ToJson(definition);
            Assert.DoesNotContain("\"Animator\"", json);
            Assert.Contains("\"AnimationKey\"", json);
            var persisted = SpriteDefinitionSerializer.FromJson(json);
            Assert.All(persisted.Sprites, entry => Assert.Equal(key, entry.AnimationKey));
            Assert.True(persisted.Sprites[0].StartAnimation);
            Assert.False(persisted.Sprites[1].StartAnimation);

            restored.AddRange(SpriteDefinitionSerializer.ToSprites(persisted));
            var first = restored[0].TileAnimator;
            var second = restored[1].TileAnimator;
            Assert.True(first.IsCycling);
            Assert.False(second.IsCycling);
            Assert.Equal(key, first.CurrentCycle.CycleKey);
            Assert.NotSame(first.CurrentCycle, second.CurrentCycle);
            Assert.NotSame(registered, first.CurrentCycle);
            Assert.True(first.CurrentCycle.HideTileOnCycleEnd);
            Assert.True(second.CurrentCycle.HideTileOnCycleEnd);
            first.CurrentCycle.Sequence.AdvanceFrame();
            Assert.NotEqual(first.CurrentCycle.Sequence.CurrentFrameIdx,
                second.CurrentCycle.Sequence.CurrentFrameIdx);
        }
        finally
        {
            foreach (var sprite in restored.Append(originalA).Append(originalB))
            {
                SpriteManager.Instance._spriteList.Remove(sprite);
                sprite.DisposeImmediate();
            }
        }
    }

    [Fact]
    public void MissingAnimationRollsBackTheEntireIncomingSpriteCollection()
    {
        using var sheet = TilesheetRegistry.Instance.LoadFromBitmap(
            "gspr-missing-cycle-" + Guid.NewGuid(), new SKBitmap(16, 16));
        using var scene = new Scene();
        var layer = scene.AddLayer(2, 2, 16, 16);
        var key = "known-" + Guid.NewGuid();
        using var registered = new Cycle(new FrameSequence(sheet.GetFrame(0, 0)), .1, key);
        var definition = new SpriteDefinition();
        foreach (var name in new[] { "first", "second" })
            definition.Sprites.Add(new()
            {
                Nickname = name,
                SceneId = scene.ID,
                SceneLayerId = layer.ID,
                Frame = new() { Tilesheet = sheet.Name, RegionName = sheet.DefaultRegion.Name },
                AnimationKey = key,
                StartAnimation = true
            });
        definition.Sprites[1].AnimationKey = "missing-" + Guid.NewGuid();
        var before = SpriteManager.Instance.AllSprites.ToArray();
        Assert.Throws<InvalidDataException>(() => SpriteDefinitionSerializer.ToSprites(definition));
        Assert.Equal(before, SpriteManager.Instance.AllSprites);
    }

    [Fact]
    public void LegacyDefinitionHasNoAnimationAssignment()
    {
        var entry = SpriteDefinitionSerializer.FromJson(
            """{"Sprites":[{"SceneId":"level","SceneLayerId":"actors"}]}""").Sprites.Single();
        Assert.Null(entry.AnimationKey);
        Assert.False(entry.StartAnimation);
    }

    [Fact]
    public void StartAnimationNeedsAnAnimationKey()
    {
        var definition = new SpriteDefinition
        {
            Sprites = [new() { SceneId = "level", SceneLayerId = "actors", StartAnimation = true }]
        };
        Assert.Contains(SpriteDefinitionValidator.Validate(definition),
            error => error.Contains("StartAnimation requires AnimationKey", StringComparison.Ordinal));
    }

    [Fact]
    public void AnimationSourcesRejectKeysThatDifferOnlyByCase()
    {
        var definition = new SpriteDefinition
        {
            AnimationSources =
            [
                SpriteAnimationSourceDefinition.Loose("idle", "idle.gani"),
                SpriteAnimationSourceDefinition.Loose("IDLE", "another.gani")
            ]
        };

        Assert.Contains(SpriteDefinitionValidator.Validate(definition),
            error => error.Contains("duplicate AnimationSources identity", StringComparison.Ordinal));
    }
}
