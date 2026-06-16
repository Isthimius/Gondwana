using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Gondwana.Blazor.Rendering;

/// <summary>
/// A Blazor component that presents Gondwana game frames on an HTML <c>&lt;canvas&gt;</c> element
/// and forwards keyboard, mouse, and touch events to the Gondwana input adapters.
/// </summary>
/// <remarks>
/// <para>
/// Place this component anywhere in your Blazor page layout and pass the instance to
/// <c>BlazorGameHost</c> (from <c>Gondwana.Blazor.Hosting</c>) to wire up the engine lifecycle.
/// </para>
/// <para>
/// The component must be visible in the DOM before <c>Initialize</c> is called on the host,
/// because rendering is initiated during <see cref="OnAfterRenderAsync"/> on the first render.
/// </para>
/// <para>
/// Keyboard events require the canvas to have focus. The component requests focus automatically
/// on first render; games may also call <c>element.focus()</c> from JavaScript as needed.
/// </para>
/// </remarks>
public sealed partial class BlazorBitmapRenderSurfaceComponent : IDisposable
{
    private ElementReference _canvasRef;
    private IJSObjectReference? _module;
    private bool _moduleLoaded;

    // Internal events consumed by input adapters (subscribed via BlazorGameHost / EngineExtensions).
    internal event Action<KeyboardEventArgs>? KeyDown;
    internal event Action<KeyboardEventArgs>? KeyUp;
    internal event Action<MouseEventArgs>? MouseMove;
    internal event Action<MouseEventArgs>? MouseDown;
    internal event Action<MouseEventArgs>? MouseUp;
    internal event Action<WheelEventArgs>? Wheel;
    internal event Action<TouchEventArgs>? TouchStart;
    internal event Action<TouchEventArgs>? TouchMove;
    internal event Action<TouchEventArgs>? TouchEnd;
    internal event Action<TouchEventArgs>? TouchCancel;

    /// <summary>Gets the render surface adapter that drives this component.</summary>
    public BlazorBitmapRenderSurfaceAdapter Adapter { get; private set; } = null!;

    /// <summary>Gets the <see cref="RenderSurfaceHost{T}"/> bound to this component.</summary>
    public RenderSurfaceHost<BitmapBackbuffer> Host { get; private set; } = null!;

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Adapter = new BlazorBitmapRenderSurfaceAdapter(this);
        Host = new RenderSurfaceHost<BitmapBackbuffer>(Adapter);
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _module = await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Gondwana.Blazor/gondwana-blazor.js");
        _moduleLoaded = true;

        // Request focus so keyboard events are received without requiring a click.
        try
        {
            await JS.InvokeVoidAsync("eval",
                "(function(){var c=document.querySelector('canvas[tabindex]');if(c)c.focus();})()");
        }
        catch
        {
            // Focus request is best-effort; ignore failures (e.g. server-side pre-render).
        }
    }

    /// <summary>
    /// Sends a rendered RGBA frame to the canvas.
    /// Safe to call from any thread; marshals to the component's synchronization context.
    /// </summary>
    /// <param name="rgbaPixels">RGBA pixel data (width × height × 4 bytes, unpremultiplied).</param>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    internal void EnqueueFrame(byte[] rgbaPixels, int width, int height)
    {
        // Fire-and-forget: the canvas is updated on the Blazor component's UI context.
        // In Blazor WASM (single-threaded), this queues a microtask that executes once
        // the current synchronous call stack yields.
        _ = InvokeAsync(async () =>
        {
            if (!_moduleLoaded || _module is null) return;
            await _module.InvokeVoidAsync("putImageData", _canvasRef, width, height, rgbaPixels);
        });
    }

    private void HandleKeyDown(KeyboardEventArgs e) => KeyDown?.Invoke(e);
    private void HandleKeyUp(KeyboardEventArgs e) => KeyUp?.Invoke(e);
    private void HandleMouseMove(MouseEventArgs e) => MouseMove?.Invoke(e);
    private void HandleMouseDown(MouseEventArgs e) => MouseDown?.Invoke(e);
    private void HandleMouseUp(MouseEventArgs e) => MouseUp?.Invoke(e);
    private void HandleWheel(WheelEventArgs e) => Wheel?.Invoke(e);
    private void HandleTouchStart(TouchEventArgs e) => TouchStart?.Invoke(e);
    private void HandleTouchMove(TouchEventArgs e) => TouchMove?.Invoke(e);
    private void HandleTouchEnd(TouchEventArgs e) => TouchEnd?.Invoke(e);
    private void HandleTouchCancel(TouchEventArgs e) => TouchCancel?.Invoke(e);

    /// <inheritdoc/>
    public void Dispose()
    {
        _ = _module?.DisposeAsync();
    }
}
