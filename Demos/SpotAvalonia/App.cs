using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Gondwana.Demos.SpotAvalonia;

internal sealed class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new GameWindow();
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            singleView.MainView = new GameView();

        base.OnFrameworkInitializationCompleted();
    }
}
