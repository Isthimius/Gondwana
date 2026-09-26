using System.Threading;
using Gondwana.Input.Keyboard;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameHost
{
    private const int ScoreToggleKey = 9; // Tab virtual-key code.

    private bool _handleHumanInput;

    protected override void OnMouseAdapterInitialized()
    {
        if (Engine.Input.MouseEventPoller is null)
            return;

        Engine.Input.MouseEventPoller.MouseEvent += MouseEventPoller_MouseEvent;
        Engine.Input.MouseEventPoller.StartMonitoringMouse();
    }

    protected override void OnKeyboardAdapterInitialized()
    {
        if (Engine.Input.KeyboardEventPoller is null)
            return;

        Engine.Input.KeyboardEventPoller.KeyDown += KeyboardEventPoller_KeyDown;
        Engine.Input.KeyboardEventPoller.StartMonitoringKey(ScoreToggleKey);
    }

    protected override void UnhookEvents()
    {
        _newGameDialog?.Dispose();
        _newGameDialog = null;
        Interlocked.Exchange(ref _dialogOpen, 0);

        if (Engine.Input.MouseEventPoller is not null)
            Engine.Input.MouseEventPoller.MouseEvent -= MouseEventPoller_MouseEvent;

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

    private void MouseEventPoller_MouseEvent(Gondwana.Input.Mouse.MouseEventArgs args)
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

        var screenPos = args.CurrentPosition;

        if (args.LeftButtonJustPressed)
        {
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
}
