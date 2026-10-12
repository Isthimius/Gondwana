using System.Drawing;
using Gondwana.Demos.Spot;
using Gondwana.Demos.Spot.Game;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Input.Touch;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>Exercises Spot's actual board input through the mouse and touch pollers.</summary>
[Collection("Global engine state")]
public sealed class SpotBoardInputTests : IDisposable
{
    private readonly Adapter _adapter;
    private readonly RenderSurfaceHost<BitmapBackbuffer> _host;
    private readonly Tilesheet _sheet;
    private readonly Scene _scene;
    private readonly SpotGameRuntime _runtime;
    private readonly WidgetInputRouter _router;
    private readonly Touch _touch;
    private readonly Mouse _mouse;
    private long _tick = 1;

    /// <summary>Creates a real Spot game with two human players and simulated input devices.</summary>
    public SpotBoardInputTests()
    {
        Engine.Instance.EngineDispatcher.BindToCurrentThread();
        Engine.Instance.EngineDispatcher.Drain();
        Engine.Instance.Configuration.RenderScale = 1;
        _adapter = new Adapter();
        _host = new RenderSurfaceHost<BitmapBackbuffer>(_adapter);
        _touch = new Touch(_adapter);
        _mouse = new Mouse(_adapter);
        TouchEventPoller.Initialize(_touch, new TouchEventConfiguration());
        MouseEventPoller.Initialize(_mouse);
        _router = new WidgetInputRouter(_host, null, MouseEventPoller.Instance, TouchEventPoller.Instance);
        _router.Start();

        _runtime = new SpotGameRuntime(_host, 0, () => _router);
        _scene = _runtime.CreateInitialScene();
        _host.Bind(_scene, false);
        _runtime.OnSceneGraphCreated();
        _sheet = TilesheetFactory.FromBitmap("spot.board-input", new SKBitmap(320, 320));
        _sheet.DefaultRegion.TileSize = new Size(64, 64);
        _runtime.SetTilesheets(_sheet, _sheet, _sheet);
        _runtime.SetAudioResources(null, null, null, null, null, null, null, null, null, SKTypeface.Default);
        _runtime.SetCloudsEnabled(false);
        _runtime.SetJiggleEnabled(false);
        _runtime.StartNewGame(new NewGameOptions
        {
            PlayerCount = 2,
            BoardWidth = 12,
            BoardHeight = 12,
            Players =
            [
                new Player { Name = "Blue", Type = PlayerType.Human, ColorItem = new ColorItem("Blue", SKColors.Blue, SKColors.White) },
                new Player { Name = "Green", Type = PlayerType.Human, ColorItem = new ColorItem("Green", SKColors.Green, SKColors.White) }
            ]
        });
        _runtime.OnMouseAdapterInitialized();
        MouseEventPoller.Instance!.StartMonitoringMouse(timeBetweenEvents: 0);
        _runtime.OnTouchAdapterInitialized();
    }

    /// <summary>Verifies taps select exactly once and choose a valid clone destination after resizing.</summary>
    /// <param name="width">The displayed canvas width.</param>
    /// <param name="height">The displayed canvas height.</param>
    [Theory]
    [InlineData(769, 801)]
    [InlineData(393, 409)]
    [InlineData(375, 390)]
    public void TapSelectsOnceAndMovesAfterPresentationResize(int width, int height)
    {
        _adapter.Resize(width, height);
        var selections = 0;
        _runtime.SpotGame.SpotSelected += _ => selections++;

        Tap(CellPosition(0, 0));

        Assert.Equal(1, selections);
        Assert.Same(_runtime.SpotGame.SpotGameField.GetCell(0, 0), _runtime.SpotGame.SelectedCell);

        Tap(CellPosition(1, 0));

        Assert.Null(_runtime.SpotGame.SelectedCell);
        Assert.Same(_runtime.SpotGame.CurrentPlayer, _runtime.SpotGame.SpotGameField.GetCell(1, 0).OccupiedBy);
        Assert.NotSame(_runtime.SpotGame.SpotGameField.GetCell(0, 0).Sprite,
            _runtime.SpotGame.SpotGameField.GetCell(1, 0).Sprite);
    }

    /// <summary>Verifies a second tap on the selected spot deselects it.</summary>
    [Fact]
    public void SecondTapDeselectsSpot()
    {
        Tap(CellPosition(0, 0));
        Tap(CellPosition(0, 0));

        Assert.Null(_runtime.SpotGame.SelectedCell);
    }

    /// <summary>Verifies widget taps activate the widget without selecting the spot behind it.</summary>
    [Fact]
    public void WidgetTapDoesNotSelectUnderlyingSpot()
    {
        var position = CellPosition(0, 0);
        using var button = new ButtonWidget(_host, _host.ViewManager.Views[0],
            new Rectangle(position.X - 20, position.Y - 20, 40, 40), "Overlay");
        var clicks = 0;
        button.PointerClick += _ => clicks++;
        button.Show();

        Tap(position);

        Assert.Equal(1, clicks);
        Assert.Null(_runtime.SpotGame.SelectedCell);
    }

    /// <summary>Verifies the new-game dialog blocks board taps even outside its bounds.</summary>
    [Fact]
    public void NewGameDialogBlocksBoardTap()
    {
        _runtime.OpenNewGameDialog();

        Tap(CellPosition(0, 0));

        Assert.Null(_runtime.SpotGame.SelectedCell);
    }

    /// <summary>Verifies cancelled contacts and drags do not select a spot.</summary>
    /// <param name="phase">The ending contact phase.</param>
    /// <param name="drag">Whether the contact ends away from its starting cell.</param>
    [Theory]
    [InlineData(TouchPhase.Cancelled, false)]
    [InlineData(TouchPhase.Ended, true)]
    public void CancelledContactAndDragDoNotSelectSpot(TouchPhase phase, bool drag)
    {
        _touch.Begin(1, PhysicalPosition(CellPosition(0, 0)));
        _touch.End(1, PhysicalPosition(CellPosition(drag ? 2 : 0, 0)), phase);
        TouchEventPoller.Instance!.PollForEvents(_tick++);

        Assert.Null(_runtime.SpotGame.SelectedCell);
    }

    /// <summary>Verifies multi-finger gestures do not select board cells.</summary>
    [Fact]
    public void MultiTouchDoesNotSelectSpot()
    {
        var position = PhysicalPosition(CellPosition(0, 0));
        _touch.Begin(1, position);
        _touch.Begin(2, position);
        _touch.End(1, position);
        _touch.End(2, position);
        TouchEventPoller.Instance!.PollForEvents(_tick++);

        Assert.Null(_runtime.SpotGame.SelectedCell);
    }

    /// <summary>Verifies desktop mouse presses continue to select board cells.</summary>
    [Fact]
    public void MousePressStillSelectsSpot()
    {
        _mouse.Position = PhysicalPosition(CellPosition(0, 0));
        _mouse.PressedButtons.Add(MouseButton.Left);
        MouseEventPoller.Instance!.PollForEvents(_tick++);

        Assert.Same(_runtime.SpotGame.SpotGameField.GetCell(0, 0), _runtime.SpotGame.SelectedCell);
    }

    /// <summary>Verifies taps are ignored while the computer owns the current turn.</summary>
    [Fact]
    public void ComputerTurnBlocksBoardTap()
    {
        _runtime.SpotGame.Players[1].Type = PlayerType.Computer;
        _runtime.SpotGame.NextPlayer();

        Tap(CellPosition(11, 11));

        Assert.Null(_runtime.SpotGame.SelectedCell);
    }

    /// <summary>Verifies host cleanup removes the board's gesture subscription.</summary>
    [Fact]
    public void UnhookEventsStopsBoardTouchHandling()
    {
        _runtime.UnhookEvents();

        Tap(CellPosition(0, 0));

        Assert.Null(_runtime.SpotGame.SelectedCell);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _runtime.UnhookEvents();
        _router.Dispose();
        TouchEventPoller.Reset();
        MouseEventPoller.Reset();
        foreach (var drawing in Engine.Instance.Managers.DirectDrawings.DirectDrawings.OfType<DirectDrawingBase>()
                     .Where(drawing => ReferenceEquals(drawing.RenderSurfaceHost, _host)))
            drawing.Dispose();
        Engine.Instance.Managers.Sprites.Clear();
        Gondwana.Timers.Timer.ClearAll();
        _scene.Dispose();
        _host.Backbuffer.Dispose();
        _host.Dispose();
        _sheet.Dispose();
        Engine.Instance.EngineDispatcher.Drain();
    }

    private Point CellPosition(int x, int y)
    {
        var position = _host.ViewManager.Views[0].WorldPxToScreenPx(
            _scene[0], new PointF(x * 64 + 32, y * 64 + 32));
        return Point.Round(position);
    }

    private PointF PhysicalPosition(Point position)
    {
        var transform = _adapter.Presentation;
        return new PointF(transform.DestinationRect.Left + position.X * transform.Scale,
            transform.DestinationRect.Top + position.Y * transform.Scale);
    }

    private void Tap(Point position)
    {
        var physical = PhysicalPosition(position);
        _touch.Begin(1, physical);
        _touch.End(1, physical);
        TouchEventPoller.Instance!.PollForEvents(_tick++);
    }

    private sealed class Adapter() : RenderSurfaceAdapterBase(769, 801)
    {
        /// <summary>Updates the canvas presentation size without resizing the logical game.</summary>
        /// <param name="width">The canvas width.</param>
        /// <param name="height">The canvas height.</param>
        internal void Resize(int width, int height) => SetDestinationSize(width, height);
        /// <inheritdoc/>
        public override void Present(SKImage image, SKRectI source, SKRect destination) => image.Dispose();
    }

    private sealed class Touch(Adapter adapter) : ITouchAdapter
    {
        private readonly Dictionary<int, TouchPoint> _active = [];
        private readonly Queue<TouchPoint> _began = [];
        private readonly Queue<TouchPoint> _ended = [];
        /// <inheritdoc/>
        public IReadOnlyList<TouchPoint> ActiveTouches => _active.Values.ToArray();
        /// <inheritdoc/>
        public IReadOnlyList<TouchPoint> ConsumeBeganTouches() => Drain(_began);
        /// <inheritdoc/>
        public IReadOnlyList<TouchPoint> ConsumeEndedTouches() => Drain(_ended);

        /// <summary>Queues a new contact using the adapter's input coordinate transform.</summary>
        /// <param name="id">The contact identifier.</param>
        /// <param name="position">The physical canvas position.</param>
        internal void Begin(int id, PointF position)
        {
            var point = new TouchPoint(id, adapter.AdapterPxToScreenPx(position), TouchPhase.Began);
            _active[id] = point;
            _began.Enqueue(point);
        }

        /// <summary>Queues an ended or cancelled contact.</summary>
        /// <param name="id">The contact identifier.</param>
        /// <param name="position">The physical canvas position.</param>
        /// <param name="phase">The ending phase.</param>
        internal void End(int id, PointF position, TouchPhase phase = TouchPhase.Ended)
        {
            _active.Remove(id);
            _ended.Enqueue(new TouchPoint(id, adapter.AdapterPxToScreenPx(position), phase));
        }

        private static TouchPoint[] Drain(Queue<TouchPoint> queue)
        {
            var points = queue.ToArray();
            queue.Clear();
            return points;
        }
    }

    private sealed class Mouse(Adapter adapter) : IMouseAdapter
    {
        /// <summary>The physical canvas position.</summary>
        internal PointF Position;
        /// <inheritdoc/>
        public Point CurrentPosition => adapter.AdapterPxToScreenPx(Position);
        /// <inheritdoc/>
        public HashSet<MouseButton> PressedButtons { get; } = [];
        /// <inheritdoc/>
        public KeyboardModifierState CurrentKeyboardModifiers => KeyboardModifierState.None;
        /// <inheritdoc/>
        public int ScrollDelta => 0;
    }
}
