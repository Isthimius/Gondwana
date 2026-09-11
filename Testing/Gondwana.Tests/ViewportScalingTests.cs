using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Input.Touch;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Widgets;
using SkiaSharp;

namespace Gondwana.Tests;

[Collection("Global engine state")]
public sealed class ViewportScalingTests : IDisposable
{
    public ViewportScalingTests()
    {
        Engine.Instance.EngineDispatcher.BindToCurrentThread();
        Engine.Instance.EngineDispatcher.Drain();
        Engine.Instance.Configuration.RenderScale = 1;
    }

    public void Dispose()
    {
        MouseEventPoller.Reset();
        TouchEventPoller.Reset();
        Engine.Instance.Configuration.RenderScale = 1;
        Engine.Instance.Configuration.RenderScalingFilter = RenderScalingFilter.Linear;
    }

    [Theory]
    [InlineData(3840, 2160, 0.5f, 1920, 1080, 2f)]
    [InlineData(1920, 1080, 1f, 1920, 1080, 1f)]
    [InlineData(1920, 1080, 2f, 3840, 2160, 0.5f)]
    [InlineData(3, 5, 0.5f, 2, 3, 1.5f)]
    [InlineData(1, 1, 0.001f, 1, 1, 1f)]
    public void EstablishesLogicalResolution(int w, int h, float scale, int bw, int bh, float presentation)
    {
        Engine.Instance.Configuration.RenderScale = scale;
        using var host = new RenderSurfaceHost<BitmapBackbuffer>(new Adapter(w, h));
        using var buffer = host.Backbuffer;
        Assert.Equal((bw, bh), (buffer.Width, buffer.Height));
        Assert.Equal(presentation, host.PresentationScale, 5);
        using var scene = new Scene();
        host.Bind(scene, false);
        Assert.Equal(new Rectangle(0, 0, bw, bh), Assert.Single(host.ViewManager.Views).Viewport.TargetRectPx);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void RejectsInvalidIntent(float scale)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Engine.Instance.Configuration.RenderScale = scale);

    [Fact]
    public void ResizePreservesBufferCanvasViewsAndCamera_ExplicitScaleUsesCurrentAdapter()
    {
        var adapter = new Adapter(1920, 1080);
        using var host = new RenderSurfaceHost<BitmapBackbuffer>(adapter);
        using var buffer = host.Backbuffer;
        using var scene = new Scene();
        host.Bind(scene, false);
        var view = Assert.Single(host.ViewManager.Views);
        view.Viewport.Zoom = 2;
        var canvas = buffer.Canvas;
        var originalViewport = view.Viewport.TargetRectPx;
        var camera = view.Camera.PositionPx;
        int sizeChanges = 0;
        buffer.SizeChanged += (_, _) => sizeChanges++;
        foreach (var size in new[] { (3840, 2160), (1600, 1000), (0, 0), (900, 1600), (1600, 1000) })
        {
            adapter.Resize(size.Item1, size.Item2);
            buffer.BeginFrame();
            Assert.Same(buffer, host.Backbuffer);
            Assert.Same(canvas, buffer.Canvas);
            Assert.Equal((1920, 1080), (buffer.Width, buffer.Height));
            Assert.Equal(originalViewport, view.Viewport.TargetRectPx);
            Assert.Equal(camera, view.Camera.PositionPx);
            Assert.Equal(2, view.Viewport.Zoom);
        }
        Assert.Equal(0, sizeChanges);
        Assert.Equal(5f / 6, host.PresentationScale, 5);
        Assert.Equal(new SKRect(0, 50, 1600, 950), adapter.Presentation.DestinationRect);
        Engine.Instance.Configuration.RenderScale = 0.5f;
        buffer.BeginFrame();
        Assert.Equal((800, 500), (buffer.Width, buffer.Height));
        Assert.Equal(1, sizeChanges);
        Assert.Equal(2, host.PresentationScale);
        Assert.Equal(new Rectangle(0, 0, 800, 500), view.Viewport.TargetRectPx);
    }

    [Fact]
    public void DeferredInitialLayoutIsEstablishedOnce_ExplicitChangeWhileMinimizedIsDeferred()
    {
        Engine.Instance.Configuration.RenderScale = 0.5f;
        var adapter = new Adapter(1, 1, false);
        using var host = new RenderSurfaceHost<BitmapBackbuffer>(adapter);
        using var buffer = host.Backbuffer;
        adapter.Resize(1920, 1080);
        buffer.BeginFrame();
        Assert.Equal((960, 540), (buffer.Width, buffer.Height));
        adapter.Resize(1600, 1000);
        buffer.BeginFrame();
        Assert.Equal((960, 540), (buffer.Width, buffer.Height));
        adapter.Resize(0, 0);
        Engine.Instance.Configuration.RenderScale = 2;
        adapter.Resize(800, 600);
        buffer.BeginFrame();
        Assert.Equal((1600, 1200), (buffer.Width, buffer.Height));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 720)]
    [InlineData(1280, 0)]
    public void RepeatedUnavailableDimensionsDoNotRaiseResize(int width, int height)
    {
        var adapter = new Adapter(1, 1, false);
        int events = 0;
        adapter.Resized += _ => events++;
        adapter.Resize(width, height);
        Assert.Equal(1, events);
        adapter.Resize(width, height);
        adapter.Resize(width, height);
        Assert.Equal(1, events);
        Assert.False(adapter.InitialSizeAvailable);
    }

    [Fact]
    public void FirstValidLayoutMatchingPlaceholderEstablishesResolutionOnce()
    {
        Engine.Instance.Configuration.RenderScale = 2;
        var adapter = new Adapter(1, 1, false);
        using var host = new RenderSurfaceHost<BitmapBackbuffer>(adapter);
        using var buffer = host.Backbuffer;
        int events = 0;
        adapter.Resized += _ => events++;
        adapter.Resize(1, 1);
        buffer.BeginFrame();
        Assert.True(adapter.InitialSizeAvailable);
        Assert.Equal((2, 2), (buffer.Width, buffer.Height));
        adapter.Resize(1, 1);
        Assert.Equal(1, events);
    }

    [Theory]
    [InlineData(0.5f, 2f)]
    [InlineData(2f, 0.5f)]
    public void AdapterToScreenToWorldToGrid_IsIndependentOfZoom(float scale, float zoom)
    {
        Engine.Instance.Configuration.RenderScale = scale;
        var adapter = new Adapter(800, 600);
        using var host = new RenderSurfaceHost<BitmapBackbuffer>(adapter);
        using var buffer = host.Backbuffer;
        using var scene = new Scene();
        var layer = scene.AddLayer(100, 100, width: 32, height: 32);
        host.Bind(scene, false);
        var view = Assert.Single(host.ViewManager.Views);
        view.Viewport.Zoom = zoom;
        view.Camera.SnapTo(new PointF(32, 16));
        var world = new PointF(96, 80);
        var logical = view.WorldPxToScreenPx(layer, world);
        foreach (var size in new[] { (800, 600), (1600, 1000), (400, 300) })
        {
            adapter.Resize(size.Item1, size.Item2);
            var t = adapter.Presentation;
            var physical = new PointF(t.DestinationRect.Left + logical.X * t.Scale,
                                      t.DestinationRect.Top + logical.Y * t.Scale);
            Assert.True(t.TryAdapterPxToScreenPx(physical, out var screen));
            var restored = view.ScreenPxToWorldPx(layer, screen);
            Assert.Equal(world.X, restored.X, 3);
            Assert.Equal(world.Y, restored.Y, 3);
            var grid = view.ScreenPxToGrid(layer, screen);
            Assert.Equal(3f, grid.X, 3);
            Assert.Equal(2.5f, grid.Y, 3);
        }
    }

    [Fact]
    public void MarginsAreOutside_NotClampedOrTruncatedToZero()
    {
        var adapter = new Adapter(1600, 1000);
        adapter.SetBackbufferSize(1920, 1080);
        Assert.Equal(new Point(960, 540), adapter.AdapterPxToScreenPx(new PointF(800, 500)));
        Assert.Equal(-1, adapter.AdapterPxToScreenPx(new PointF(0, 49.9f)).Y);
        Assert.False(adapter.Presentation.TryAdapterPxToScreenPx(new PointF(800, 20), out _));
        Assert.False(adapter.Presentation.TryAdapterPxToScreenPx(new PointF(800, 950), out _));
        Assert.True(adapter.Presentation.TryAdapterPxToScreenPx(new PointF(0, 50), out _));
        adapter.Resize(0, 0);
        Assert.Equal(0, adapter.PresentationScale);
        Assert.Equal(new Point(-1, -1), adapter.AdapterPxToScreenPx(Point.Empty));
    }

    [Fact]
    public void DirtyEdgesRoundOutwardsWithoutHoles()
    {
        var transform = PresentationTransform.Fit(1920, 1080, 1600, 1000);
        var first = transform.ScreenRectToAdapterRect(new Rectangle(0, 0, 7, 11));
        var second = transform.ScreenRectToAdapterRect(new Rectangle(7, 0, 7, 11));
        Assert.Equal(new Rectangle(0, 50, 6, 10), first);
        Assert.True(first.Right >= second.Left);
    }

    [Theory]
    [InlineData(RenderScalingFilter.NearestNeighbor)]
    [InlineData(RenderScalingFilter.Linear)]
    public void PresentationClearsMarginsAndHonorsFilter(RenderScalingFilter filter)
    {
        Engine.Instance.Configuration.RenderScalingFilter = filter;
        var adapter = new Adapter(8, 8);
        using var source = new SKBitmap(2, 1);
        source.SetPixel(0, 0, SKColors.Red);
        source.SetPixel(1, 0, SKColors.Blue);
        using var image = SKImage.FromBitmap(source);
        using var dest = new SKBitmap(8, 8);
        using var canvas = new SKCanvas(dest);
        canvas.Clear(SKColors.Green);
        adapter.DrawImage(canvas, image, SKColors.Black);
        Assert.Equal(SKColors.Black, dest.GetPixel(4, 0));
        Assert.Equal(SKColors.Red, dest.GetPixel(0, 3));
        Assert.Equal(SKColors.Blue, dest.GetPixel(7, 3));
        if (filter == RenderScalingFilter.NearestNeighbor)
            Assert.Equal(SKColors.Red, dest.GetPixel(3, 3));
        else
            Assert.InRange(dest.GetPixel(3, 3).Blue, (byte)1, (byte)254);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2f)]
    public void WidgetRouterClickFocusCaptureAndDragUseLogicalCoordinates(float scale)
    {
        Engine.Instance.Configuration.RenderScale = scale;
        var adapter = new Adapter(400, 300);
        using var host = new RenderSurfaceHost<BitmapBackbuffer>(adapter);
        using var buffer = host.Backbuffer;
        using var scene = new Scene();
        host.Bind(scene, false);
        var view = Assert.Single(host.ViewManager.Views);
        using var widget = new DragWidget(host, view);
        var mouse = new Mouse(adapter);
        MouseEventPoller.Initialize(mouse, new MouseEventConfiguration(true));
        using var router = new WidgetInputRouter(host, null, MouseEventPoller.Instance, null);
        router.Register(widget);
        router.Start();
        var events = new List<string>();
        widget.PointerEnter += _ => events.Add("enter");
        widget.PointerLeave += _ => events.Add("leave");
        widget.PointerDown += _ => events.Add("down");
        widget.PointerUp += _ => events.Add("up");
        widget.PointerClick += _ => events.Add("click");
        widget.DragStarted += _ => events.Add("start");
        widget.Dragged += _ => events.Add("drag");
        widget.DragEnded += _ => events.Add("end");
        long tick = 1;
        void Poll(int x, int y, bool down)
        {
            var t = adapter.Presentation;
            mouse.Position = new PointF(t.DestinationRect.Left + x * t.Scale, t.DestinationRect.Top + y * t.Scale);
            mouse.PressedButtons.Clear();
            if (down) mouse.PressedButtons.Add(MouseButton.Left);
            MouseEventPoller.Instance!.PollForEvents(tick++);
        }
        Poll(15, 15, false);
        Poll(15, 15, true);
        Poll(15, 15, false);
        Assert.Contains("click", events);
        Assert.Same(widget, router.FocusedWidget);
        adapter.Resize(600, 600);
        Poll(15, 15, true);
        Poll(-20, -10, true); // capture must keep routing outside the presented image
        Poll(-20, -10, false);
        foreach (var expected in new[] { "enter", "leave", "down", "up", "start", "drag", "end" })
            Assert.Contains(expected, events);
        Assert.False(widget.IsDragging);
        Assert.Equal(1, events.Count(x => x == "click"));
        events.Clear();
        Poll(0, -1, true);
        Poll(0, -1, false);
        Assert.DoesNotContain("down", events);
        Assert.DoesNotContain("click", events);
    }

    [Theory]
    [InlineData(0.5f, 2f, true)]
    [InlineData(2f, 0.5f, true)]
    [InlineData(0.5f, 2f, false)]
    [InlineData(2f, 2f, false)]
    public void TextBlockLogicalPixelsAndBoundsSurvivePresentationResize(float scale, float zoom, bool worldMode)
    {
        Engine.Instance.Configuration.RenderScale = scale;
        var adapter = new Adapter(600, 400);
        using var host = new RenderSurfaceHost<GpuBackbuffer>(adapter);
        using var buffer = host.Backbuffer;
        using var scene = new Scene();
        var layer = scene.AddLayer(20, 20, width: 32, height: 32);
        host.Bind(scene, false);
        var view = Assert.Single(host.ViewManager.Views);
        view.Viewport.Zoom = zoom;
        using var text = (worldMode
            ? new TextBlock(host, layer, view, new Rectangle(15, 15, 100, 70))
            : new TextBlock(host, view, new Rectangle(15, 15, 100, 70)))
            .SetText("Scale").SetFont(SKTypeface.Default, 14).SetColors(SKColors.White, SKColors.Transparent);
        var beforeBounds = text.GetDrawLocationScreen(view);
        using var before = host.GlRenderAndSnapshot();
        using var beforePixels = SKBitmap.FromImage(before!);
        Assert.Contains(beforePixels.Pixels, p => p.Red > 0);
        adapter.Resize(777, 333);
        using var after = host.GlRenderAndSnapshot();
        using var afterPixels = SKBitmap.FromImage(after!);
        Assert.Equal(beforeBounds, text.GetDrawLocationScreen(view));
        Assert.Equal(beforePixels.Pixels, afterPixels.Pixels);
        Assert.Equal(worldMode ? zoom : 1, TextBlock.ResolveTextScale(text.Mode, view.Viewport.Zoom));
        using var presented = new SKBitmap(adapter.Width, adapter.Height);
        using var canvas = new SKCanvas(presented);
        Assert.True(host.GlDrawCurrentFrameToCanvas(canvas));
        Assert.Contains(presented.Pixels, p => p.Red > 0);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2f)]
    public void TouchRouterPreservesClickAndCaptureThroughScalingAndResize(float scale)
    {
        Engine.Instance.Configuration.RenderScale = scale;
        var adapter = new Adapter(400, 300);
        using var host = new RenderSurfaceHost<BitmapBackbuffer>(adapter);
        using var buffer = host.Backbuffer;
        using var scene = new Scene();
        host.Bind(scene, false);
        using var widget = new DragWidget(host, Assert.Single(host.ViewManager.Views));
        var touch = new Touch(adapter);
        TouchEventPoller.Initialize(touch, new TouchEventConfiguration());
        using var router = new WidgetInputRouter(host, null, null, TouchEventPoller.Instance);
        router.Register(widget);
        router.Start();
        int clicks = 0, ups = 0, starts = 0, ends = 0;
        widget.PointerClick += _ => clicks++;
        widget.PointerUp += _ => ups++;
        widget.DragStarted += _ => starts++;
        widget.DragEnded += _ => ends++;
        long tick = 1;
        void Poll(int x, int y, TouchPhase phase)
        {
            var t = adapter.Presentation;
            touch.Set(new PointF(t.DestinationRect.Left + x * t.Scale, t.DestinationRect.Top + y * t.Scale), phase);
            TouchEventPoller.Instance!.PollForEvents(tick++);
        }
        Poll(15, 15, TouchPhase.Began);
        Poll(15, 15, TouchPhase.Ended);
        Assert.Equal(1, clicks);
        Assert.Same(widget, router.FocusedWidget);
        adapter.Resize(600, 600);
        Poll(15, 15, TouchPhase.Began);
        Poll(-10, -10, TouchPhase.Moved);
        Poll(-10, -10, TouchPhase.Ended);
        Assert.Equal(1, clicks);
        Assert.Equal(2, ups);
        Assert.Equal(1, starts);
        Assert.Equal(1, ends);
        Poll(0, -1, TouchPhase.Began);
        Poll(0, -1, TouchPhase.Ended);
        Assert.Equal(1, clicks);
        Assert.Equal(2, ups);
    }

    [Theory]
    [InlineData(RenderScalingFilter.Linear)]
    [InlineData(RenderScalingFilter.NearestNeighbor)]
    public void DirectSurfacePresentationAppliesFilterAndMargins(RenderScalingFilter filter)
    {
        Engine.Instance.Configuration.RenderScalingFilter = filter;
        var adapter = new Adapter(2, 1);
        using var host = new RenderSurfaceHost<GpuBackbuffer>(adapter);
        using var buffer = host.Backbuffer;
        buffer.Canvas.Clear(SKColors.Red);
        using (var blue = new SKPaint { Color = SKColors.Blue })
            buffer.Canvas.DrawRect(new SKRect(1, 0, 2, 1), blue);
        adapter.Resize(8, 8);
        using var pixels = new SKBitmap(8, 8);
        using var canvas = new SKCanvas(pixels);
        Assert.True(host.GlDrawCurrentFrameToCanvas(canvas));
        Assert.Equal(SKColors.Black, pixels.GetPixel(4, 0));
        Assert.Equal(SKColors.Red, pixels.GetPixel(0, 3));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(7, 3));
        if (filter == RenderScalingFilter.NearestNeighbor)
            Assert.Equal(SKColors.Red, pixels.GetPixel(3, 3));
        else
            Assert.InRange(pixels.GetPixel(3, 3).Blue, (byte)1, (byte)254);
    }

    private sealed class Touch(Adapter adapter) : ITouchAdapter
    {
        private TouchPoint[] _active = [], _began = [], _ended = [];
        public IReadOnlyList<TouchPoint> ActiveTouches => _active;
        public IReadOnlyList<TouchPoint> ConsumeBeganTouches() { var result = _began; _began = []; return result; }
        public IReadOnlyList<TouchPoint> ConsumeEndedTouches() { var result = _ended; _ended = []; return result; }
        internal void Set(PointF physical, TouchPhase phase)
        {
            var point = new TouchPoint(1, adapter.AdapterPxToScreenPx(physical), phase);
            _active = phase == TouchPhase.Ended ? [] : [point];
            if (phase == TouchPhase.Began) _began = [point];
            if (phase == TouchPhase.Ended) _ended = [point];
        }
    }

    internal sealed class Adapter(int width, int height, bool initial = true) : RenderSurfaceAdapterBase(width, height, initial)
    {
        internal void Resize(int width, int height) => SetDestinationSize(width, height);
        public override void Present(SKImage image, SKRectI source, SKRect dest) => image.Dispose();
    }

    private sealed class Mouse(Adapter adapter) : IMouseAdapter
    {
        internal PointF Position;
        public Point CurrentPosition => adapter.AdapterPxToScreenPx(Position);
        public HashSet<MouseButton> PressedButtons { get; } = [];
        public KeyboardModifierState CurrentKeyboardModifiers => KeyboardModifierState.None;
        public int ScrollDelta => 0;
    }

    private sealed class DragWidget : DraggableWidgetBase
    {
        internal DragWidget(RenderSurfaceHostBase host, View view) : base(host, DirectDrawingMode.View, new PointF(10, 10))
        {
            CanReceiveFocus = true;
            IsKeyboardInputEnabled = true;
            Add(new DirectRectangle(Color.White, host, view, new Rectangle(10, 10, 40, 40)));
        }
    }
}
