using System.Threading;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameHost
{
    private int _dialogOpen; // 0 = not open; 1 = open/pending.
    private NewGameOptions? _lastNewGameOptions;
    private NewGameDialog? _newGameDialog;

    public NewGameOptions? LastNewGameOptions => _lastNewGameOptions;

    public void OpenNewGameDialog(NewGameOptions? newGameOptions = null)
    {
        if (Interlocked.CompareExchange(ref _dialogOpen, 1, 0) != 0)
        {
            _newGameDialog?.Activate();
            if (_newGameDialog is not null)
                WidgetInputRouter?.Focus(_newGameDialog.InitialFocusTarget);
            return;
        }

        if (SurfaceHost.ViewManager.Views.Count == 0)
        {
            Interlocked.Exchange(ref _dialogOpen, 0);
            return;
        }

        var view = SurfaceHost.ViewManager.Views[0];
        var dialog = new NewGameDialog(SurfaceHost, view, newGameOptions);
        var previousFocus = WidgetInputRouter?.FocusedWidget;
        _newGameDialog = dialog;

        dialog.Closed += result =>
        {
            NewGameOptions options = dialog.Options;
            _lastNewGameOptions = options;
            _newGameDialog = null;
            Interlocked.Exchange(ref _dialogOpen, 0);
            WidgetInputRouter?.Focus(previousFocus);

            if (result == Gondwana.Widgets.Dialogs.DialogResult.OK)
                Engine.EngineDispatcher.Post(() => StartNewGame(options));
        };

        dialog.Show();
        dialog.Activate();
        WidgetInputRouter?.Focus(dialog.InitialFocusTarget);
    }
}
