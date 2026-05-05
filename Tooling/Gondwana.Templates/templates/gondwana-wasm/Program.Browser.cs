#if BROWSER
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Skia;

namespace MyGame;

internal static partial class Program
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
                     .UseSkia()
                     .LogToTrace();

    // Browser entry point: import the audio module, then start Avalonia.
    [SupportedOSPlatform("browser")]
    private static async Task Main(string[] args)
    {
        // Import the Gondwana audio JS module so BrowserAudioManager can be used.
        // The gondwana-audio.js file is served from wwwroot/ inside AppBundle/.
        await JSHost.ImportAsync("gondwana-audio", "./gondwana-audio.js");

        await BuildAvaloniaApp().StartBrowserAppAsync("out");
    }
}
#endif
