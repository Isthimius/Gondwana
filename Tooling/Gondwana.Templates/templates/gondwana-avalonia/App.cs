using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace MyGame;

internal sealed class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new GameWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
