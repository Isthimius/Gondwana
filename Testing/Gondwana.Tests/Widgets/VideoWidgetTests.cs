using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using Gondwana.Assets;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Tests.Drawing.Direct;
using Gondwana.Video;
using Gondwana.Video.Widgets;
using Gondwana.Widgets;
using SkiaSharp;

namespace Gondwana.Tests.Widgets;

public sealed class VideoWidgetTests
{
    private static readonly Rectangle InitialBounds = new(10, 20, 100, 60);
    private static VideoSource Source => VideoSource.FromUri(new Uri("file:///intro.mp4"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstructionAndBoundsUseTheOwnedVideo(bool world)
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        var player = new FakeVideoPlayer();
        int created = 0;
        IVideoPlayer Factory() { created++; return player; }
        using var widget = world
            ? new VideoWidget(host, host.Scene.AddLayer(20, 20, 16, 16), InitialBounds, Source, Factory)
            : new VideoWidget(host, view, InitialBounds, Source, Factory);
        Assert.Equal(1, created);
        Assert.Same(widget.Video, Assert.Single(widget.Children));
        Assert.Equal(world ? DirectDrawingMode.SceneLayer : DirectDrawingMode.View, widget.Mode);
        Assert.Equal(InitialBounds, widget.Bounds);
        Assert.Equal(InitialBounds, world ? widget.WorldBounds : widget.ScreenBounds);
        Assert.True(widget.HitTest(view, new Point(15, 25)));
        Assert.False(widget.HitTest(view, new Point(9, 25)));
        Assert.False(widget.IsDragEnabled);
        Assert.True(widget.CanReceiveFocus && widget.IsKeyboardInputEnabled && widget.IsPointerInputEnabled);
        Assert.Equal(1, player.OpenCount);
        Assert.True(widget.IsPlaying);

        widget.SetBounds(new Rectangle(30, 40, 50, 25));
        widget.SetPosition(60, 70);
        Assert.Equal(new Rectangle(60, 70, 50, 25), widget.Bounds);
        Assert.Equal(new Vector2(60, 70), widget.Video.GetPosition());
        Assert.True(widget.HitTest(view, new Point(65, 75)));
        Assert.False(widget.HitTest(view, new Point(15, 25)));
        Assert.Throws<ArgumentOutOfRangeException>(() => widget.SetBounds(Rectangle.Empty));
        Assert.Equal(new Rectangle(60, 70, 50, 25), widget.Bounds);
    }

    [Fact]
    public void ShowHideFocusAndDisposalUseTheExistingRouterWithoutChangingPlayback()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var router = new WidgetInputRouter(host, null, null, null);
        router.Start();
        var player = new FakeVideoPlayer();
        using var widget = new VideoWidget(host, view, InitialBounds, Source, player);
        var events = new List<string>();
        widget.Shown += () => events.Add("shown");
        widget.Hidden += () => events.Add("hidden");
        widget.Activated += () => events.Add("activated");
        widget.Cancelled += () => events.Add("cancelled");
        widget.FocusGained += () => events.Add("focus");
        widget.FocusLost += () => events.Add("blur");
        Assert.Null(Call(router, "HitTest", new Point(15, 25)));
        widget.Show();
        Assert.NotNull(Call(router, "HitTest", new Point(15, 25)));
        router.Focus(widget);
        widget.Activate();
        widget.Cancel();
        widget.Hide();
        Assert.False(widget.Video.Visible);
        Assert.Null(router.FocusedWidget);
        Assert.Null(Call(router, "HitTest", new Point(15, 25)));
        Assert.True(player.IsPlaying);
        widget.Pause();
        widget.Show();
        Assert.False(widget.IsPlaying);
        Assert.True(widget.Video.Visible);
        Assert.Equal(new[] { "shown", "focus", "activated", "cancelled", "blur", "hidden", "shown" }, events);
        router.Focus(widget);
        int videoDisposed = 0;
        widget.Video.Disposing += (_, _) => videoDisposed++;
        widget.Dispose();
        widget.Dispose();
        Assert.Equal(1, videoDisposed);
        Assert.Equal(1, player.DisposeCount);
        Assert.Equal(0, player.FrameSubscribers);
        Assert.Null(router.FocusedWidget);
        Assert.Null(Call(router, "HitTest", new Point(15, 25)));
        Assert.Empty(widget.Children);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RouterDragMovesVideoAndSuppressesOnlyTheDragClick(bool world)
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        if (world) view.Viewport.Zoom = 2;
        var player = new FakeVideoPlayer();
        using var widget = world
            ? new VideoWidget(host, host.Scene.AddLayer(20, 20, 16, 16), InitialBounds, Source, player)
            : new VideoWidget(host, view, InitialBounds, Source, player);
        using var router = new WidgetInputRouter(host, null, null, null);
        router.Start();
        widget.Show();
        var rect = widget.GetDrawLocationScreen(view);
        var start = new Point((int)rect.Left + 5, (int)rect.Top + 5);
        var end = new Point(start.X + 20, start.Y + 10);
        int clicks = 0;
        var dragEvents = new List<string>();
        widget.PointerClick += _ => clicks++;
        widget.DragStarted += _ => dragEvents.Add("start");
        widget.Dragged += _ => dragEvents.Add("drag");
        widget.DragEnded += _ => dragEvents.Add("end");
        void Gesture()
        {
            Call(router, "ProcessMouseDown", Call(router, "HitTest", start), start, MouseButton.Left, 1L);
            Call(router, "ProcessMouseMove", start, end, 2L);
            Call(router, "ProcessMouseUp", end, MouseButton.Left, 3L);
        }
        Gesture();
        Assert.Equal(InitialBounds, widget.Bounds);
        Assert.Empty(dragEvents);
        Assert.Equal(1, clicks);
        widget.IsDragEnabled = true;
        widget.DragThresholdPx = 4;
        Gesture();
        Assert.Equal(new[] { "start", "drag", "end" }, dragEvents);
        Assert.False(widget.IsDragging);
        Assert.Equal(1, clicks);
        Assert.Equal(new Point(world ? 20 : 30, world ? 25 : 30), widget.Bounds.Location);
        Assert.Equal(widget.GetPosition(), widget.Video.GetPosition());
        Assert.True(widget.IsPlaying); // the wrapper assigns no click playback behavior
    }

    [Fact]
    public void PointerAndKeyboardEventsUseNormalDispatchWithoutPlaybackCommands()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        using var widget = new VideoWidget(host, view, InitialBounds, Source, new FakeVideoPlayer());
        var events = new List<string>();
        widget.PointerEnter += _ => events.Add("enter");
        widget.PointerLeave += _ => events.Add("leave");
        widget.PointerDown += _ => events.Add("down");
        widget.PointerMove += _ => events.Add("move");
        widget.PointerUp += _ => events.Add("up");
        widget.PointerClick += _ => events.Add("click");
        widget.KeyboardInput += _ => events.Add("key");
        using var router = new WidgetInputRouter(host, null, null, null);
        router.Start();
        widget.Show();
        // Reuse the existing Widget tests' private-router entry point pattern.
        var point = new Point(15, 25);
        Call(router, "UpdateMouseHover", Call(router, "HitTest", point), point, 1L);
        Call(router, "ProcessMouseMove", Point.Empty, point, 1L);
        Call(router, "ProcessMouseDown", Call(router, "HitTest", point), point, MouseButton.Left, 2L);
        Call(router, "ProcessMouseMove", point, new Point(16, 25), 3L);
        Call(router, "ProcessMouseUp", new Point(16, 25), MouseButton.Left, 4L);
        Call(router, "ProcessMouseMove", point, new Point(250, 150), 5L);
        Call(router, "UpdateMouseHover", null, new Point(250, 150), 5L);
        Call(router, "RouteKeyboardInput", 32, KeyAction.Pressed, KeyboardModifierState.None);
        foreach (var name in new[] { "enter", "leave", "down", "move", "up", "click", "key" })
            Assert.Contains(name, events);
        Assert.Same(widget, router.FocusedWidget);
        Assert.True(widget.IsPlaying);
    }

    [Fact]
    public void PlaybackAndEventsDelegateToAuthoritativePlayer()
    {
        using var host = new TestRenderSurfaceHost();
        var player = new FakeVideoPlayer();
        using var widget = new VideoWidget(host, AddView(host), InitialBounds, Source, player);
        var events = new List<string>();
        widget.Started += (sender, _) => { Assert.Same(player, sender); events.Add("start"); };
        widget.Paused += (_, _) => events.Add("pause");
        widget.Stopped += (_, _) => events.Add("stop");
        widget.Ended += (_, _) => events.Add("end");
        widget.StateChanged += (_, e) => events.Add(e.State);
        widget.Pause(); Assert.False(widget.IsPlaying);
        widget.Play(); Assert.True(widget.IsPlaying);
        widget.Seek(TimeSpan.FromSeconds(5)); Assert.Equal(player.Position, widget.Position);
        widget.PlaybackRate = 1.5; Assert.Equal(1.5, player.Rate);
        Assert.Equal(widget.Video.PlaybackRate, widget.PlaybackRate);
        widget.Loop = true; Assert.True(player.Loop);
        widget.Stretch = StretchMode.Uniform; Assert.Equal(StretchMode.Uniform, widget.Video.Stretch);
        Assert.False(widget.IsMetadataReady);
        player.Metadata = new(VideoMetadataStatus.Ready, true, TimeSpan.FromSeconds(20));
        player.EmitState("MetadataReady");
        Assert.Same(player.Metadata, widget.Metadata);
        Assert.True(widget.IsMetadataReady && widget.HasAudio);
        Assert.Equal(TimeSpan.FromSeconds(20), widget.Duration);
        player.Emit([0, 0, 255, 255], 1, 1, 4);
        Assert.Equal((1, 1), widget.NaturalSize);
        player.EmitEnded();
        widget.Stop(); Assert.False(widget.IsPlaying);
        Assert.Equal(new[] { "pause", "start", "MetadataReady", "end", "stop" }, events);
        EventHandler removed = (_, _) => throw new Exception("Unsubscribed handler called");
        widget.Started += removed;
        widget.Started -= removed;
        widget.Open(new Uri("file:///replacement.mp4"));
        Assert.Equal(2, player.OpenCount);
        Assert.True(widget.IsPlaying);
    }

    [Fact]
    public void CompositeOpacityFadeAndZOrderApplyToVideoPixels()
    {
        using var host = new TestRenderSurfaceHost();
        var player = new FakeVideoPlayer();
        using var widget = new VideoWidget(host, AddView(host), new Rectangle(0, 0, 2, 2), Source, player);
        player.Emit([0, 0, 255, 0], 1, 1, 4);
        long tick = Stopwatch.GetTimestamp();
        widget.Video.Update(tick);
        widget.SetOpacity(0.5f);
        widget.SetZOrder(17);
        Assert.Equal(17, widget.Video.ZOrder);
        using var buffer = new BitmapBackbuffer(2, 2);
        buffer.Canvas.Clear(SKColors.Transparent);
        widget.Video.Draw(buffer, widget.Bounds);
        using var snapshot = buffer.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);
        Assert.InRange(bitmap.GetPixel(0, 0).Alpha, (byte)126, (byte)129);
        widget.FadeOut(1);
        widget.Video.Update(tick += Stopwatch.Frequency);
        Assert.False(widget.Visible);
        widget.FadeIn(1);
        widget.Video.Update(tick + Stopwatch.Frequency);
        Assert.Equal(1, widget.Video.Opacity);
        Assert.True(widget.Visible);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StreamOwnershipFlowsThroughVideoSource(bool leaveOpen)
    {
        using var host = new TestRenderSurfaceHost();
        using var stream = new MemoryStream([1, 2, 3]);
        var player = new FakeVideoPlayer();
        using var widget = new VideoWidget(host, AddView(host), InitialBounds,
            VideoSource.FromStream(stream, leaveOpen), player);
        Assert.Same(stream, player.Stream);
        widget.Dispose();
        widget.Dispose();
        Assert.Equal(leaveOpen, stream.CanRead);
        Assert.Equal(1, player.DisposeCount);
    }

    [Fact]
    public void GafSourceUsesFreshStreamsAndFailedConstructionCleansUp()
    {
        string path = Path.Combine(Path.GetTempPath(), $"video-widget-{Guid.NewGuid():N}.gaf");
        try
        {
            using var assets = AssetsFile.LoadOrCreate(path, null, false, register: false);
            assets.Add(AssetTypes.Video, "intro.mp4", new MemoryStream([1, 2, 3]));
            using var host = new TestRenderSurfaceHost();
            var view = AddView(host);
            var player = new FakeVideoPlayer();
            var source = VideoSource.FromAsset(assets, "intro");
            using var widget = new VideoWidget(host, view, InitialBounds, source, player);
            var first = player.Stream!;
            Assert.Equal(1, first.ReadByte());
            widget.Open(source);
            Assert.False(first.CanRead);
            Assert.NotSame(first, player.Stream);
            Assert.Equal(1, player.Stream!.ReadByte());
            widget.Dispose();
            Assert.False(player.Stream.CanRead);
            var failedPlayer = new FakeVideoPlayer();
            Assert.Throws<FileNotFoundException>(() => new VideoWidget(host, view, InitialBounds,
                VideoSource.FromAsset(assets, "missing"), failedPlayer));
            Assert.Equal(1, failedPlayer.DisposeCount);
            Assert.Equal(0, failedPlayer.FrameSubscribers);
            Assert.DoesNotContain(DirectDrawingManager.Instance.DirectDrawings, d => ReferenceEquals(d.RenderSurfaceHost, host));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FactoryFailuresLeaveNoRegisteredComposite(bool world)
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        IVideoPlayer Fail() => throw new InvalidOperationException("backend unavailable");
        Assert.Throws<InvalidOperationException>(() => world
            ? new VideoWidget(host, host.Scene.AddLayer(1, 1, 16, 16), InitialBounds, Source, Fail)
            : new VideoWidget(host, view, InitialBounds, Source, Fail));
        Assert.DoesNotContain(DirectDrawingManager.Instance.DirectDrawings, d => ReferenceEquals(d.RenderSurfaceHost, host));
    }

    [Fact]
    public void SceneLayerWidgetHitsWrappedCopies()
    {
        using var host = new TestRenderSurfaceHost();
        var view = AddView(host);
        var layer = host.Scene.AddLayer(2, 2, 16, 16);
        layer.WrapHorizontally = layer.WrapVertically = true;
        using var widget = new VideoWidget(host, layer, new Rectangle(2, 2, 8, 8), Source, new FakeVideoPlayer());
        Assert.True(widget.HitTest(view, new Point(69, 101)));
        Assert.True(widget.HitTest(view, new Point(5, 5)));
        Assert.False(widget.HitTest(view, new Point(15, 15)));
    }

    private static View AddView(TestRenderSurfaceHost host)
    {
        host.ViewManager.AddView(new Rectangle(0, 0, 320, 200), zOrder: 0);
        return host.ViewManager.Views.Single();
    }

    private static object? Call(WidgetInputRouter router, string method, params object?[] args)
        => Call(typeof(WidgetInputRouter), router, method, args);

    private static object? Call(Type type, object target, string method, params object?[] args)
        => type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
}
