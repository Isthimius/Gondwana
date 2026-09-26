using Gondwana.Widgets.Controls;
using Microsoft.JSInterop;

namespace Gondwana.Demos.Spot;

internal sealed class BlazorExternalUriLauncher : IExternalUriLauncher
{
    private readonly IJSRuntime _jsRuntime;

    internal BlazorExternalUriLauncher(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    public async ValueTask OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        cancellationToken.ThrowIfCancellationRequested();

        await _jsRuntime.InvokeVoidAsync(
            "open",
            cancellationToken,
            uri.AbsoluteUri,
            "_blank",
            "noopener,noreferrer");
    }
}
