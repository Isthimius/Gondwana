namespace Gondwana.Blazor.Rendering;

internal static class SkiaNativeInitializationDiagnostic
{
    internal const string Guidance =
        "SkiaSharp native initialization failed. Check the application's TargetFramework, " +
        "the SDK selected by 'dotnet --version' (including global.json), and 'dotnet workload list'. " +
        "For the Gondwana .NET 10 Blazor template, use net10.0-browser with a .NET 10 SDK and " +
        "install wasm-tools using that SDK. Check project.assets.json for restored " +
        "SkiaSharp.NativeAssets.WebAssembly assets, then clean, restore and rebuild.";

    internal static bool IsMissingSkiaNativeLibrary(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DllNotFoundException &&
                current.Message.Contains("libSkiaSharp", StringComparison.OrdinalIgnoreCase))
                return true;

            if (current is AggregateException aggregate &&
                aggregate.InnerExceptions.Any(IsMissingSkiaNativeLibrary))
                return true;
        }

        return false;
    }
}
