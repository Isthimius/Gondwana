using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

public sealed class View
{
    public Camera Camera { get; }
    public Viewport Viewport { get; }

    internal View(Camera cam, Viewport vp)
    {
        Camera = cam;
        Viewport = vp;
        // Let camera clamp against THIS viewport’s visible world size.
        Camera.GetVisibleWorldSizePx = () => Viewport.VisibleWorldSizePx;
    }

    /// <summary>
    /// Converts a point in screen-space (RenderSurface pixel coordinates) into a
    /// world-space pixel position using this view’s camera, viewport offset, and zoom.
    /// </summary>
    /// <param name="screenPx">The pixel position relative to the RenderSurface.</param>
    /// <returns>The corresponding world-space pixel coordinate.</returns>
    public PointF ScreenToWorldPx(PointF screenPx)
    {
        float zoom = (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);

        float worldX =
            Camera.PositionPx.X +
            (screenPx.X - Viewport.TargetRectPx.Left - Viewport.ScreenOffsetPx.X) * zoom;

        float worldY =
            Camera.PositionPx.Y +
            (screenPx.Y - Viewport.TargetRectPx.Top - Viewport.ScreenOffsetPx.Y) * zoom;

        return new PointF(worldX, worldY);
    }

    public PointF WorldToScreenPx(PointF worldPx)
    {
        float zoom = (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);

        // Subtract camera
        float localX = worldPx.X - Camera.PositionPx.X;
        float localY = worldPx.Y - Camera.PositionPx.Y;

        // Apply inverse zoom
        localX /= zoom;
        localY /= zoom;

        // Apply viewport placement
        localX += Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        localY += Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;

        return new PointF(localX, localY);
    }

    /// <summary>
    /// Converts a point in screen-space into the grid coordinate on the specified
    /// SceneLayer by first mapping the screen pixel to world-space, then letting the
    /// layer’s coordinate system resolve the corresponding tile.
    /// </summary>
    /// <param name="layer">The SceneLayer whose grid the point should be mapped onto.</param>
    /// <param name="screenPx">The pixel position relative to the RenderSurface.</param>
    /// <returns>The grid coordinate (column/row or axial) under the screen pixel.</returns>
    public PointF ScreenToGrid(SceneLayer layer, PointF screenPx)
    {
        var world = ScreenToWorldPx(screenPx);
        return layer.CoordinateSystem.GetSceneLayerCoordinatesAtPixel(layer, world);
    }
}
