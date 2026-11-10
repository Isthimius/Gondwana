using Avalonia;

namespace Gondwana.Demos.Spot;

internal static class Program
{
    /// <summary>
    /// Configures the Avalonia application builder.
    /// </summary>
    /// <returns>The resulting app builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
                     .UsePlatformDetect()
                     // OpenGlControlBase requires a GPU compositor capable of
                     // sharing an OpenGL context. Do not silently fall back to
                     // software rendering, which leaves a blank game surface.
                     .With(new Win32PlatformOptions
                     {
                         RenderingMode = [Win32RenderingMode.Wgl, Win32RenderingMode.AngleEgl],
                         CompositionMode = [Win32CompositionMode.RedirectionSurface]
                     })
                     .LogToTrace();

    /// <summary>
    /// Starts the application.
    /// </summary>
    /// <param name="args">The args.</param>
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
}
