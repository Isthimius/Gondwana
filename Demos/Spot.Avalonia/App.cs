using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace Gondwana.Demos.Spot;

internal sealed class App : Application
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new GameWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
