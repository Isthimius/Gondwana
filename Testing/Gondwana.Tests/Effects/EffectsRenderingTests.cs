using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Effects;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Tests.Effects;

/// <summary>
/// Contains regression tests for effects rendering.
/// </summary>
[Collection("Effects rendering")]
public sealed class EffectsRenderingTests
{
    /// <summary>
    /// Initializes a new instance of the <c>EffectsRenderingTests</c> class.
    /// </summary>
    public EffectsRenderingTests()
    {
        Engine.Instance.EngineDispatcher.BindToCurrentThread();
        Engine.Instance.EngineDispatcher.Drain();
    }

    /// <summary>
    /// Verifies bitmap path composites view opacity.
    /// </summary>
    [Fact]
    public void BitmapPath_CompositesViewOpacity()
    {
        AssertViewOpacityIsComposited<BitmapBackbuffer>();
    }

    /// <summary>
    /// Verifies gpu full frame path composites view opacity.
    /// </summary>
    [Fact]
    public void GpuFullFramePath_CompositesViewOpacity()
    {
        // GpuBackbuffer uses its CPU fallback surface until a GRContext is attached,
        // while still exercising the GL-thread/full-frame host path.
        AssertViewOpacityIsComposited<GpuBackbuffer>();
    }

    /// <summary>
    /// Verifies bitmap path composites scene layer opacity.
    /// </summary>
    [Fact]
    public void BitmapPath_CompositesSceneLayerOpacity()
    {
        AssertSceneLayerOpacityIsComposited<BitmapBackbuffer>();
    }

    /// <summary>
    /// Verifies gpu full frame path composites scene layer opacity.
    /// </summary>
    [Fact]
    public void GpuFullFramePath_CompositesSceneLayerOpacity()
    {
        AssertSceneLayerOpacityIsComposited<GpuBackbuffer>();
    }

    private static void AssertViewOpacityIsComposited<TBackbuffer>()
        where TBackbuffer : BackbufferBase
    {
        using var scene = new Scene();
        using var host = new RenderSurfaceHost<TBackbuffer>(new TestAdapter(100, 50));
        host.Bind(scene, limitCameraToWorldBoundPx: false);

        var view = Assert.Single(host.ViewManager.Views);
        using var rectangle = new DirectRectangle(
            Color.Red,
            host,
            view,
            new Rectangle(0, 0, 100, 50),
            nickname: $"effects-render-{typeof(TBackbuffer).Name}")
            .SetFilled(true);

        host.Effects.Run(view, new FadeOutEffect(1f));
        host.Effects.Advance(0.5f);

        host.RenderToBackbuffer(tick: 0);
        host.Backbuffer.EndFrame();

        using SKImage image = host.Backbuffer.Snapshot();
        using SKBitmap bitmap = SKBitmap.FromImage(image);
        SKColor pixel = bitmap.GetPixel(50, 25);

        Assert.InRange(pixel.Red, (byte)120, (byte)135);
        Assert.Equal((byte)0, pixel.Green);
        Assert.Equal((byte)0, pixel.Blue);
        Assert.Equal((byte)255, pixel.Alpha);

        host.Backbuffer.BeginFrame();
    }

    private static void AssertSceneLayerOpacityIsComposited<TBackbuffer>()
        where TBackbuffer : BackbufferBase
    {
        using var scene = new Scene();
        SceneLayer layer = scene.AddLayer(4, 2, width: 25, height: 25);
        using var host = new RenderSurfaceHost<TBackbuffer>(new TestAdapter(100, 50));
        host.Bind(scene, limitCameraToWorldBoundPx: false);

        using var rectangle = new DirectRectangle(
            Color.Red,
            host,
            layer,
            new Rectangle(0, 0, 100, 50),
            nickname: $"effects-layer-render-{typeof(TBackbuffer).Name}")
            .SetFilled(true);

        host.Effects.Run(layer, new FadeOutEffect(1f));
        host.Effects.Advance(0.5f);

        host.RenderToBackbuffer(tick: 0);
        host.Backbuffer.EndFrame();

        using SKImage image = host.Backbuffer.Snapshot();
        using SKBitmap bitmap = SKBitmap.FromImage(image);
        SKColor pixel = bitmap.GetPixel(50, 25);

        Assert.InRange(pixel.Red, (byte)120, (byte)135);
        Assert.Equal((byte)0, pixel.Green);
        Assert.Equal((byte)0, pixel.Blue);
        Assert.Equal((byte)255, pixel.Alpha);

        host.Backbuffer.BeginFrame();
    }

    private sealed class TestAdapter(int width, int height)
        : RenderSurfaceAdapterBase(width, height)
    {
        /// <inheritdoc/>
        public override void Present(
            SKImage bufferImage,
            SKRectI bufferRect,
            SKRect destRect)
        {
        }
    }
}

/// <summary>
/// Represents effects rendering collection.
/// </summary>
[CollectionDefinition("Effects rendering", DisableParallelization = true)]
public sealed class EffectsRenderingCollection;
