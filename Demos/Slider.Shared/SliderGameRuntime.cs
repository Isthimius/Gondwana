using System.Diagnostics;
using Gondwana.Audio;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Input.Keyboard;
using Gondwana.Physics.Movement.Scripted;
using Gondwana.Scenes;
using Gondwana.Rendering;
using Gondwana.Widgets;
using Gondwana.Widgets.Dialogs;
using SkiaSharp;
using View = Gondwana.Rendering.Views.View;

namespace Gondwana.Demos.Slider;

/// <summary>Owns Gondwana startup, input, graphics, audio, and engine-thread puzzle state.</summary>
internal sealed partial class SliderGameRuntime
{
    private static global::Gondwana.Engine Engine => global::Gondwana.Engine.Instance;
    private readonly RenderSurfaceHostBase _surface;
    private readonly Func<WidgetInputRouter?> _routerAccessor;
    private readonly ISliderScoreStore _scoreStore;
    private readonly IExternalUriLauncher _uriLauncher;
    private readonly Action<int, int> _requestLogicalResolution;
    private readonly SKImage? _aboutLogo;
    private WidgetInputRouter? WidgetInputRouter => _routerAccessor();

    private const int WorldTiles = 20;
    private const int WorldTileSize = 64;
    private const float PlayerSlideSeconds = 0.15f;
    private const float MaxShuffleSlideSeconds = 0.06f;
    private readonly object _requestGate = new();
    private readonly Queue<PuzzleRequest> _requests = new();
    private readonly Stopwatch _timer = new();

    private SceneLayer _layer = null!;
    private SlidingPuzzle _board = null!;
    private PuzzleRenderer _renderer = null!;
    private SliderSidebar? _sidebar;
    private View _uiView = null!;
    private View _gameView = null!;
    private AudioResource? _slideSound;
    private AudioResource? _winSound;
    private Sprite? _movingSprite;
    private IEnumerator<PuzzleMove>? _shuffleMoves;
    private float _shuffleSlideSeconds;
    private bool _showGrid = true;
    private bool _fitRequested = true;
    private bool _cameraFitPending;
    private Size _clientSize;
    private Size _requestedLogicalSize;
    private bool _started;
    private bool _won;
    private PuzzleRequest? _pendingPictureLoad;
    private SliderSave? _pendingSavedLoad;
    private bool _hideLoadingOnNextTick;
    private long _presentedLoadingFrames;
    private long _loadingFrameBarrier;
    private long _lastStatusTicks;

    internal SliderGameRuntime(RenderSurfaceHostBase surface,
        Func<WidgetInputRouter?> routerAccessor, ISliderScoreStore scoreStore,
        SKImage? aboutLogo, IExternalUriLauncher uriLauncher,
        Action<int, int> requestLogicalResolution)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _routerAccessor = routerAccessor ?? throw new ArgumentNullException(nameof(routerAccessor));
        _scoreStore = scoreStore ?? throw new ArgumentNullException(nameof(scoreStore));
        _aboutLogo = aboutLogo;
        _uriLauncher = uriLauncher ?? throw new ArgumentNullException(nameof(uriLauncher));
        _requestLogicalResolution = requestLogicalResolution ?? throw new ArgumentNullException(nameof(requestLogicalResolution));
        _highScores = scoreStore.LoadScores();
        _clientSize = new Size(surface.Backbuffer.Width, surface.Backbuffer.Height);
    }

    /// <summary>
    /// Raised by the in-engine Open Picture button. Subscribers must open the
    /// platform's file picker on the platform UI thread.
    /// </summary>
    internal event Action<int, int>? OpenPictureRequested;

    internal void RequestPuzzle(byte[]? imageBytes, int columns, int rows)
    {
        lock (_requestGate)
            _requests.Enqueue(new PuzzleRequest(imageBytes ?? _imagePng, columns, rows, null));
    }

    internal void RequestShuffle()
    {
        lock (_requestGate)
            _requests.Enqueue(new PuzzleRequest(null, 0, 0, PuzzleCommand.Shuffle));
    }

    internal void RequestGrid(bool show)
    {
        lock (_requestGate)
            _requests.Enqueue(new PuzzleRequest(null, 0, 0,
                show ? PuzzleCommand.ShowGrid : PuzzleCommand.HideGrid));
    }

    internal void RequestResize(Size size)
    {
        lock (_requestGate)
        {
            _clientSize = size;
            _fitRequested = true;
        }
    }

    internal void SetAudioResources(AudioResource? slideSound, AudioResource? winSound)
    {
        _slideSound = slideSound;
        _winSound = winSound;
    }

    internal Scene CreateInitialScene()
    {
        var scene = new Scene();
        _layer = scene.AddLayer(WorldTiles, WorldTiles, WorldTileSize, WorldTileSize,
            zOrder: 0, parallax: 1f, coordinateSystem: CoordinateSystemTypes.Orthogonal);
        return scene;
    }

    internal void OnSceneBound()
    {
        var host = _surface;
        host.Backbuffer.ClearColor = new SKColor(18, 23, 35);

        // Two non-overlapping Views share the one GPU adapter: a fixed-width
        // view for in-engine widgets and a fluid view for the puzzle scene.
        var views = host.ViewManager;
        _uiView = views.Views[0];
        Size logical = new(host.Backbuffer.Width, host.Backbuffer.Height);
        _uiView.Viewport.TargetRectPx = new Rectangle(0, 0,
            SliderSidebar.Width, logical.Height);
        views.AddView(new Rectangle(SliderSidebar.Width, 0,
            Math.Max(1, logical.Width - SliderSidebar.Width), logical.Height),
            zOrder: 1, worldBoundsPx: RectangleF.Empty);
        _gameView = views.Views[^1];

        // A camera clamped to world bounds would push a puzzle off-center when
        // the viewport is larger than the fixed scene in either dimension.
        _gameView.Camera.WorldBoundsPx = RectangleF.Empty;
    }

    internal void CreateSprites()
    {
        _board = new SlidingPuzzle(4, 3);
        _renderer = new PuzzleRenderer(_layer, Engine.Managers.Tilesheets, Engine.Managers.Sprites);
        using var image = CreateSampleImage();
        _renderer.Load(image, _board, _showGrid);
        FitPuzzle();
    }

    internal void CreateDirectDrawings()
    {
        _sidebar = new SliderSidebar(_surface, _uiView,
            _surface.Backbuffer.Height,
            onNewPuzzle: () => RequestPuzzle(null, _sidebar!.Columns, _sidebar.Rows),
            onOpenPicture: (columns, rows) => OpenPictureRequested?.Invoke(columns, rows),
            onShuffle: RequestShuffle,
            onGridChanged: RequestGrid);
        CreateMenu();
        WidgetInputRouter!.UnhandledPrimaryAction += OnPrimaryPointerAction;
    }

    internal void OnKeyboardAdapterInitialized()
    {
        Engine.Input.KeyboardEventPoller!.KeyDown += OnKeyDown;
    }

    internal void OnEngineInitialized()
    {
        Engine.Configuration.TargetFPS = 60;
        Engine.BeforeBackgroundTasksExecute += OnTick;
    }

    internal void OnInitialized() => PublishStatus();

    // The platform host reports actual GPU presentations so a loading state
    // remains visible until a new frame has reached the screen.
    internal void NotifyFramePresented()
        => Interlocked.Increment(ref _presentedLoadingFrames);

    private void BeginLoadingIndicator()
    {
        _loadingFrameBarrier = Interlocked.Read(ref _presentedLoadingFrames);
        _hideLoadingOnNextTick = false;
        _sidebar?.SetLoading(0.15f);
    }

    private void FinishLoadingIndicator()
    {
        _sidebar?.SetLoading(1f);
        _loadingFrameBarrier = Interlocked.Read(ref _presentedLoadingFrames);
        _hideLoadingOnNextTick = true;
    }

    private void StopLoadingIndicator()
    {
        _sidebar?.SetLoading(-1f);
        _hideLoadingOnNextTick = false;
    }

    private void OnTick()
    {
        long paintedFrames = Interlocked.Read(ref _presentedLoadingFrames);
        if (_hideLoadingOnNextTick)
        {
            if (paintedFrames > _loadingFrameBarrier)
                StopLoadingIndicator();
        }
        else if (paintedFrames > _loadingFrameBarrier && _pendingSavedLoad is { } saved)
        {
            _pendingSavedLoad = null;
            try
            {
                RestoreSavedGame(saved);
                FinishLoadingIndicator();
            }
            catch (Exception ex)
            {
                StopLoadingIndicator();
                _sidebar?.SetStatus("Unable to load saved puzzle: " + ex.Message);
            }
        }
        else if (paintedFrames > _loadingFrameBarrier && _pendingPictureLoad is { } picture)
        {
            _pendingPictureLoad = null;
            try
            {
                LoadPicture(picture);
                _cameraFitPending = true;
                FinishLoadingIndicator();
            }
            catch (Exception ex)
            {
                StopLoadingIndicator();
                _sidebar?.SetStatus("Unable to load puzzle: " + ex.Message);
            }
        }

        List<PuzzleRequest> pending = [];
        bool resized;
        Size newSize;
        lock (_requestGate)
        {
            while (_requests.Count > 0)
                pending.Add(_requests.Dequeue());
            resized = _fitRequested;
            newSize = _clientSize;
            _fitRequested = false;
        }

        // Normal Gondwana surface resizing is presentation-only. Slider instead has a
        // fluid playfield: request a matching logical resolution so GPU presentation
        // fills the entire control, with no inherited letterbox/pillarbox bars.
        if (resized && newSize.Width > 0 && newSize.Height > 0)
        {
            _requestedLogicalSize = newSize;
            _requestLogicalResolution(newSize.Width, newSize.Height);
            _cameraFitPending = true;
        }

        foreach (PuzzleRequest request in pending)
        {
            try
            {
                switch (request.Command)
                {
                    case PuzzleCommand.Shuffle:
                        CancelAnimation();
                        _timer.Reset();
                        _elapsedBeforeResume = TimeSpan.Zero;
                        _started = false;
                        _won = false;
                        // Use the previous demo's legal-move shuffle count. Animate
                        // each move, with a shorter duration than a player slide.
                        int shuffleCount = _board.Columns * _board.Rows * 3;
                        _shuffleSlideSeconds = Math.Min(
                            MaxShuffleSlideSeconds, 15f / shuffleCount);
                        _shuffleMoves = _board.ShuffleSteps(shuffleCount).GetEnumerator();
                        StartNextShuffleMove();
                        PublishStatus();
                        break;
                    case PuzzleCommand.Restore:
                        _pendingPictureLoad = null;
                        _pendingSavedLoad = request.SavedGame!;
                        BeginLoadingIndicator();
                        break;
                    case PuzzleCommand.ShowGrid:
                    case PuzzleCommand.HideGrid:
                        _showGrid = request.Command == PuzzleCommand.ShowGrid;
                        _renderer.ShowGrid(_showGrid);
                        break;
                    case null:
                        _pendingSavedLoad = null;
                        _pendingPictureLoad = request;
                        BeginLoadingIndicator();
                        break;
                }
            }
            catch (Exception ex)
            {
                _sidebar?.SetStatus("Unable to load puzzle: " + ex.Message);
            }
        }

        // GPU resize is applied asynchronously on the owning GL thread. Calculate
        // the camera fit only after its logical viewport has actually resized;
        // using the control's newer size against an older viewport clips pieces.
        if (_cameraFitPending &&
            (_requestedLogicalSize.IsEmpty ||
             _surface.Backbuffer.Width == _requestedLogicalSize.Width &&
             _surface.Backbuffer.Height == _requestedLogicalSize.Height))
        {
            ApplyLayout();
            _cameraFitPending = false;
        }

        UpdateShiftLabels();

        // Refresh the clock without touching any platform UI control from the engine thread.
        if (_timer.IsRunning && Stopwatch.GetTimestamp() - _lastStatusTicks >= Stopwatch.Frequency / 4)
            PublishStatus();
    }

    private void LoadPicture(PuzzleRequest request)
    {
        if (request.Columns is < 3 or > 20 || request.Rows is < 3 or > 20)
            throw new ArgumentOutOfRangeException(nameof(request), "Rows and columns must be 3–20.");

        // Decode first. An invalid file must not destroy the currently playable board.
        using SKBitmap image = request.ImagePng is { Length: > 0 }
            ? SKBitmap.Decode(request.ImagePng)
                ?? throw new InvalidDataException("Unsupported or corrupt picture.")
            : CreateSampleImage();

        var replacement = new SlidingPuzzle(request.Columns, request.Rows);
        CancelAnimation();
        _renderer.Load(image, replacement, _showGrid);
        _board = replacement;
        CapturePicture(image);
        _elapsedBeforeResume = TimeSpan.Zero;
        _started = false;
        _won = false;
        _timer.Reset();
        PublishStatus();
    }

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (args.KeyAction != KeyAction.Pressed || _movingSprite is not null || _shuffleMoves is not null ||
            _won || HasMenuOrDialogOpen || _sidebar?.IsEditingDimensions == true ||
            string.IsNullOrEmpty(args.KeyConfig.Key))
            return;

        // Direction refers to the piece's motion, not the empty square's motion.
        int x = _board.BlankColumn;
        int y = _board.BlankRow;
        switch (args.KeyConfig.Key)
        {
            case "Left": case "ArrowLeft": x++; break;
            case "Right": case "ArrowRight": x--; break;
            case "Up": case "ArrowUp": y++; break;
            case "Down": case "ArrowDown": y--; break;
            default: return;
        }

        int piece = _board.PieceAt(x, y);
        if (piece >= 0)
            TryMovePiece(piece);
    }

    private void TryMovePiece(int piece)
    {
        if (_renderer.GetPiece(piece) is null ||
            !_board.TryMovePiece(piece, out PuzzleMove move))
            return;

        if (!_started)
        {
            _started = true;
            _timer.Start();
        }

        AnimateMove(move, PlayerSlideSeconds);
        PublishStatus();
    }

    private void StartNextShuffleMove()
    {
        if (_shuffleMoves is null)
            return;

        if (!_shuffleMoves.MoveNext())
        {
            _shuffleMoves.Dispose();
            _shuffleMoves = null;
            return;
        }

        AnimateMove(_shuffleMoves.Current, _shuffleSlideSeconds);
    }

    private void AnimateMove(PuzzleMove move, float durationSeconds)
    {
        Sprite sprite = _renderer.GetPiece(move.PieceId)
            ?? throw new InvalidOperationException("Shuffle references a missing puzzle piece.");
        _movingSprite = sprite;
        sprite.Movement.ScriptedMovementStopped += OnMoveStopped;
        sprite.Movement.MoveTo(_renderer.PositionForIndex(move.DestinationIndex),
            durationSeconds, null, 0.01f);
        _slideSound?.Play();
    }

    private void OnMoveStopped(ScriptedMovement movement)
    {
        if (_movingSprite is null)
            return;

        _movingSprite.Movement.ScriptedMovementStopped -= OnMoveStopped;
        _movingSprite = null;
        _slideSound?.Stop();

        if (_shuffleMoves is not null)
        {
            StartNextShuffleMove();
            if (_shuffleMoves is not null)
                return;
        }

        if (_board.IsSolved && _started)
        {
            _timer.Stop();
            _won = true;
            _winSound?.Play();
            RecordHighScore();
        }

        PublishStatus();
    }

    private void CancelAnimation()
    {
        _shuffleMoves?.Dispose();
        _shuffleMoves = null;

        if (_movingSprite is not null)
        {
            Sprite sprite = _movingSprite;
            _movingSprite = null;
            sprite.Movement.ScriptedMovementStopped -= OnMoveStopped;
            sprite.Movement.CancelScript();
            _renderer.SnapToBoard(_board);
        }

        _slideSound?.Stop();
    }

    private void ApplyLayout()
    {
        // View rectangles are automatically scaled with the logical Backbuffer,
        // so restore the sidebar's exact width after the GPU resize completes.
        var buffer = _surface.Backbuffer;
        _uiView.Viewport.TargetRectPx =
            new Rectangle(0, 0, SliderSidebar.Width, buffer.Height);
        _gameView.Viewport.TargetRectPx =
            new Rectangle(SliderSidebar.Width, 0,
                Math.Max(1, buffer.Width - SliderSidebar.Width), buffer.Height);
        _sidebar?.Resize(buffer.Height);
        FitPuzzle();
    }

    private void FitPuzzle()
    {
        // This must use the puzzle View's actual logical viewport. The adapter
        // may resize before the GPU Backbuffer and Viewports catch up.
        View view = _gameView;
        Rectangle viewport = view.Viewport.TargetRectPx;
        if (_renderer.WidthPx <= 0 || _renderer.HeightPx <= 0 ||
            viewport.Width <= 0 || viewport.Height <= 0)
            return;

        float zoom = Math.Min(
            viewport.Width / (float)_renderer.WidthPx,
            viewport.Height / (float)_renderer.HeightPx) * 0.96f;
        view.Viewport.SnapZoom(Math.Max(0.05f, zoom));
        float center = WorldTiles * WorldTileSize / 2f;
        view.Camera.CenterOn(new PointF(center, center));
    }

    private void PublishStatus()
    {
        _lastStatusTicks = Stopwatch.GetTimestamp();
        string result = _shuffleMoves is not null ? "Shuffling..." :
            _won ? "Solved!" : _started ? "In progress" : "Ready";
        _sidebar?.SetStatus(
            result + Environment.NewLine +
            "Correct: " + _board.CorrectPieces + " / " + _board.TotalPieces + Environment.NewLine +
            "Moves: " + _board.MoveCount + Environment.NewLine +
            "Time: " + SliderPersistence.FormatElapsed(_elapsedBeforeResume + _timer.Elapsed));
    }

    internal void UnhookEvents()
    {
        if (WidgetInputRouter is not null)
            WidgetInputRouter.UnhandledPrimaryAction -= OnPrimaryPointerAction;
        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;
        Engine.BeforeBackgroundTasksExecute -= OnTick;
        StopLoadingIndicator();
        CancelAnimation();
        _menu?.Dispose();
        _menu = null;
        _sidebar?.Dispose();
        _sidebar = null;
        _renderer?.Dispose();
    }

    private static SKBitmap CreateSampleImage()
    {
        const int width = 1200;
        const int height = 800;
        var image = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(image);
        canvas.Clear(new SKColor(132, 197, 231));
        using var paint = new SKPaint { IsAntialias = true };

        paint.Color = new SKColor(251, 216, 115);
        canvas.DrawCircle(990, 150, 112, paint);
        paint.Color = new SKColor(229, 238, 241);
        canvas.DrawOval(new SKRect(115, 100, 380, 190), paint);
        canvas.DrawOval(new SKRect(355, 175, 690, 255), paint);
        paint.Color = new SKColor(81, 131, 165);
        using (var mountain = new SKPath())
        {
            mountain.MoveTo(0, 505);
            mountain.LineTo(290, 190);
            mountain.LineTo(560, 510);
            mountain.LineTo(800, 270);
            mountain.LineTo(width, 520);
            mountain.Close();
            canvas.DrawPath(mountain, paint);
        }

        paint.Color = new SKColor(82, 155, 112);
        canvas.DrawRect(0, 510, width, 290, paint);
        paint.Color = new SKColor(235, 182, 124);
        canvas.DrawRect(380, 425, 420, 285, paint);
        paint.Color = new SKColor(126, 64, 67);
        using (var roof = new SKPath())
        {
            roof.MoveTo(330, 430);
            roof.LineTo(590, 235);
            roof.LineTo(850, 430);
            roof.Close();
            canvas.DrawPath(roof, paint);
        }

        paint.Color = new SKColor(70, 107, 143);
        canvas.DrawRect(550, 550, 95, 160, paint);
        paint.Color = new SKColor(245, 225, 166);
        canvas.DrawRect(430, 475, 75, 80, paint);
        canvas.DrawRect(695, 475, 75, 80, paint);
        paint.Color = new SKColor(86, 125, 83);
        canvas.DrawRect(110, 460, 50, 250, paint);
        canvas.DrawRect(1010, 450, 55, 270, paint);
        paint.Color = new SKColor(39, 111, 68);
        canvas.DrawCircle(133, 440, 115, paint);
        canvas.DrawCircle(1040, 420, 130, paint);
        paint.Color = new SKColor(238, 194, 104);
        canvas.DrawRect(0, 740, width, 60, paint);
        canvas.Flush();
        return image;
    }

    private enum PuzzleCommand { Shuffle, ShowGrid, HideGrid, Restore }
    private sealed record PuzzleRequest(byte[]? ImagePng, int Columns, int Rows,
        PuzzleCommand? Command, SliderSave? SavedGame = null);
}
