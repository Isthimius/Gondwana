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
        video.Update(System.Diagnostics.Stopwatch.Frequency);
        Assert.True(video.Visible);
        Assert.Equal(1, video.Opacity);
    }
}

internal sealed class FakeVideoPlayer : IVideoPlayer
{
    public bool Loop { get; set; }
    public bool IsPlaying { get; private set; }
    public TimeSpan Duration => TimeSpan.Zero;
    public TimeSpan Position { get; private set; }
    public (int width, int height) NaturalSize { get; private set; }
    public bool HasAudio => false;
    public event EventHandler? Started;
    public event EventHandler? Paused;
    public event EventHandler? Stopped;
    public event EventHandler? Ended;
    public event EventHandler<VideoStateChangedEventArgs>? StateChanged;
    public event EventHandler<VideoFrameReadyEventArgs>? FrameReady;
    public void Open(Uri source) => StateChanged?.Invoke(this, new("MediaOpened"));
    public void Play() { IsPlaying = true; Started?.Invoke(this, EventArgs.Empty); }
    public void Pause() { IsPlaying = false; Paused?.Invoke(this, EventArgs.Empty); }
    public void Stop() { IsPlaying = false; Stopped?.Invoke(this, EventArgs.Empty); }
    public void Seek(TimeSpan position) => Position = position;
    public void SetRate(double rate) { }
    public void Dispose() { }
    public void Emit(byte[] pixels, int width, int height, int stride)
    {
        NaturalSize = (width, height);
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try { FrameReady?.Invoke(this, new(handle.AddrOfPinnedObject(), width, height, stride, 0)); }
        finally { handle.Free(); }
    }
}


