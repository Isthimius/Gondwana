using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Gondwana.Blazor.Rendering;

/// <summary>
/// Provides the shared DOM input surface used by Gondwana's Blazor bitmap and GPU render
/// components.
/// </summary>
/// <remarks>
/// The concrete render components intentionally use different presentation strategies, but both
/// expose the same keyboard, mouse, and touch event source to the Blazor input adapters.
/// </remarks>
public abstract class BlazorRenderSurfaceComponentBase : ComponentBase, IDisposable
{
    private const string DefaultCanvasStyle =
        "width: 100%; height: 100%; display: block; outline: none;";

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

    /// <summary>
    /// Gets or sets additional attributes applied to the underlying HTML canvas element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// Gets the canvas attributes with the component-owned style attribute removed.
    /// </summary>
    protected IReadOnlyDictionary<string, object>? CanvasAttributes { get; private set; }

    /// <summary>
    /// Gets the effective canvas style, including Gondwana's required defaults.
    /// </summary>
    protected string CanvasStyle { get; private set; } = DefaultCanvasStyle;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (AdditionalAttributes is null)
        {
            CanvasAttributes = null;
            CanvasStyle = DefaultCanvasStyle;
            return;
        }

        CanvasStyle = DefaultCanvasStyle;
        if (AdditionalAttributes.TryGetValue("style", out var styleObj) && styleObj is not null)
            CanvasStyle = $"{DefaultCanvasStyle} {styleObj}";

        var attributes = new Dictionary<string, object>(
            AdditionalAttributes,
            StringComparer.OrdinalIgnoreCase);
        attributes.Remove("style");
        CanvasAttributes = attributes;
    }

    /// <summary>Forwards a browser key-down event to registered input adapters.</summary>
    protected void HandleKeyDown(KeyboardEventArgs e) => KeyDown?.Invoke(e);

    /// <summary>Forwards a browser key-up event to registered input adapters.</summary>
    protected void HandleKeyUp(KeyboardEventArgs e) => KeyUp?.Invoke(e);

    /// <summary>Forwards a browser mouse-move event to registered input adapters.</summary>
    protected void HandleMouseMove(MouseEventArgs e) => MouseMove?.Invoke(e);

    /// <summary>Forwards a browser mouse-down event to registered input adapters.</summary>
    protected void HandleMouseDown(MouseEventArgs e) => MouseDown?.Invoke(e);

    /// <summary>Forwards a browser mouse-up event to registered input adapters.</summary>
    protected void HandleMouseUp(MouseEventArgs e) => MouseUp?.Invoke(e);

    /// <summary>Forwards a browser wheel event to registered input adapters.</summary>
    protected void HandleWheel(WheelEventArgs e) => Wheel?.Invoke(e);

    /// <summary>Forwards a browser touch-start event to registered input adapters.</summary>
    protected void HandleTouchStart(TouchEventArgs e) => TouchStart?.Invoke(e);

    /// <summary>Forwards a browser touch-move event to registered input adapters.</summary>
    protected void HandleTouchMove(TouchEventArgs e) => TouchMove?.Invoke(e);

    /// <summary>Forwards a browser touch-end event to registered input adapters.</summary>
    protected void HandleTouchEnd(TouchEventArgs e) => TouchEnd?.Invoke(e);

    /// <summary>Forwards a browser touch-cancel event to registered input adapters.</summary>
    protected void HandleTouchCancel(TouchEventArgs e) => TouchCancel?.Invoke(e);

    /// <summary>Releases resources owned by the concrete render surface component.</summary>
    public abstract void Dispose();
}
