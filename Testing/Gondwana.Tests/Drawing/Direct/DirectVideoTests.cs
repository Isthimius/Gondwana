using System.Drawing;
using System.Runtime.InteropServices;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Video;
using SkiaSharp;

namespace Gondwana.Tests.Drawing.Direct;

/// <summary>
/// Contains regression tests for direct video.
/// </summary>
public sealed class DirectVideoTests
{
    /// <summary>
    /// Verifies decoder frames are not visible until update and reopen clears old frame.
    /// </summary>
    /// <returns>A task that represents completion of the operation.</returns>
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

    /// <summary>
    /// Verifies scene layer mode and fades use normal lifecycle.
    /// </summary>
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

    /// <summary>
    /// Verifies stretch preserves expected placement.
    /// </summary>
    /// <param name="mode">The mode value for this test case.</param>
    /// <param name="left">The left value for this test case.</param>
    /// <param name="top">The top value for this test case.</param>
    /// <param name="right">The right value for this test case.</param>
    /// <param name="bottom">The bottom value for this test case.</param>
    [Theory]
    [InlineData(StretchMode.Fill, 10, 20, 110, 120)]
    [InlineData(StretchMode.None, 10, 20, 210, 120)]
    [InlineData(StretchMode.Uniform, 10, 45, 110, 95)]
    [InlineData(StretchMode.UniformToFill, -40, 20, 160, 120)]
    public void StretchPreservesExpectedPlacement(StretchMode mode, float left, float top, float right, float bottom)
    {
        Assert.Equal(new SKRect(left, top, right, bottom), DirectVideo.ComputeDestRect(new RectangleF(10, 20, 100, 100), 200, 100, mode));
    }

    /// <summary>
    /// Verifies uniform to fill clips to bounds.
    /// </summary>
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

    /// <summary>
    /// Verifies opacity uses base property and is applied only once.
    /// </summary>
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
    /// <summary>
    /// Gets the stream.
    /// </summary>
    public Stream? Stream { get; private set; }
    /// <summary>
    /// Gets the dispose count.
    /// </summary>
    public int DisposeCount { get; private set; }
    /// <summary>
    /// Gets the open count.
    /// </summary>
    public int OpenCount { get; private set; }
    /// <summary>
    /// Gets the frame subscribers.
    /// </summary>
    public int FrameSubscribers => FrameReady?.GetInvocationList().Length ?? 0;
    /// <summary>
    /// Gets the rate.
    /// </summary>
    public double Rate { get; private set; }
    /// <inheritdoc/>
    public bool Loop { get; set; }
    /// <inheritdoc/>
    public bool IsPlaying { get; private set; }
    /// <inheritdoc/>
    public VideoMetadata Metadata { get; set; } = VideoMetadata.Unavailable;
    /// <inheritdoc/>
    public TimeSpan Duration => Metadata.Duration;
    /// <inheritdoc/>
    public TimeSpan Position { get; private set; }
    /// <inheritdoc/>
    public (int width, int height) NaturalSize { get; private set; }
    /// <inheritdoc/>
    public bool HasAudio => Metadata.HasAudio;
    /// <inheritdoc/>
    public event EventHandler? Started;
    /// <inheritdoc/>
    public event EventHandler? Paused;
    /// <inheritdoc/>
    public event EventHandler? Stopped;
    /// <inheritdoc/>
    public event EventHandler? Ended;
    /// <inheritdoc/>
    public event EventHandler<VideoStateChangedEventArgs>? StateChanged;
    /// <inheritdoc/>
    public event EventHandler<VideoFrameReadyEventArgs>? FrameReady;
    /// <inheritdoc/>
    public void Open(Uri source) { _ownedStream?.Dispose(); _ownedStream = null; OpenCount++; StateChanged?.Invoke(this, new("MediaOpened")); }
    /// <inheritdoc/>
    public void Open(Stream source, bool leaveOpen = false)
    {
        _ownedStream?.Dispose();
        Stream = source;
        _ownedStream = leaveOpen ? null : source;
        OpenCount++;
        StateChanged?.Invoke(this, new("MediaOpened"));
    }
    /// <inheritdoc/>
    public void Play() { IsPlaying = true; Started?.Invoke(this, EventArgs.Empty); }
    /// <inheritdoc/>
    public void Pause() { IsPlaying = false; Paused?.Invoke(this, EventArgs.Empty); }
    /// <inheritdoc/>
    public void Stop() { IsPlaying = false; Stopped?.Invoke(this, EventArgs.Empty); }
    /// <inheritdoc/>
    public void Seek(TimeSpan position) => Position = position;
    /// <inheritdoc/>
    public void SetRate(double rate) => Rate = rate;
    /// <summary>
    /// Raises the simulated video-ended event.
    /// </summary>
    public void EmitEnded() => Ended?.Invoke(this, EventArgs.Empty);
    /// <summary>
    /// Raises a simulated playback-state change.
    /// </summary>
    /// <param name="state">The state value for this test case.</param>
    public void EmitState(string state) => StateChanged?.Invoke(this, new(state));
    /// <summary>
    /// Releases the resources owned by this instance.
    /// </summary>
    public void Dispose() { DisposeCount++; _ownedStream?.Dispose(); }
    /// <summary>
    /// Publishes a simulated video frame.
    /// </summary>
    /// <param name="pixels">The pixels value for this test case.</param>
    /// <param name="width">The width value for this test case.</param>
    /// <param name="height">The height value for this test case.</param>
    /// <param name="stride">The stride value for this test case.</param>
    public void Emit(byte[] pixels, int width, int height, int stride)
    {
        NaturalSize = (width, height);
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try { FrameReady?.Invoke(this, new(handle.AddrOfPinnedObject(), width, height, stride, 0)); }
        finally { handle.Free(); }
    }
}
