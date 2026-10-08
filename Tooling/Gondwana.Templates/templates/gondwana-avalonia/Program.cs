using Avalonia;

namespace MyGame;

internal static class Program
{
    /// <summary>
    /// Starts the application.
    /// </summary>
    /// <param name="args">The args.</param>
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Configures the Avalonia application builder.
    /// </summary>
    /// <returns>The resulting app builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
                     .UsePlatformDetect()
                     .LogToTrace();
}
