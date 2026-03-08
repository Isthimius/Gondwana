namespace Gondwana.Rendering;

/// <summary>
/// Provides data for the <see cref="RenderSurfaceAdapterBase.Resized"/> event.
/// </summary>
/// <remarks>
/// This event argument class contains information about the render surface adapter that was resized,
/// including both the old and new dimensions.
/// </remarks>
public class RenderSurfaceAdapterResizedEventArgs
{
    /// <summary>
    /// Gets the render surface adapter that was resized.
    /// </summary>
    /// <value>The <see cref="RenderSurfaceAdapterBase"/> instance that triggered the resize event.</value>
    public RenderSurfaceAdapterBase RenderSurfaceAdapter { get; }

    /// <summary>
    /// Gets the previous width of the render surface adapter before the resize.
    /// </summary>
    /// <value>The old width in pixels.</value>
    public int OldWidth { get; }

    /// <summary>
    /// Gets the previous height of the render surface adapter before the resize.
    /// </summary>
    /// <value>The old height in pixels.</value>
    public int OldHeight { get; }

    /// <summary>
    /// Gets the new width of the render surface adapter after the resize.
    /// </summary>
    /// <value>The new width in pixels.</value>
    public int NewWidth { get; }

    /// <summary>
    /// Gets the new height of the render surface adapter after the resize.
    /// </summary>
    /// <value>The new height in pixels.</value>
    public int NewHeight { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderSurfaceAdapterResizedEventArgs"/> class.
    /// </summary>
    /// <param name="renderSurfaceAdapter">The render surface adapter that was resized.</param>
    /// <param name="oldWidth">The previous width of the render surface adapter in pixels.</param>
    /// <param name="oldHeight">The previous height of the render surface adapter in pixels.</param>
    /// <param name="newWidth">The new width of the render surface adapter in pixels.</param>
    /// <param name="newHeight">The new height of the render surface adapter in pixels.</param>
    public RenderSurfaceAdapterResizedEventArgs(RenderSurfaceAdapterBase renderSurfaceAdapter, int oldWidth, int oldHeight, int newWidth, int newHeight)
    {
        RenderSurfaceAdapter = renderSurfaceAdapter;
        OldWidth = oldWidth;
        OldHeight = oldHeight;
        NewWidth = newWidth;
        NewHeight = newHeight;
    }
}
