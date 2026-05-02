using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace Gondwana.Demos.SpotAvalonia;

internal sealed class App : Application
{
    public override void Initialize()
    {
        // Register the Fluent theme so Avalonia controls (Menu, Button, etc.) have visual templates.
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new GameWindow();
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            singleView.MainView = new GameView();

        base.OnFrameworkInitializationCompleted();
    }
}
