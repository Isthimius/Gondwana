using System.Drawing;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Touch;
using Gondwana.Input.Touch.Gestures;
using Gondwana.Widgets;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameRuntime
{
    private bool _handleHumanInput;
    private readonly HashSet<int> _boardTouchContacts = [];

    internal void OnMouseAdapterInitialized()
    {
        if (Engine.Input.MouseEventPoller is null)
            return;

        Engine.Input.MouseEventPoller.MouseEvent += MouseEventPoller_MouseEvent;
        Engine.Input.MouseEventPoller.StartMonitoringMouse();
    }

    internal void OnKeyboardAdapterInitialized()
    {
        if (Engine.Input.KeyboardEventPoller is null)
            return;

        Engine.Input.KeyboardEventPoller.KeyDown += KeyboardEventPoller_KeyDown;
        Engine.Input.KeyboardEventPoller.StartMonitoringKey(ScoreToggleKey);
        _configurePlatformKeyboardInput?.Invoke(Engine.Input.KeyboardEventPoller);
    }

    /// <summary>Connects recognized touch taps to the board's selection and move handling.</summary>
    internal void OnTouchAdapterInitialized()
    {
        if (Engine.Input.TouchEventPoller is null)
            return;

        Engine.Input.TouchEventPoller.TouchBegan += TouchEventPoller_TouchBegan;
        Engine.Input.TouchEventPoller.TouchEnded += TouchEventPoller_TouchEnded;
        Engine.Input.TouchEventPoller.TouchEvent += TouchEventPoller_TouchEvent;
        Engine.Input.TouchEventPoller.StartMonitoringTouch();
    }

    internal void UnhookEvents()
    {
        _newGameDialog?.Dispose();
        _newGameDialog = null;
        Interlocked.Exchange(ref _dialogOpen, 0);

        if (Engine.Input.MouseEventPoller is not null)
            Engine.Input.MouseEventPoller.MouseEvent -= MouseEventPoller_MouseEvent;

        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= KeyboardEventPoller_KeyDown;

        if (Engine.Input.TouchEventPoller is not null)
        {
            Engine.Input.TouchEventPoller.TouchBegan -= TouchEventPoller_TouchBegan;
            Engine.Input.TouchEventPoller.TouchEnded -= TouchEventPoller_TouchEnded;
            Engine.Input.TouchEventPoller.TouchEvent -= TouchEventPoller_TouchEvent;
        }

        _boardTouchContacts.Clear();

        UnhookSpotGameEvents();
    }


    private void KeyboardEventPoller_KeyDown(KeyDownEventArgs args)
    {
        if (Volatile.Read(ref _dialogOpen) != 0)
            return;

        if (args.KeyAction != KeyAction.Pressed)
            return;

        if (int.TryParse(args.KeyConfig.Key, out int key) && key == ScoreToggleKey)
            SetScoreVisible(!_showScores);
    }

    private void MouseEventPoller_MouseEvent(Gondwana.Input.Mouse.MouseEventArgs args)
    {
        if (args.LeftButtonJustPressed)
            HandleBoardPress(args.CurrentPosition);
    }

    private void TouchEventPoller_TouchEvent(GestureEventArgs args)
    {
        if (args.IsTap && _boardTouchContacts.Contains(args.Tap!.TouchId))
            HandleBoardPress(args.Tap!.Position);
    }

    private void TouchEventPoller_TouchBegan(object? sender, TouchEventArgs args)
    {
        // This runs before widget routing. Keep ownership even if a widget hides on press.
        if (CanHandleBoardPress(args.Touch.Position))
            _boardTouchContacts.Add(args.Touch.Id);
    }

    private void TouchEventPoller_TouchEnded(object? sender, TouchEventArgs args)
        => _boardTouchContacts.Remove(args.Touch.Id);

    private bool CanHandleBoardPress(Point screenPos)
    {
        if (Volatile.Read(ref _dialogOpen) != 0)
            return false;

        if (!_handleHumanInput)
            return false;

        if (Scene is null || ActiveScene.SceneLayers.Count == 0)
            return false;

        if (SurfaceHost.ViewManager.Views.Count == 0)
            return false;

        var view = SurfaceHost.ViewManager.Views[0];

        // Widgets own presses over their bounds, including menu dropdowns and dialogs.
        return !Engine.Managers.DirectDrawings.DirectDrawings.OfType<WidgetBase>().Any(widget =>
            ReferenceEquals(widget.RenderSurfaceHost, SurfaceHost) && widget.HitTest(view, screenPos));
    }

    private void HandleBoardPress(Point screenPos)
    {
        if (!CanHandleBoardPress(screenPos))
            return;

        var view = SurfaceHost.ViewManager.Views[0];
        var layer = ActiveScene.SceneLayers[0];
        var selectedCoord = view.ScreenPxToGrid(layer, screenPos);

        if (selectedCoord.X >= 0 && selectedCoord.X < layer.GridColumnCount &&
            selectedCoord.Y >= 0 && selectedCoord.Y < layer.GridRowCount)
        {
            var cell = SpotGame.SpotGameField.GetCell((int)selectedCoord.X, (int)selectedCoord.Y);

            if (SpotGame.AttemptSelectCell(cell, out var playerMovement) && playerMovement != null)
                SpotGame.ExecuteMove(playerMovement.Value);
        }
    }
}
