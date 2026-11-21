using Gondwana.Drawing.Sprites;
using Gondwana.Input.Keyboard;
using SkiaSharp;

namespace Gondwana.Demos.Slider;

internal sealed partial class SliderGameHost
{
    private SliderMenu? _menu;
    private IReadOnlyList<SliderScore> _highScores = SliderPersistence.LoadScores();
    private byte[]? _imagePng;
    private TimeSpan _elapsedBeforeResume;

    internal bool HasMenuOrDialogOpen => HasMenuOrDialogOpen || _menu?.HasOpenMenu == true;

    internal event Action<SliderSave>? SaveGameRequested;
    internal event Action? LoadGameRequested;
    internal event Action? ExitRequested;

    internal void RequestRestore(SliderSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        lock (_requestGate)
            _requests.Enqueue(new PuzzleRequest(null, 0, 0, PuzzleCommand.Restore, SavedGame: save));
    }

    private void CreateMenu()
    {
        _menu = new SliderMenu(RenderSurface.Host, _uiView, _gameView,
            newPuzzle: () => RequestPuzzle(_picturePath, _sidebar!.Columns, _sidebar.Rows),
            openPicture: () => OpenPictureRequested?.Invoke(_sidebar!.Columns, _sidebar.Rows),
            shuffle: RequestShuffle,
            save: () => SaveGameRequested?.Invoke(CaptureGame()),
            load: () => LoadGameRequested?.Invoke(),
            highScores: () => _menu?.ShowHighScores(_highScores),
            exit: () => ExitRequested?.Invoke());
    }

    private SliderSave CaptureGame() => new()
    {
        Columns = _board.Columns,
        Rows = _board.Rows,
        Cells = _board.Cells.ToArray(),
        MoveCount = _board.MoveCount,
        ElapsedTicks = (_elapsedBeforeResume + _timer.Elapsed).Ticks,
        Started = _started,
        Won = _won,
        ImagePngBase64 = _imagePng is null ? null : Convert.ToBase64String(_imagePng)
    };

    private void CapturePicture(SKBitmap image)
    {
        using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 90)
            ?? throw new InvalidDataException("Could not encode the puzzle image.");
        _imagePng = encoded.ToArray();
    }

    private void RestoreSavedGame(SliderSave save)
    {
        SliderPersistence.Validate(save);
        SlidingPuzzle restored = SlidingPuzzle.Restore(
            save.Columns, save.Rows, save.Cells, save.MoveCount);

        using SKBitmap image = string.IsNullOrEmpty(save.ImagePngBase64)
            ? CreateSampleImage()
            : SKBitmap.Decode(Convert.FromBase64String(save.ImagePngBase64))
                ?? throw new InvalidDataException("The saved puzzle image is invalid.");

        CancelAnimation();
        _renderer.Load(image, restored, _showGrid);
        _board = restored;
        _picturePath = null;
        CapturePicture(image);

        _elapsedBeforeResume = TimeSpan.FromTicks(save.ElapsedTicks);
        _timer.Reset();
        _started = save.Started;
        _won = save.Won;
        if (_started && !_won)
            _timer.Start();

        _cameraFitPending = true;
        PublishStatus();
    }

    private void RecordHighScore()
    {
        _highScores = SliderPersistence.AddScore(_highScores,
            new SliderScore(_board.Columns, _board.Rows, _board.MoveCount,
                (_elapsedBeforeResume + _timer.Elapsed).Ticks, DateTime.UtcNow));
        try
        {
            SliderPersistence.SaveScores(_highScores);
        }
        catch (IOException)
        {
            _sidebar?.SetStatus("Puzzle solved. High score could not be saved to disk.");
        }
        catch (UnauthorizedAccessException)
        {
            _sidebar?.SetStatus("Puzzle solved. High score could not be saved to disk.");
        }
    }

    private void OnPrimaryPointerAction(Gondwana.Widgets.PrimaryPointerActionEventArgs args)
    {
        if (!ReferenceEquals(args.View, _gameView) || _movingSprite is not null ||
            _shuffleMoves is not null || _won || HasMenuOrDialogOpen)
            return;

        Sprite? sprite = Engine.Managers.Sprites
            .GetSpritesAtViewPixel(_gameView, args.ScreenPositionPx, _layer)
            .FirstOrDefault();
        if (sprite is not null && int.TryParse(sprite.Nickname, out int piece))
            TryMovePiece(piece);
    }

    private void UpdateShiftLabels()
    {
        // The adapter updates modifiers on the UI thread; reading its snapshot
        // also handles lost key-up events when the window loses focus.
        KeyboardModifierState modifiers = Engine.Input.KeyboardEventPoller?.Adapter?
            .CurrentKeyboardModifiers ?? KeyboardModifierState.None;
        _renderer.ShowDestinations((modifiers & KeyboardModifierState.Shift) != 0);
    }
}
