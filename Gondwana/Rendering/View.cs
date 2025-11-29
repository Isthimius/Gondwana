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

    public PointF ScreenPxToWorldPx(SceneLayer layer, PointF screenPx)
    {
        float zoom = (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);

        float offsetX = Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        float offsetY = Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;

        float parallax = layer.Parallax;

        // screen = offset + (world - camera * p) / zoom
        // => world = (screen - offset) * zoom + camera * p

        float worldX = (screenPx.X - offsetX) * zoom + Camera.PositionPx.X * parallax;
        float worldY = (screenPx.Y - offsetY) * zoom + Camera.PositionPx.Y * parallax;

        return new PointF(worldX, worldY);
    }

    public PointF WorldPxToScreenPx(SceneLayer layer, PointF worldPx)
    {
        float zoom = (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);

        float offsetX = Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        float offsetY = Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;

        float parallax = layer.Parallax;

        // screen = offset + (world - camera * p) / zoom
        float screenX = offsetX + (worldPx.X - Camera.PositionPx.X * parallax) / zoom;
        float screenY = offsetY + (worldPx.Y - Camera.PositionPx.Y * parallax) / zoom;

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
        var world = ScreenPxToWorldPx(layer, screenPx);
        return layer.CoordinateSystem.GetSceneLayerCoordinatesAtPixel(layer, world);
    }

    /// <summary>
    /// Converts a world-space pixel rectangle into a screen-space rectangle
    /// for this View, using the specified layer's parallax factor.
    ///
    /// Matches the render path:
    ///   screen = offset + (world - camera * parallax) / zoom
    /// </summary>
    /// <param name="layer">Scene layer whose parallax should be applied.</param>
    /// <param name="worldRect">World-space rectangle (in pixels).</param>
    /// <returns>Screen-space rectangle on the render surface.</returns>
    public RectangleF WorldRectToScreenRect(SceneLayer layer, RectangleF worldRect)
    {
        if (layer is null)
            throw new ArgumentNullException(nameof(layer));

        float zoom = (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);
        float inverseZoom = 1f / zoom;

        float offsetX = Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        float offsetY = Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;

        float parallax = layer.Parallax;

        // screen = offset + (world - camera * p) / zoom
        float localLeft = worldRect.Left - Camera.PositionPx.X * parallax;
        float localTop = worldRect.Top - Camera.PositionPx.Y * parallax;

        float scaledLeft = localLeft * inverseZoom;
        float scaledTop = localTop * inverseZoom;
        float scaledWidth = worldRect.Width * inverseZoom;
        float scaledHeight = worldRect.Height * inverseZoom;

        float screenLeft = offsetX + scaledLeft;
        float screenTop = offsetY + scaledTop;

        return new RectangleF(screenLeft, screenTop, scaledWidth, scaledHeight);
    }

    /// <summary>
    /// Converts a screen-space rectangle (on the adapter) into a world-space rectangle
    /// for the given layer, respecting zoom, camera position, viewport offsets,
    /// and the layer's parallax factor.
    ///
    /// Inverse of:
    ///     screen = offset + (world - camera * p) / zoom
    /// </summary>
    public RectangleF ScreenRectToWorldRect(SceneLayer layer, RectangleF screenRect)
    {
        if (layer is null)
            throw new ArgumentNullException(nameof(layer));

        float zoom = (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);

        float offsetX = Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        float offsetY = Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;

        float parallax = layer.Parallax;

        // (screen - offset)
        float localLeft = screenRect.Left - offsetX;
        float localTop = screenRect.Top - offsetY;

        // world = camera*parallax + local * zoom
        float worldLeft = Camera.PositionPx.X * parallax + localLeft * zoom;
        float worldTop = Camera.PositionPx.Y * parallax + localTop * zoom;

        float worldWidth = screenRect.Width * zoom;
        float worldHeight = screenRect.Height * zoom;

        return new RectangleF(worldLeft, worldTop, worldWidth, worldHeight);
    }

    #endregion Coordinate conversion methods
}
