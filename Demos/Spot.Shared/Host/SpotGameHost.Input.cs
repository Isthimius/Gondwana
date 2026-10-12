using System.Drawing;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Touch.Gestures;
using Gondwana.Widgets;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameRuntime
{
    private bool _handleHumanInput;

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
            Engine.Input.TouchEventPoller.TouchEvent -= TouchEventPoller_TouchEvent;

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
        if (args.IsTap)
            HandleBoardPress(args.Tap!.Position);
    }

    private void HandleBoardPress(Point screenPos)
    {
        if (Volatile.Read(ref _dialogOpen) != 0)
            return;

        if (!_handleHumanInput)
            return;

        if (Scene is null || ActiveScene.SceneLayers.Count == 0)
            return;

        if (SurfaceHost.ViewManager.Views.Count == 0)
            return;

        var view = SurfaceHost.ViewManager.Views[0];
        var layer = ActiveScene.SceneLayers[0];

        // Widgets own presses over their bounds, including menu dropdowns and dialogs.
        // A tap is recognized before the widget router dispatches its release/click.
        if (Engine.Managers.DirectDrawings.DirectDrawings.OfType<WidgetBase>().Any(widget =>
                ReferenceEquals(widget.RenderSurfaceHost, SurfaceHost) && widget.HitTest(view, screenPos)))
            return;

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
