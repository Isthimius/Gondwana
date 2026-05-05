using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace MyGame;

internal sealed class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
#if !BROWSER
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new GameWindow();
        else
#endif
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            singleView.MainView = new GameView();

        base.OnFrameworkInitializationCompleted();
    }
}
