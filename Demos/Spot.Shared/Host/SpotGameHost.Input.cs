using System.Drawing;
using Gondwana.Input.Keyboard;
using Gondwana.Widgets;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameRuntime
{
    private bool _handleHumanInput;
    private WidgetInputRouter? _boardInputRouter;

    internal void OnMouseAdapterInitialized()
    {
        Engine.Input.MouseEventPoller?.StartMonitoringMouse();
    }

    internal void OnKeyboardAdapterInitialized()
    {
        if (Engine.Input.KeyboardEventPoller is null)
            return;

        Engine.Input.KeyboardEventPoller.KeyDown += KeyboardEventPoller_KeyDown;
        Engine.Input.KeyboardEventPoller.StartMonitoringKey(ScoreToggleKey);
        _configurePlatformKeyboardInput?.Invoke(Engine.Input.KeyboardEventPoller);
    }

    /// <summary>Enables touch polling; the widget router owns gameplay tap routing.</summary>
    internal void OnTouchAdapterInitialized()
    {
        Engine.Input.TouchEventPoller?.StartMonitoringTouch();
    }

    /// <summary>Receives primary mouse/touch gameplay actions not owned by any widget.</summary>
    private void HookBoardInput()
    {
        _boardInputRouter = WidgetInputRouter;
        if (_boardInputRouter is not null)
            _boardInputRouter.UnhandledPrimaryAction += WidgetInputRouter_UnhandledPrimaryAction;
    }

    internal void UnhookEvents()
    {
        _newGameDialog?.Dispose();
        _newGameDialog = null;
        Interlocked.Exchange(ref _dialogOpen, 0);

        if (_boardInputRouter is not null)
            _boardInputRouter.UnhandledPrimaryAction -= WidgetInputRouter_UnhandledPrimaryAction;
        _boardInputRouter = null;

        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= KeyboardEventPoller_KeyDown;

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

    private void WidgetInputRouter_UnhandledPrimaryAction(PrimaryPointerActionEventArgs args)
        => HandleBoardPress(args.ScreenPositionPx);

    private void HandleBoardPress(Point screenPos)
    {
        if (Volatile.Read(ref _dialogOpen) != 0 || !_handleHumanInput ||
            Scene is null || ActiveScene.SceneLayers.Count == 0 ||
            SurfaceHost.ViewManager.Views.Count == 0)
        {
            return;
        }

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
