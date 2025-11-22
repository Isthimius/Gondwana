using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

public sealed class View
{
    public Camera Camera { get; }
    public Viewport Viewport { get; }

    /// <summary>
    /// Controls the draw order of this view relative to other views.
    /// Lower values are drawn first (behind); higher values are drawn later (in front).
    /// </summary>
    public int ZOrder { get; set; } = 0;

    internal View(Camera cam, Viewport vp)
    {
        Camera = cam;
        Viewport = vp;
        // Let camera clamp against THIS viewport’s visible world size.
        Camera.GetVisibleWorldSizePx = () => Viewport.VisibleWorldSizePx;
    }

    #region Coordinate conversion methods

    /// <summary>
    /// Converts a screen-space pixel position (relative to the render surface)
    /// into a world-space pixel position for this View. This applies the
    /// inverse viewport placement, zoom, and camera offset so the result can
    /// be used with SceneLayer/world-space logic.
    /// </summary>
    /// <param name="screenPx">
    /// Screen-space pixel position on the render surface.
    /// </param>
    /// <returns>
    /// The corresponding world-space pixel position.
    /// </returns>
    public PointF ScreenPxToWorldPx(PointF screenPx)
    {
        float zoom = (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);

        var offsetX = Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        var offsetY = Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;

        // screen = (world + offset) / zoom - camera
        // => world = zoom * (screen + camera) - offset
        float worldX = zoom * (screenPx.X + Camera.PositionPx.X) - offsetX;
        float worldY = zoom * (screenPx.Y + Camera.PositionPx.Y) - offsetY;

        return new PointF(worldX, worldY);
    }

    /// <summary>
    /// Converts a world-space pixel position into a screen-space pixel position
    /// for this View. This applies the camera offset, zoom, and viewport
    /// placement so the result matches where the world point is drawn on the
    /// render surface.
    /// </summary>
    /// <param name="worldPx">
    /// World-space pixel position.
    /// </param>
    /// <returns>
    /// The corresponding screen-space pixel position on the render surface.
    /// </returns>
    public PointF WorldPxToScreenPx(PointF worldPx)
    {
        float zoom = (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);

        var offsetX = Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        var offsetY = Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;

        // screen = (world + offset) / zoom - camera
        float screenX = (worldPx.X + offsetX) / zoom - Camera.PositionPx.X;
        float screenY = (worldPx.Y + offsetY) / zoom - Camera.PositionPx.Y;

        return new PointF(screenX, screenY);
    }

    /// <summary>
    /// Converts a point in screen-space into the grid coordinate on the specified
    /// SceneLayer by first mapping the screen pixel to world-space, then letting the
    /// layer’s coordinate system resolve the corresponding tile.
    /// </summary>
    /// <param name="layer">The SceneLayer whose grid the point should be mapped onto.</param>
    /// <param name="screenPx">The pixel position relative to the RenderSurface.</param>
    /// <returns>The grid coordinate (column/row or axial) under the screen pixel.</returns>
    public PointF ScreenPxToGrid(SceneLayer layer, PointF screenPx)
    {
        var world = ScreenPxToWorldPx(screenPx);
        return layer.CoordinateSystem.GetSceneLayerCoordinatesAtPixel(layer, world);
    }

    /// <summary>
    /// Converts a world-space pixel rectangle into a screen-space rectangle
    /// for this View. Each edge is mapped using the same transform as
    /// <see cref="WorldPxToScreenPx(PointF)"/>.
    /// </summary>
    /// <param name="worldRect">
    /// World-space rectangle.
    /// </param>
    /// <returns>
    /// The corresponding screen-space rectangle on the render surface.
    /// </returns>
    public RectangleF WorldRectToScreenRect(RectangleF worldRect)
    {
        float zoom = (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);

        // Subtract camera → camera-relative
        float localLeft = worldRect.Left - Camera.PositionPx.X;
        float localTop = worldRect.Top - Camera.PositionPx.Y;

        // Scale from world → screen (1 / zoom)
        float scaledLeft = localLeft / zoom;
        float scaledTop = localTop / zoom;
        float scaledWidth = worldRect.Width / zoom;
        float scaledHeight = worldRect.Height / zoom;

        // Place inside the viewport’s target rect
        float screenLeft = scaledLeft + Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        float screenTop = scaledTop + Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;

        return new RectangleF(screenLeft, screenTop, scaledWidth, scaledHeight);
    }

    /// <summary>
    /// Converts a screen-space pixel rectangle (relative to the render surface)
    /// into a world-space pixel rectangle for this View. Each edge is mapped
    /// using the same transform as <see cref="ScreenPxToWorldPx(PointF)"/>.
    /// </summary>
    /// <param name="screenRect">
    /// Screen-space rectangle on the render surface.
    /// </param>
    /// <returns>
    /// The corresponding world-space rectangle covered by this View.
    /// </returns>
    public RectangleF ScreenRectToWorldRect(RectangleF screenRect)
    {
        float zoom = (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);

        float offsetX = Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        float offsetY = Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;

        // world = zoom * (screen + camera) - offset
        float worldLeft = zoom * (screenRect.Left + Camera.PositionPx.X) - offsetX;
        float worldTop = zoom * (screenRect.Top + Camera.PositionPx.Y) - offsetY;
        float worldRight = zoom * (screenRect.Right + Camera.PositionPx.X) - offsetX;
        float worldBottom = zoom * (screenRect.Bottom + Camera.PositionPx.Y) - offsetY;

        return RectangleF.FromLTRB(worldLeft, worldTop, worldRight, worldBottom);
    }

    #endregion Coordinate conversion methods
}
