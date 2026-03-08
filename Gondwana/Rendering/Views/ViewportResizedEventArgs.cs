using System.Drawing;

namespace Gondwana.Rendering.Views;

/// <summary>
/// Provides data for the <see cref="Viewport.TargetRectChanged"/> event,
/// which is raised when a viewport's target rectangle is resized or repositioned
/// on the render surface.
/// </summary>
public class ViewportResizedEventArgs
{
    /// <summary>
    /// Gets the viewport instance that was resized or repositioned.
    /// </summary>
    /// <value>
    /// The <see cref="Views.Viewport"/> that raised the event.
    /// </value>
    public Viewport Viewport { get; }

    /// <summary>
    /// Gets the previous target rectangle before the resize or reposition operation.
    /// </summary>
    /// <value>
    /// A <see cref="Rectangle"/> representing the viewport's screen-space bounds
    /// before the change occurred.
    /// </value>
    public Rectangle OldRect { get; }

    /// <summary>
    /// Gets the new target rectangle after the resize or reposition operation.
    /// </summary>
    /// <value>
    /// A <see cref="Rectangle"/> representing the viewport's current screen-space bounds
    /// after the change occurred.
    /// </value>
    public Rectangle NewRect { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewportResizedEventArgs"/> class
    /// with the specified viewport and rectangle values.
    /// </summary>
    /// <param name="viewport">The viewport that was resized or repositioned.</param>
    /// <param name="oldRect">The target rectangle before the change.</param>
    /// <param name="newRect">The target rectangle after the change.</param>
    public ViewportResizedEventArgs(Viewport viewport, Rectangle oldRect, Rectangle newRect)
    {
        Viewport = viewport;
        OldRect = oldRect;
        NewRect = newRect;
    }
}
