using Avalonia;

namespace Gondwana.Demos.SpotAvalonia;

internal static partial class Program
{
#if !BROWSER
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
                     .UsePlatformDetect()
                     .LogToTrace();

    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
#endif
}
