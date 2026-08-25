using System.Drawing;
using Gondwana.Effects;
using Gondwana.Physics.Movement.Easing;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Tests.Effects;

public sealed class EffectsManagerTests
{
    [Fact]
    public void FadeOut_View_AdvancesAndCompletes()
    {
        using var host = CreateHost(out View view, out _);
        host.Scene.FullRefreshNeeded = false;

        var effect = host.Effects.Run(view, new FadeOutEffect(1f));

        Assert.Equal(EffectStatus.Running, effect.Status);
        Assert.Single(host.Effects.ActiveEffects);
        Assert.True(host.Scene.FullRefreshNeeded);

        host.Effects.Advance(0.5f);

        Assert.Equal(0.5f, view.EffectOpacity, 3);
        Assert.Equal(0.5f, effect.Progress, 3);

        host.Effects.Advance(0.5f);

        Assert.Equal(EffectStatus.Completed, effect.Status);
        Assert.Equal(0f, view.EffectOpacity);
        Assert.Empty(host.Effects.ActiveEffects);
    }

    [Fact]
    public void FadeOut_SceneLayer_DoesNotChangeViewOpacity()
    {
        using var host = CreateHost(out View view, out SceneLayer layer);

        host.Effects.Run(layer, new FadeOutEffect(1f));
        host.Effects.Advance(0.25f);

        Assert.Equal(1f, view.EffectOpacity);
        Assert.Equal(0.75f, layer.EffectOpacity, 3);
    }

    [Fact]
    public void Run_ReplacesEffectOnSameTargetAndChannel()
    {
        using var host = CreateHost(out View view, out _);
        var first = host.Effects.Run(view, new FadeOutEffect(1f));

        host.Effects.Advance(0.25f);
        var replacement = host.Effects.Run(view, new FadeInEffect(1f));

        Assert.Equal(EffectStatus.Cancelled, first.Status);
        Assert.Equal(EffectStatus.Running, replacement.Status);
        Assert.Single(host.Effects.ActiveEffects);
        Assert.Equal(0.75f, view.EffectOpacity, 3);

        host.Effects.Advance(0.5f);

        Assert.Equal(0.875f, view.EffectOpacity, 3);
    }

    [Fact]
    public void CompatibleChannels_ComposeOnOneTarget()
    {
        using var host = CreateHost(out View view, out _);

        host.Effects.Run(view, new FadeOutEffect(1f));
        host.Effects.Run(
            view,
            new SlideOutEffect(
                EffectDirection.FromLeftToRight,
                1f,
                EasingKind.Linear));

        Assert.Equal(2, host.Effects.ActiveEffects.Count);

        host.Effects.Advance(0.5f);

        Assert.Equal(0.5f, view.EffectOpacity, 3);
        Assert.Equal(0.5f, view.EffectOffsetFactor.X, 3);
    }

    [Fact]
    public void Cancel_RestoresStateFromBeforeEffect()
    {
        using var host = CreateHost(out View view, out _);
        var effect = host.Effects.Run(
            view,
            new SlideOutEffect(
                EffectDirection.FromTopToBottom,
                1f,
                EasingKind.Linear));

        host.Effects.Advance(0.5f);
        effect.Cancel();

        Assert.Equal(EffectStatus.Cancelled, effect.Status);
        Assert.Equal(PointF.Empty, view.EffectOffsetFactor);
        Assert.Empty(host.Effects.ActiveEffects);
    }

    [Fact]
    public void Slide_ChangesPresentationCoordinatesWithoutChangingWorldState()
    {
        using var host = CreateHost(out View view, out SceneLayer layer);
        PointF world = new(20f, 10f);

        host.Effects.Run(
            view,
            new SlideOutEffect(
                EffectDirection.FromLeftToRight,
                1f,
                EasingKind.Linear));
        host.Effects.Advance(0.5f);

        PointF screen = view.WorldPxToScreenPx(layer, world);
        PointF roundTrip = view.ScreenPxToWorldPx(layer, screen);

        Assert.Equal(120f, screen.X, 3);
        Assert.Equal(world.X, roundTrip.X, 3);
        Assert.Equal(world.Y, roundTrip.Y, 3);
        Assert.Equal(PointF.Empty, view.Camera.PositionPx);
        Assert.Equal(Point.Empty, layer.OriginPx);
    }

    [Fact]
    public void FillAndErase_UpdateRevealState()
    {
        using var host = CreateHost(out _, out SceneLayer layer);

        host.Effects.Run(
            layer,
            new FillEffect(EffectDirection.FromRightToLeft, 1f));
        Assert.Equal(0f, layer.EffectReveal);

        host.Effects.Advance(1f);
        Assert.Equal(1f, layer.EffectReveal);

        host.Effects.Run(
            layer,
            new EraseEffect(EffectDirection.FromTopToBottom, 1f));
        host.Effects.Advance(0.5f);

        Assert.Equal(0.5f, layer.EffectReveal, 3);
        Assert.Equal(EffectDirection.FromTopToBottom, layer.EffectRevealDirection);
    }

    [Fact]
    public void Earthquake_IsViewOnlyAndResetsItsOffsetAtCompletion()
    {
        using var host = CreateHost(out View view, out SceneLayer layer);
        var effect = host.Effects.Run(
            view,
            new EarthquakeEffect(1f, intensityPx: 10f, randomSeed: 7));

        host.Effects.Advance(0.25f);

        Assert.NotEqual(PointF.Empty, view.EffectOffsetPx);
        Assert.Throws<ArgumentException>(() =>
            host.Effects.Run(layer, new EarthquakeEffect(1f)));

        host.Effects.Advance(0.75f);

        Assert.Equal(EffectStatus.Completed, effect.Status);
        Assert.Equal(PointF.Empty, view.EffectOffsetPx);
    }

    [Fact]
    public void Zoom_DelegatesAnimationToViewportWithoutAdvancingItTwice()
    {
        using var host = CreateHost(out View view, out _);
        var effect = host.Effects.Run(view, new ZoomInEffect(2f, 1f));

        host.Effects.Advance(0.5f);

        Assert.Equal(1f, view.Viewport.Zoom);
        Assert.True(view.Viewport.IsZoomAnimating);

        view.Update(0.5f);

        Assert.InRange(view.Viewport.Zoom, 1f, 2f);
        Assert.NotEqual(1f, view.Viewport.Zoom);
        Assert.Equal(EffectStatus.Running, effect.Status);

        host.Effects.Advance(0.5f);

        Assert.Equal(EffectStatus.Completed, effect.Status);
        Assert.Equal(2f, view.Viewport.Zoom);
        Assert.False(view.Viewport.IsZoomAnimating);
    }

    [Fact]
    public void Run_RejectsTargetOwnedByAnotherHost()
    {
        using var host = CreateHost(out _, out _);
        using var other = CreateHost(out View otherView, out _);

        Assert.Throws<ArgumentException>(() =>
            host.Effects.Run(otherView, new FadeOutEffect(1f)));
    }

    [Theory]
    [InlineData(EffectDirection.FromLeftToRight, 0f, 0f, 25f, 50f)]
    [InlineData(EffectDirection.FromRightToLeft, 75f, 0f, 25f, 50f)]
    [InlineData(EffectDirection.FromTopToBottom, 0f, 0f, 100f, 12.5f)]
    [InlineData(EffectDirection.FromBottomToTop, 0f, 37.5f, 100f, 12.5f)]
    public void RevealGeometry_UsesRequestedDirection(
        EffectDirection direction,
        float x,
        float y,
        float width,
        float height)
    {
        RectangleF actual = EffectGeometry.GetRevealRect(
            new RectangleF(0f, 0f, 100f, 50f),
            direction,
            0.25f);

        Assert.Equal(new RectangleF(x, y, width, height), actual);
    }

    private static TestRenderSurfaceHost CreateHost(
        out View view,
        out SceneLayer layer)
    {
        var host = new TestRenderSurfaceHost();
        layer = host.Scene.AddLayer(10, 10, width: 32, height: 32);
        host.ViewManager.AddView(new Rectangle(0, 0, 200, 100));
        view = Assert.Single(host.ViewManager.Views);
        return host;
    }
}
