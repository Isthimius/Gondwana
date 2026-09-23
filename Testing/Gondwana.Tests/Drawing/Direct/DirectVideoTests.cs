using System.Drawing;
using System.Runtime.InteropServices;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Video;
using SkiaSharp;

namespace Gondwana.Tests.Drawing.Direct;

public sealed class DirectVideoTests
{
    [Fact]
    public async Task DecoderFramesAreNotVisibleUntilUpdateAndReopenClearsOldFrame()
    {
        using var host = new TestRenderSurfaceHost();
        host.ViewManager.AddView(new Rectangle(0, 0, 2, 2), zOrder: 0);
        var player = new FakeVideoPlayer();
        using var video = new DirectVideo(player, new Uri("file:///test.mp4"), host, host.ViewManager.Views.Single(), new Rectangle(0, 0, 2, 2));
        using var buffer = new BitmapBackbuffer(2, 2);
        await Task.Run(() => player.Emit([0, 0, 255, 0], 1, 1, 4));
        Assert.Equal(new SKColor(0, 0, 0, 0), DrawPixel(video, buffer));
        video.Update(1);
        Assert.Equal(SKColors.Red, DrawPixel(video, buffer));
        await Task.Run(() => player.Emit([255, 0, 0, 0], 1, 1, 4));
        Assert.Equal(SKColors.Red, DrawPixel(video, buffer));
        video.Update(2);
        Assert.Equal(SKColors.Blue, DrawPixel(video, buffer));
        player.Emit([0, 255, 0, 0], 1, 1, 4);
        video.Open(new Uri("file:///next.mp4"));
        Assert.Equal(new SKColor(0, 0, 0, 0), DrawPixel(video, buffer));
        Assert.Equal(2, player.OpenCount);
        video.Dispose();
        video.Dispose();
        Assert.Equal(1, player.DisposeCount);
        Assert.Equal(0, player.FrameSubscribers);
    }

    [Fact]
    public void SceneLayerModeAndFadesUseNormalLifecycle()
    {
        using var host = new TestRenderSurfaceHost();
        var layer = host.Scene.AddLayer(1, 1, 10, 10);
        var player = new FakeVideoPlayer();
        using var video = new DirectVideo(player, new Uri("file:///test.mp4"), host, layer, new Rectangle(4, 5, 8, 8));
        Assert.Equal(DirectDrawingMode.SceneLayer, video.Mode);
        long tick = System.Diagnostics.Stopwatch.GetTimestamp();
        video.Update(tick);
        video.FadeTo(0.5f, 1);
        video.Update(tick += System.Diagnostics.Stopwatch.Frequency);
        Assert.Equal(0.5f, video.Opacity);
        video.FadeOut(1);
        video.Update(tick += System.Diagnostics.Stopwatch.Frequency);
        Assert.False(video.Visible);
        video.FadeIn(1);
        video.Update(tick + System.Diagnostics.Stopwatch.Frequency);
        Assert.Equal(1, video.Opacity);
        video.Pause(); Assert.False(player.IsPlaying);
        video.Play(); Assert.True(player.IsPlaying);
        video.Seek(TimeSpan.FromSeconds(4)); Assert.Equal(TimeSpan.FromSeconds(4), player.Position);
        video.PlaybackRate = 0.5; Assert.Equal(0.5, player.Rate);
        video.Loop = true; Assert.True(player.Loop);
        video.Stop(); Assert.False(player.IsPlaying);
    }

    [Theory]
    [InlineData(StretchMode.Fill, 10, 20, 110, 120)]
    [InlineData(StretchMode.None, 10, 20, 210, 120)]
    [InlineData(StretchMode.Uniform, 10, 45, 110, 95)]
    [InlineData(StretchMode.UniformToFill, -40, 20, 160, 120)]
    public void StretchPreservesExpectedPlacement(StretchMode mode, float left, float top, float right, float bottom)
    {
        Assert.Equal(new SKRect(left, top, right, bottom), DirectVideo.ComputeDestRect(new RectangleF(10, 20, 100, 100), 200, 100, mode));
    }

    [Fact]
    public void UniformToFillClipsToBounds()
    {
        using var host = new TestRenderSurfaceHost();
        host.ViewManager.AddView(new Rectangle(0, 0, 6, 6), zOrder: 0);
        var player = new FakeVideoPlayer();
        using var video = new DirectVideo(player, new Uri("file:///test.mp4"), host, host.ViewManager.Views.Single(), new Rectangle(2, 2, 2, 2));
        video.Stretch = StretchMode.UniformToFill;
        player.Emit([0, 0, 255, 0, 0, 0, 255, 0], 2, 1, 8);
        video.Update(1);
        using var buffer = new BitmapBackbuffer(6, 6);
        buffer.Canvas.Clear(SKColors.Transparent);
        video.Draw(buffer, new RectangleF(2, 2, 2, 2));
        using var snapshot = buffer.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);
        Assert.Equal(new SKColor(0, 0, 0, 0), bitmap.GetPixel(1, 2));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(2, 2));
        Assert.Equal(new SKColor(0, 0, 0, 0), bitmap.GetPixel(4, 2));
    }

    private static SKColor DrawPixel(DirectVideo video, BitmapBackbuffer buffer)
    {
        buffer.Canvas.Clear(SKColors.Transparent);
        video.Draw(buffer, new RectangleF(0, 0, 2, 2));
        using var snapshot = buffer.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);
        return bitmap.GetPixel(0, 0);
    }

    [Fact]
    public void OpacityUsesBasePropertyAndIsAppliedOnlyOnce()
    {
        using var host = new TestRenderSurfaceHost();
        host.ViewManager.AddView(new Rectangle(0, 0, 2, 2), zOrder: 0);
        var view = host.ViewManager.Views.Single();
        var player = new FakeVideoPlayer();
        using var video = new DirectVideo(player, new Uri("file:///test.mp4"), host, view, new Rectangle(0, 0, 2, 2));
        player.Emit(new byte[] { 255, 255, 255, 255 }, 1, 1, 4);
        video.Update(1);
        video.Opacity = 0.5f;
        Assert.Equal(0.5f, ((DirectDrawingBase)video).Opacity);
        using var buffer = new BitmapBackbuffer(2, 2);
        buffer.Canvas.Clear(SKColors.Transparent);
        video.Draw(buffer, new RectangleF(0, 0, 2, 2));
        using var snapshot = buffer.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);
        Assert.InRange(bitmap.GetPixel(0, 0).Alpha, (byte)126, (byte)129);
        video.Opacity = 0;
        Assert.False(video.Visible);
        video.FadeIn(0);
        video.Update(System.Diagnostics.Stopwatch.GetTimestamp() + System.Diagnostics.Stopwatch.Frequency);
        Assert.True(video.Visible);
        Assert.Equal(1, video.Opacity);
    }
}

internal sealed class FakeVideoPlayer : IVideoPlayer
{
    private Stream? _ownedStream;
    public Stream? Stream { get; private set; }
    public int DisposeCount { get; private set; }
    public int OpenCount { get; private set; }
    public int FrameSubscribers => FrameReady?.GetInvocationList().Length ?? 0;
    public double Rate { get; private set; }
    public bool Loop { get; set; }
    public bool IsPlaying { get; private set; }
    public TimeSpan Duration => TimeSpan.Zero;
    public TimeSpan Position { get; private set; }
    public (int width, int height) NaturalSize { get; private set; }
    public bool HasAudio => false;
    public event EventHandler? Started;
    public event EventHandler? Paused;
    public event EventHandler? Stopped;
    public event EventHandler? Ended { add { } remove { } }
    public event EventHandler<VideoStateChangedEventArgs>? StateChanged;
    public event EventHandler<VideoFrameReadyEventArgs>? FrameReady;
    public void Open(Uri source) { _ownedStream?.Dispose(); _ownedStream = null; OpenCount++; StateChanged?.Invoke(this, new("MediaOpened")); }
    public void Open(Stream source, bool leaveOpen = false)
    {
        _ownedStream?.Dispose();
        Stream = source;
        _ownedStream = leaveOpen ? null : source;
        OpenCount++;
        StateChanged?.Invoke(this, new("MediaOpened"));
    }
    public void Play() { IsPlaying = true; Started?.Invoke(this, EventArgs.Empty); }
    public void Pause() { IsPlaying = false; Paused?.Invoke(this, EventArgs.Empty); }
    public void Stop() { IsPlaying = false; Stopped?.Invoke(this, EventArgs.Empty); }
    public void Seek(TimeSpan position) => Position = position;
    public void SetRate(double rate) => Rate = rate;
    public void Dispose() { DisposeCount++; _ownedStream?.Dispose(); }
    public void Emit(byte[] pixels, int width, int height, int stride)
    {
        NaturalSize = (width, height);
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try { FrameReady?.Invoke(this, new(handle.AddrOfPinnedObject(), width, height, stride, 0)); }
        finally { handle.Free(); }
    }
}




