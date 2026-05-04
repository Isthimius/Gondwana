#if BROWSER
using System.Runtime.InteropServices.JavaScript;
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
    {
        // Import the Gondwana audio JS module so BrowserAudioManager can be used.
        await JSHost.ImportAsync("gondwana-audio", "./gondwana-audio.js");
        await BuildAvaloniaApp().StartBrowserAppAsync("out");
    }
}
#endif
