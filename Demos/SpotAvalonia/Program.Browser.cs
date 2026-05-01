#if BROWSER
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Skia;

namespace Gondwana.Demos.SpotAvalonia;

internal static partial class Program
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
                     .UseSkia()
                     .LogToTrace();

    [SupportedOSPlatform("browser")]
    private static async Task Main(string[] args)
        => await BuildAvaloniaApp().StartBrowserAppAsync("out");
}
#endif
