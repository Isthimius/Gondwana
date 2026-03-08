using System.Drawing;

namespace Gondwana.Rendering.Views;

/// <summary>
/// Provides data for the <see cref="Viewport.ZoomChanged"/> event,
/// which is raised when a viewport's zoom level changes either through
/// direct assignment or animated zoom transitions.
/// </summary>
public class ViewportZoomChangedEventArgs
{
    /// <summary>
    /// Gets the viewport instance whose zoom level changed.
    /// </summary>
    /// <value>
    /// The <see cref="Views.Viewport"/> that raised the event.
    /// </value>
    public Viewport Viewport { get; }

    /// <summary>
    /// Gets the previous zoom level before the change occurred.
    /// </summary>
    /// <value>
    /// A <see cref="float"/> representing the zoom factor before the change.
    /// Values greater than 1 indicate zoom in; values between 0 and 1 indicate zoom out.
    /// </value>
    public float OldZoom { get; }

    /// <summary>
    /// Gets the new zoom level after the change occurred.
    /// </summary>
    /// <value>
    /// A <see cref="float"/> representing the current zoom factor after the change.
    /// Values greater than 1 indicate zoom in; values between 0 and 1 indicate zoom out.
    /// </value>
    public float NewZoom { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewportZoomChangedEventArgs"/> class
    /// with the specified viewport and zoom values.
    /// </summary>
    /// <param name="viewport">The viewport whose zoom level changed.</param>
    /// <param name="oldZoom">The zoom level before the change.</param>
    /// <param name="newZoom">The zoom level after the change.</param>
    public ViewportZoomChangedEventArgs(Viewport viewport, float oldZoom, float newZoom)
    {
        Viewport = viewport;
        OldZoom = oldZoom;
        NewZoom = newZoom;
    }
}
