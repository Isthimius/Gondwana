using System.Diagnostics;
using Gondwana.Audio;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Physics.Movement.Scripted;
using Gondwana.Scenes;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;
using MouseEventArgs = Gondwana.Input.Mouse.MouseEventArgs;

namespace Gondwana.Demos.Slider;

/// <summary>Owns Gondwana startup, input, graphics, audio, and engine-thread puzzle state.</summary>
internal sealed class SliderGameHost : WinFormsGpuGameHost
{
    private const int WorldTiles = 20;
    private const int WorldTileSize = 64;
    private readonly object _requestGate = new();
    private readonly Queue<PuzzleRequest> _requests = new();
    private readonly Stopwatch _timer = new();

    private SceneLayer _layer = null!;
    private SlidingPuzzle _board = null!;
    private PuzzleRenderer _renderer = null!;
    private AudioResource? _slideSound;
    private AudioResource? _winSound;
    private Sprite? _movingSprite;
    private bool _showGrid = true;
    private bool _fitRequested = true;
    private Size _clientSize;
    private bool _started;
    private bool _won;
    private long _lastStatusTicks;

    internal SliderGameHost(WinFormGpuRenderSurfaceControl surface) : base(surface)
    {
        _clientSize = surface.ClientSize;
        surface.SizeChanged += OnSurfaceSizeChanged;
    }

    /// <summary>Fired from the engine thread; UI subscribers must marshal to their dispatcher.</summary>
    internal event Action<string>? StatusChanged;

    internal void RequestPuzzle(string? imagePath, int columns, int rows)
    {
        lock (_requestGate)
            _requests.Enqueue(new PuzzleRequest(imagePath, columns, rows, null));
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

    private void OnSurfaceSizeChanged(object? sender, EventArgs args)
    {
        lock (_requestGate)
        {
            _clientSize = RenderSurface.ClientSize;
            _fitRequested = true;
        }
    }

    protected override void LoadAssets()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "assets");
        _slideSound = Engine.Managers.AudioResources.LoadFromFile(
            "slider-move", Path.Combine(root,
                "75143__willc2-45220__slide-cup-16b-44k-0-747s.wav"));
        _winSound = Engine.Managers.AudioResources.LoadFromFile(
            "slider-victory", Path.Combine(root,
                "177120__rdholder__2dogsound-tadaa1-3s-2013jan31-cc-by-30-us.wav"));
    }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();
        _layer = scene.AddLayer(WorldTiles, WorldTiles, WorldTileSize, WorldTileSize,
            zOrder: 0, parallax: 1f, coordinateSystem: CoordinateSystemTypes.Orthogonal);
        return scene;
    }

    protected override void OnSceneBound()
    {
        RenderSurface.Host.Backbuffer.ClearColor = new SKColor(18, 23, 35);
    }

    protected override void CreateSprites()
    {
        _board = new SlidingPuzzle(4, 3);
        _renderer = new PuzzleRenderer(_layer, Engine.Managers.Tilesheets, Engine.Managers.Sprites);
        using var image = CreateSampleImage();
        _renderer.Load(image, _board, _showGrid);
        FitPuzzle();
    }

    protected override void OnMouseAdapterInitialized()
    {
        var mouse = Engine.Input.MouseEventPoller!;
        mouse.MouseEvent += OnMouse;
        mouse.StartMonitoringMouse(trackMouseMovement: false, timeBetweenEvents: 0);
    }

    protected override void OnKeyboardAdapterInitialized()
    {
        var keyboard = Engine.Input.KeyboardEventPoller!;
        keyboard.KeyDown += OnKeyDown;
        foreach (Keys key in new[] { Keys.Up, Keys.Down, Keys.Left, Keys.Right })
            keyboard.StartMonitoringKey((int)key, key.ToString());
    }

    protected override void OnEngineInitialized()
    {
        Engine.Configuration.TargetFPS = 60;
        Engine.BeforeBackgroundTasksExecute += OnTick;
    }

    protected override void OnInitialized() => PublishStatus();

    private void OnTick()
    {
        List<PuzzleRequest> pending = [];
        bool fit;
        lock (_requestGate)
        {
            while (_requests.Count > 0)
                pending.Add(_requests.Dequeue());
            fit = _fitRequested;
            _fitRequested = false;
        }

        foreach (PuzzleRequest request in pending)
        {
            try
            {
                switch (request.Command)
                {
                    case PuzzleCommand.Shuffle:
                        CancelAnimation();
                        _board.Shuffle(_board.TotalPieces * 12);
                        _renderer.SnapToBoard(_board);
                        _timer.Reset();
                        _started = false;
                        _won = false;
                        PublishStatus();
                        break;
                    case PuzzleCommand.ShowGrid:
                    case PuzzleCommand.HideGrid:
                        _showGrid = request.Command == PuzzleCommand.ShowGrid;
                        _renderer.ShowGrid(_showGrid);
                        break;
                    case null:
                        LoadPicture(request);
                        fit = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke("Unable to load puzzle: " + ex.Message);
            }
        }

        if (fit)
            FitPuzzle();

        // Refresh the clock without touching any WinForms control from the engine thread.
        if (_timer.IsRunning && Stopwatch.GetTimestamp() - _lastStatusTicks >= Stopwatch.Frequency / 4)
            PublishStatus();
    }

    private void LoadPicture(PuzzleRequest request)
    {
        if (request.Columns is < 3 or > 20 || request.Rows is < 3 or > 20)
            throw new ArgumentOutOfRangeException(nameof(request), "Rows and columns must be 3–20.");

        // Decode first. An invalid file must not destroy the currently playable board.
        using SKBitmap image = request.ImagePath is null
            ? CreateSampleImage()
            : SKBitmap.Decode(request.ImagePath)
                ?? throw new InvalidDataException("Unsupported or corrupt picture.");

        var replacement = new SlidingPuzzle(request.Columns, request.Rows);
        CancelAnimation();
        _renderer.Load(image, replacement, _showGrid);
        _board = replacement;
        _started = false;
        _won = false;
        _timer.Reset();
        PublishStatus();
    }

    private void OnMouse(MouseEventArgs args)
    {
        if (!args.IsButtonJustPressed(MouseButton.Left) || _movingSprite is not null || _won)
            return;

        var view = RenderSurface.Host.ViewManager.Views[0];
        Sprite? sprite = Engine.Managers.Sprites
            .GetSpritesAtViewPixel(view, args.CurrentPosition, _layer)
            .FirstOrDefault();

        if (sprite is not null && int.TryParse(sprite.Nickname, out int piece))
            TryMovePiece(piece);
    }

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (args.KeyAction != KeyAction.Pressed || _movingSprite is not null || _won ||
            !Enum.TryParse(args.KeyConfig.Key, ignoreCase: true, out Keys key))
            return;

        // Direction refers to the piece's motion, not the empty square's motion.
        int x = _board.BlankColumn;
        int y = _board.BlankRow;
        switch (key)
        {
            case Keys.Left: x++; break;
            case Keys.Right: x--; break;
            case Keys.Up: y++; break;
            case Keys.Down: y--; break;
            default: return;
        }

        int piece = _board.PieceAt(x, y);
        if (piece >= 0)
            TryMovePiece(piece);
    }

    private void TryMovePiece(int piece)
    {
        Sprite? sprite = _renderer.GetPiece(piece);
        if (sprite is null)
            return;

        if (!_board.TryMovePiece(piece, out PuzzleMove move))
            return;

        if (!_started)
        {
            _started = true;
            _timer.Start();
        }

        _movingSprite = sprite;
        sprite.Movement.ScriptedMovementStopped += OnMoveStopped;
        sprite.Movement.MoveTo(_renderer.PositionForIndex(move.DestinationIndex),
            0.15f, null, 0.01f);
        _slideSound?.Play();
        PublishStatus();
    }

    private void OnMoveStopped(ScriptedMovement movement)
    {
        if (_movingSprite is null)
            return;
        _movingSprite.Movement.ScriptedMovementStopped -= OnMoveStopped;
        _movingSprite = null;

        if (_board.IsSolved && _started)
        {
            _timer.Stop();
            _won = true;
            _winSound?.Play();
        }

        PublishStatus();
    }

    private void CancelAnimation()
    {
        if (_movingSprite is null)
            return;
        _movingSprite.Movement.ScriptedMovementStopped -= OnMoveStopped;
        _movingSprite = null;
    }

    private void FitPuzzle()
    {
        Size size;
        lock (_requestGate)
            size = _clientSize;

        if (_renderer.WidthPx <= 0 || _renderer.HeightPx <= 0 ||
            size.Width <= 0 || size.Height <= 0)
            return;

        var view = RenderSurface.Host.ViewManager.Views[0];
        float zoom = Math.Min(
            size.Width / (float)_renderer.WidthPx,
            size.Height / (float)_renderer.HeightPx) * 0.92f;
        view.Viewport.SnapZoom(Math.Max(0.05f, zoom));
        float center = WorldTiles * WorldTileSize / 2f;
        view.Camera.CenterOn(new PointF(center, center));
    }

    private void PublishStatus()
    {
        _lastStatusTicks = Stopwatch.GetTimestamp();
        string result = _won ? "Solved!" : _started ? "In progress" : "Ready";
        StatusChanged?.Invoke(
            result + Environment.NewLine +
            "Correct: " + _board.CorrectPieces + " / " + _board.TotalPieces + Environment.NewLine +
            "Moves: " + _board.MoveCount + Environment.NewLine +
            "Time: " + _timer.Elapsed.ToString(@"mm\:ss"));
    }

    protected override void UnhookEvents()
    {
        if (Engine.Input.MouseEventPoller is not null)
            Engine.Input.MouseEventPoller.MouseEvent -= OnMouse;
        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;
        Engine.BeforeBackgroundTasksExecute -= OnTick;
        RenderSurface.SizeChanged -= OnSurfaceSizeChanged;
        CancelAnimation();
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

    private enum PuzzleCommand { Shuffle, ShowGrid, HideGrid }
    private sealed record PuzzleRequest(string? ImagePath, int Columns, int Rows, PuzzleCommand? Command);
}
