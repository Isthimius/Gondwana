using Gondwana.Widgets.Controls;
using Microsoft.JSInterop;

namespace Gondwana.Blazor;

/// <summary>
/// Opens external URIs in a new browser tab or window.
/// </summary>
public sealed class BlazorExternalUriLauncher : IExternalUriLauncher
{
    private readonly IJSRuntime _jsRuntime;

    /// <summary>Creates a browser URI launcher backed by the supplied JavaScript runtime.</summary>
    public BlazorExternalUriLauncher(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    /// <inheritdoc />
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
