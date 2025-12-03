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

    /// <summary>
    /// Smoothly zooms the view so that a given screen-space point appears to
    /// zoom in/out around a fixed world position beneath it, similar to
    /// map-style mouse-wheel zoom. Both the viewport zoom and camera position
    /// are animated over the specified duration.
    /// </summary>
    /// <param name="layer">
    /// Reference layer whose parallax factor is used for the world-space transform.
    /// Typically the main gameplay layer (parallax = 1).
    /// </param>
    /// <param name="screenPoint">
    /// Mouse position in adapter/screen pixels relative to the render surface.
    /// </param>
    /// <param name="targetZoom">
    /// Desired zoom factor after the animation completes.
    /// </param>
    /// <param name="durationSeconds">
    /// Approximate duration in seconds for the zoom + pan animation.
    /// Values &lt;= 0 snap immediately.
    /// </param>
    public void ZoomAroundScreenPoint(SceneLayer layer, PointF screenPoint, float targetZoom, float durationSeconds)
    {
        if (layer is null) throw new ArgumentNullException(nameof(layer));

        // 1) Clamp target zoom to something sane (optional – adjust to taste)
        float minZoom = 0.1f;
        float maxZoom = 8f;
        targetZoom = Math.Clamp(targetZoom, minZoom, maxZoom);

        // 2) Compute the world position under the cursor BEFORE zoom changes.
        var worldUnderCursor = ScreenPxToWorldPx(layer, screenPoint);

        // 3) Compute the offset used in screen-space transforms.
        float offsetX = Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        float offsetY = Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;

        float parallax = layer.Parallax;
        if (Math.Abs(parallax) < 1e-6f)
            parallax = 1f; // safety: avoid divide-by-zero

        // 4) Compute the camera position that keeps worldUnderCursor pinned
        // at the same screenPoint when zoom = targetZoom.
        float localX = screenPoint.X - offsetX;
        float localY = screenPoint.Y - offsetY;

        float camTargetX = (worldUnderCursor.X - localX * targetZoom) / parallax;
        float camTargetY = (worldUnderCursor.Y - localY * targetZoom) / parallax;

        var cameraTargetUL = new PointF(camTargetX, camTargetY);

        // 5) Animate both zoom and camera over the same duration.
        if (durationSeconds <= 0f)
        {
            Viewport.SnapZoom(targetZoom);
            Camera.SnapTo(cameraTargetUL);
        }
        else
        {
            Viewport.ZoomToOverDuration(targetZoom, durationSeconds);
            Camera.PanToOverDuration(cameraTargetUL, durationSeconds);
        }
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
