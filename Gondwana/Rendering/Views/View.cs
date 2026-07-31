using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering.Views;

/// <summary>
/// Represents a rendered view of a scene, combining a camera position with a viewport
/// configuration to control what portion of the world is visible and how it is displayed
/// on screen. Multiple views can be used to create split-screen, picture-in-picture,
/// or other multi-viewport rendering scenarios.
/// </summary>
public sealed class View
{
    /// <summary>
    /// Gets the unique identifier for this view instance.
    /// </summary>
    /// <value>
    /// A GUID that uniquely identifies this view throughout its lifetime.
    /// </value>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the camera that controls the world-space position and following behavior
    /// for this view. The camera determines which part of the world is visible.
    /// </summary>
    /// <value>
    /// The <see cref="Views.Camera"/> instance associated with this view.
    /// </value>
    public Camera Camera { get; }

    /// <summary>
    /// Gets the viewport that defines the screen-space rectangle, zoom level, and
    /// other rendering parameters for this view. The viewport controls how the
    /// camera's visible world region is mapped to screen pixels.
    /// </summary>
    /// <value>
    /// The <see cref="Views.Viewport"/> instance associated with this view.
    /// </value>
    public Viewport Viewport { get; }

    /// <summary>
    /// Controls the draw order of this view relative to other views.
    /// Lower values are drawn first (behind); higher values are drawn later (in front).
    /// </summary>
    public int ZOrder { get; set; } = 0;

    /// <summary>
    /// Gets or sets the minimum zoom level allowed for this View.
    /// </summary>
    public float MinZoom { get; set; } = 0.1f;

    /// <summary>
    /// Gets or sets the maximum zoom level allowed for this View.
    /// </summary>
    public float MaxZoom { get; set; } = 8f;

    internal View(Camera cam, Viewport vp)
    {
        Camera = cam;
        Viewport = vp;
        // Let camera clamp against THIS viewport's visible world size.
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
        if (layer is null)
            throw new ArgumentNullException(nameof(layer));

        targetZoom = Math.Clamp(targetZoom, MinZoom, MaxZoom);

        // World under cursor BEFORE zoom changes
        var worldUnderCursor = ScreenPxToWorldPx(layer, screenPoint);

        float offsetX = Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        float offsetY = Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;

        float parallax = layer.Parallax;
        if (Math.Abs(parallax) < 1e-6f)
            parallax = 1f;

        float localX = screenPoint.X - offsetX;
        float localY = screenPoint.Y - offsetY;

        // screen = offset + (world - camera*p) * zoom
        // camera = (world - local/zoom) / p
        float camTargetX = (worldUnderCursor.X - localX / targetZoom) / parallax;
        float camTargetY = (worldUnderCursor.Y - localY / targetZoom) / parallax;

        var cameraTargetUL = new PointF(camTargetX, camTargetY);

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

    /// <summary>
    /// Converts a screen-space pixel position into world-space coordinates
    /// for the specified scene layer, accounting for the view's camera position,
    /// viewport zoom, screen offsets, and the layer's parallax factor.
    /// </summary>
    /// <param name="layer">
    /// The scene layer whose parallax factor should be used for the conversion.
    /// </param>
    /// <param name="screenPx">
    /// The screen-space pixel position relative to the render surface.
    /// </param>
    /// <returns>
    /// The corresponding world-space pixel position.
    /// </returns>
    /// <remarks>
    /// The transformation formula is:
    /// <code>world = camera * parallax + (screen - offset) / zoom</code>
    /// This is commonly used for mouse picking and screen-to-world raycasting.
    /// </remarks>
    public PointF ScreenPxToWorldPx(SceneLayer layer, PointF screenPx)
    {
        float zoom = Viewport.Zoom <= 0f ? 1f : Viewport.Zoom;
        float offsetX = Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        float offsetY = Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;
        float parallax = layer.Parallax;

        float worldX = Camera.PositionPx.X * parallax
                     + (screenPx.X - offsetX) / zoom;

        float worldY = Camera.PositionPx.Y * parallax
                     + (screenPx.Y - offsetY) / zoom;

        return new PointF(worldX, worldY);
    }

    /// <summary>
    /// Converts a world-space pixel position into screen-space coordinates
    /// for this view, accounting for the camera position, viewport zoom,
    /// screen offsets, and the specified layer's parallax factor.
    /// </summary>
    /// <param name="layer">
    /// The scene layer whose parallax factor should be used for the conversion.
    /// </param>
    /// <param name="worldPx">
    /// The world-space pixel position to convert.
    /// </param>
    /// <returns>
    /// The corresponding screen-space pixel position on the render surface.
    /// </returns>
    /// <remarks>
    /// The transformation formula is:
    /// <code>screen = offset + (world - camera * parallax) * zoom</code>
    /// This is commonly used for rendering world objects to screen coordinates
    /// and for UI elements that track world positions.
    /// </remarks>
    public PointF WorldPxToScreenPx(SceneLayer layer, PointF worldPx)
    {
        float zoom = Viewport.Zoom <= 0f ? 1f : Viewport.Zoom;
        float offsetX = Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        float offsetY = Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;
        float parallax = layer.Parallax;

        float screenX = offsetX + (worldPx.X - Camera.PositionPx.X * parallax) * zoom;
        float screenY = offsetY + (worldPx.Y - Camera.PositionPx.Y * parallax) * zoom;

        return new PointF(screenX, screenY);
    }

    /// <summary>
    /// Converts a point in screen-space into the grid coordinate on the specified
    /// SceneLayer by first mapping the screen pixel to world-space, then letting the
    /// layer's coordinate system resolve the corresponding tile.
    /// </summary>
    /// <param name="layer">The SceneLayer whose grid the point should be mapped onto.</param>
    /// <param name="screenPx">The pixel position relative to the RenderSurface.</param>
    /// <returns>The grid coordinate (column/row or axial) under the screen pixel.</returns>
    public PointF ScreenPxToGrid(SceneLayer layer, PointF screenPx)
    {
        var worldPx = ScreenPxToWorldPx(layer, screenPx);
        return layer.CoordinateSystem.GetSceneLayerCoordinatesAtPixel(layer, worldPx);
    }

    /// <summary>
    /// Converts a world-space pixel rectangle into a screen-space rectangle
    /// for this View, using the specified layer's parallax factor.
    ///
    /// Matches the render path:
    ///   screen = offset + (world - camera * parallax) * zoom
    /// </summary>
    /// <param name="layer">Scene layer whose parallax should be applied.</param>
    /// <param name="worldRect">World-space rectangle (in pixels).</param>
    /// <returns>Screen-space rectangle on the render surface.</returns>
    public RectangleF WorldRectToScreenRect(SceneLayer layer, RectangleF worldRect)
    {
        if (layer is null)
            throw new ArgumentNullException(nameof(layer));

        float zoom = Viewport.Zoom <= 0f ? 1f : Viewport.Zoom;

        float offsetX = Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        float offsetY = Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;

        float parallax = layer.Parallax;

        // screen = offset + (world - camera * p) * zoom
        float localLeft = worldRect.Left - Camera.PositionPx.X * parallax;
        float localTop = worldRect.Top - Camera.PositionPx.Y * parallax;

        float scaledLeft = localLeft * zoom;
        float scaledTop = localTop * zoom;
        float scaledWidth = worldRect.Width * zoom;
        float scaledHeight = worldRect.Height * zoom;

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
    ///     screen = offset + (world - camera * p) * zoom
    /// </summary>
    public RectangleF ScreenRectToWorldRect(SceneLayer layer, RectangleF screenRect)
    {
        if (layer is null)
            throw new ArgumentNullException(nameof(layer));

        float zoom = Viewport.Zoom <= 0f ? 1f : Viewport.Zoom;

        float offsetX = Viewport.TargetRectPx.Left + Viewport.ScreenOffsetPx.X;
        float offsetY = Viewport.TargetRectPx.Top + Viewport.ScreenOffsetPx.Y;

        float parallax = layer.Parallax;

        // (screen - offset)
        float localLeft = screenRect.Left - offsetX;
        float localTop = screenRect.Top - offsetY;

        // world = camera*parallax + local / zoom
        float worldLeft = Camera.PositionPx.X * parallax + localLeft / zoom;
        float worldTop = Camera.PositionPx.Y * parallax + localTop / zoom;

        float worldWidth = screenRect.Width / zoom;
        float worldHeight = screenRect.Height / zoom;

        return new RectangleF(worldLeft, worldTop, worldWidth, worldHeight);
    }

    #endregion Coordinate conversion methods
}