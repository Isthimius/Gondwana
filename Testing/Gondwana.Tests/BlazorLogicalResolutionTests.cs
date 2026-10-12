using Gondwana.Blazor.Hosting;
using Gondwana.Rendering;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>Verifies deferred Blazor logical-resolution initialization.</summary>
public sealed class BlazorLogicalResolutionTests
{
    /// <summary>Ignores placeholders and fixes scale only at the first usable size.</summary>
    [Fact]
    public void FirstValidCanvasEstablishesScaleOnce()
    {
        var adapter = new FakeAdapter();
        var applied = new List<float>();
        using var binding = new BlazorLogicalResolutionBinding(adapter, 769, 801, applied.Add);

        Assert.Empty(applied);
        adapter.Resize(0, 0);
        adapter.Resize(1, 1);
        adapter.Resize(360, 375);

        float scale = Assert.Single(applied);
        Assert.True(Math.Round(360 * (double)scale, MidpointRounding.AwayFromZero) >= 769);
        Assert.True(Math.Round(375 * (double)scale, MidpointRounding.AwayFromZero) >= 801);
        adapter.Resize(1000, 1000);
        Assert.Single(applied);
    }

    /// <summary>An already-sized canvas establishes the scale without waiting for an event.</summary>
    /// <param name="width">Canvas width.</param>
    /// <param name="height">Canvas height.</param>
    [Theory]
    [InlineData(800, 600)]
    [InlineData(390, 844)]
    [InlineData(844, 390)]
    [InlineData(1920, 1080)]
    public void AlreadySizedCanvasEstablishesScaleImmediately(int width, int height)
    {
        var adapter = new FakeAdapter(width, height);
        var applied = new List<float>();
        using var binding = new BlazorLogicalResolutionBinding(adapter, 800, 600, applied.Add);

        float scale = Assert.Single(applied);
        Assert.Equal(Math.Max(800f / width, 600f / height), scale);
        adapter.Resize(width + 100, height + 100);
        Assert.Single(applied);
    }

    /// <summary>Disposal before a first paint prevents deferred scale changes.</summary>
    [Fact]
    public void DisposeBeforeFirstPaintUnsubscribes()
    {
        var adapter = new FakeAdapter();
        var applied = new List<float>();
        var binding = new BlazorLogicalResolutionBinding(adapter, 800, 600, applied.Add);
        binding.Dispose();
        binding.Dispose();

        adapter.Resize(800, 600);
        Assert.Empty(applied);
    }

    /// <summary>Invalid logical dimensions are rejected.</summary>
    [Fact]
    public void InvalidDimensionsAreRejected()
    {
        var adapter = new FakeAdapter();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BlazorLogicalResolutionBinding(adapter, 0, 600, _ => { }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BlazorLogicalResolutionBinding(adapter, 800, -1, _ => { }));
    }

    private sealed class FakeAdapter : RenderSurfaceAdapterBase
    {
        internal FakeAdapter(int width = 1, int height = 1)
            : base(width, height, initialSizeAvailable: width > 1 && height > 1) { }

        internal void Resize(int width, int height) => SetDestinationSize(width, height);

        public override void Present(SKImage bufferImage, SKRectI bufferRect, SKRect destRect) { }
    }
}
