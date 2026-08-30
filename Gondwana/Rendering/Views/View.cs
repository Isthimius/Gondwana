using System.Drawing;
using Gondwana.Effects;
using Gondwana.Logging;
using Gondwana.Scenes;
using Microsoft.Extensions.Logging;

namespace Gondwana.Rendering.Views;

/// <summary>
/// Represents a rendered view of a scene, combining a camera position with a viewport
/// configuration to control what portion of the world is visible and how it is displayed
/// on screen. Multiple views can be used to create split-screen, picture-in-picture,
/// or other multi-viewport rendering scenarios.
/// </summary>
public sealed class View
{
    private SceneLayer? _zoomAnchorLayer;
    private PointF _zoomAnchorScreenPoint;
    private PointF _zoomAnchorWorldPoint;

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

    // Presentation-only state owned by EffectsManager. These values never alter
    // camera, viewport, world, or collision state.
    internal float EffectOpacity { get; set; } = 1f;
    internal float EffectReveal { get; set; } = 1f;
    internal EffectDirection EffectRevealDirection { get; set; } = EffectDirection.FromLeftToRight;
    internal PointF EffectOffsetFactor { get; set; } = PointF.Empty;
    internal PointF EffectOffsetPx { get; set; } = PointF.Empty;

    internal bool HasPresentationEffect =>
        EffectOpacity < 0.9999f
        || EffectReveal < 0.9999f
        || Math.Abs(EffectOffsetFactor.X) > 0.0001f
        || Math.Abs(EffectOffsetFactor.Y) > 0.0001f
        || Math.Abs(EffectOffsetPx.X) > 0.0001f
        || Math.Abs(EffectOffsetPx.Y) > 0.0001f;

    internal bool BlocksViewsBelow => !HasPresentationEffect;

    internal View(Camera cam, Viewport vp)
    {
        Camera = cam;
        Viewport = vp;
        // Let camera clamp against THIS viewport's visible world size.
        Camera.GetVisibleWorldSizePx = () => Viewport.VisibleWorldSizePx;
    }

    /// <summary>
    /// Zooms the view around a screen-space point while keeping the world point
    /// beneath that location fixed throughout the operation.
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
    /// Duration in seconds for the zoom animation. Values &lt;= 0 snap immediately.
    /// </param>
    /// <remarks>
    /// During a smooth zoom, the viewport zoom is animated and the camera position
    /// is recomputed after every intermediate zoom update. This prevents the anchor
    /// point from drifting while zoom and camera movement are in progress.
    /// </remarks>
    public void ZoomAroundScreenPoint(SceneLayer layer, PointF screenPoint, float targetZoom, float durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(layer);

        targetZoom = Math.Clamp(targetZoom, MinZoom, MaxZoom);

        PointF worldUnderCursor = ScreenPxToWorldPx(layer, screenPoint);

        // Anchored zoom owns camera positioning until the zoom completes. Cancel an
        // unrelated explicit pan, but leave follow configuration intact so it can
        // resume on the next update after the anchored zoom finishes.
        Camera.CancelPan();

        if (durationSeconds <= 0f)
        {
            ClearZoomAnchor();
            Viewport.SnapZoom(targetZoom);
            SnapCameraToZoomAnchor(layer, screenPoint, worldUnderCursor);
            return;
        }

        _zoomAnchorLayer = layer;
        _zoomAnchorScreenPoint = screenPoint;
        _zoomAnchorWorldPoint = worldUnderCursor;

        Viewport.ZoomToOverDuration(targetZoom, durationSeconds);
    }

    /// <summary>
    /// Advances camera and zoom state for this view.
    /// </summary>
    internal void Update(float dtSeconds)
    {
        if (_zoomAnchorLayer is { } anchorLayer)
        {
            // Update zoom first, then derive the one camera position that keeps
            // the selected world point at the selected screen point.
            Viewport.UpdateZoom(dtSeconds);
            SnapCameraToZoomAnchor(
                anchorLayer,
                _zoomAnchorScreenPoint,
                _zoomAnchorWorldPoint);

            if (!Viewport.IsZoomAnimating)
                ClearZoomAnchor();

            return;
        }

        Camera.Update(dtSeconds);
        Viewport.UpdateZoom(dtSeconds);
    }

    private void SnapCameraToZoomAnchor(
        SceneLayer layer,
        PointF screenPoint,
        PointF worldPoint)
    {
        float zoom = Viewport.Zoom > 0f
            ? Viewport.Zoom
            : 1f;

        PointF effectOffset = GetEffectOffsetPx(layer);

        float offsetX =
            Viewport.TargetRectPx.Left
            + Viewport.ScreenOffsetPx.X
            + effectOffset.X;

        float offsetY =
            Viewport.TargetRectPx.Top
            + Viewport.ScreenOffsetPx.Y
            + effectOffset.Y;

        float parallax = layer.Parallax;
        if (Math.Abs(parallax) < 1e-6f)
            parallax = 1f;

        float localX = screenPoint.X - offsetX;
        float localY = screenPoint.Y - offsetY;

        // screen = offset + (world - camera*p) * zoom
        // camera = (world - local/zoom) / p
        float cameraX =
            (worldPoint.X - localX / zoom)
            / parallax;

        float cameraY =
            (worldPoint.Y - localY / zoom)
            / parallax;

        Camera.SnapTo(new PointF(cameraX, cameraY));
    }

    private void ClearZoomAnchor()
    {
        _zoomAnchorLayer = null;
        _zoomAnchorScreenPoint = PointF.Empty;
        _zoomAnchorWorldPoint = PointF.Empty;
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
        RenderContext? context = GetCurrentRenderContext();
        float zoom = context?.ViewportZoom
            ?? (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);
        Rectangle targetRect = context?.ViewportTargetRectPx
            ?? Viewport.TargetRectPx;
        PointF screenOffset = context?.ViewportScreenOffsetPx
            ?? Viewport.ScreenOffsetPx;
        PointF cameraPosition = context?.CameraPositionPx
            ?? Camera.PositionPx;
        PointF effectOffset = GetEffectOffsetPx(layer);
        float offsetX = targetRect.Left + screenOffset.X + effectOffset.X;
        float offsetY = targetRect.Top + screenOffset.Y + effectOffset.Y;
        float parallax = layer.Parallax;

        float worldX = cameraPosition.X * parallax
                     + (screenPx.X - offsetX) / zoom;

        float worldY = cameraPosition.Y * parallax
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
        RenderContext? context = GetCurrentRenderContext();
        float zoom = context?.ViewportZoom
            ?? (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);
        Rectangle targetRect = context?.ViewportTargetRectPx
            ?? Viewport.TargetRectPx;
        PointF screenOffset = context?.ViewportScreenOffsetPx
            ?? Viewport.ScreenOffsetPx;
        PointF cameraPosition = context?.CameraPositionPx
            ?? Camera.PositionPx;
        PointF effectOffset = GetEffectOffsetPx(layer);
        float offsetX = targetRect.Left + screenOffset.X + effectOffset.X;
        float offsetY = targetRect.Top + screenOffset.Y + effectOffset.Y;
        float parallax = layer.Parallax;

        float screenX = offsetX + (worldPx.X - cameraPosition.X * parallax) * zoom;
        float screenY = offsetY + (worldPx.Y - cameraPosition.Y * parallax) * zoom;

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

        RenderContext? context = GetCurrentRenderContext();
        float zoom = context?.ViewportZoom
            ?? (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);
        Rectangle targetRect = context?.ViewportTargetRectPx
            ?? Viewport.TargetRectPx;
        PointF screenOffset = context?.ViewportScreenOffsetPx
            ?? Viewport.ScreenOffsetPx;
        PointF cameraPosition = context?.CameraPositionPx
            ?? Camera.PositionPx;

        PointF effectOffset = GetEffectOffsetPx(layer);
        float offsetX = targetRect.Left + screenOffset.X + effectOffset.X;
        float offsetY = targetRect.Top + screenOffset.Y + effectOffset.Y;

        float parallax = layer.Parallax;

        // screen = offset + (world - camera * p) * zoom
        float localLeft = worldRect.Left - cameraPosition.X * parallax;
        float localTop = worldRect.Top - cameraPosition.Y * parallax;

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

        RenderContext? context = GetCurrentRenderContext();
        float zoom = context?.ViewportZoom
            ?? (Viewport.Zoom <= 0f ? 1f : Viewport.Zoom);
        Rectangle targetRect = context?.ViewportTargetRectPx
            ?? Viewport.TargetRectPx;
        PointF screenOffset = context?.ViewportScreenOffsetPx
            ?? Viewport.ScreenOffsetPx;
        PointF cameraPosition = context?.CameraPositionPx
            ?? Camera.PositionPx;

        PointF effectOffset = GetEffectOffsetPx(layer);
        float offsetX = targetRect.Left + screenOffset.X + effectOffset.X;
        float offsetY = targetRect.Top + screenOffset.Y + effectOffset.Y;

        float parallax = layer.Parallax;

        // (screen - offset)
        float localLeft = screenRect.Left - offsetX;
        float localTop = screenRect.Top - offsetY;

        // world = camera*parallax + local / zoom
        float worldLeft = cameraPosition.X * parallax + localLeft / zoom;
        float worldTop = cameraPosition.Y * parallax + localTop / zoom;

        float worldWidth = screenRect.Width / zoom;
        float worldHeight = screenRect.Height / zoom;

        return new RectangleF(worldLeft, worldTop, worldWidth, worldHeight);
    }

    internal PointF GetEffectOffsetPx(SceneLayer? layer)
    {
        RenderContext? context = GetCurrentRenderContext();
        PointF factor = context?.ViewEffectOffsetFactor
            ?? EffectOffsetFactor;
        PointF pixels = context?.ViewEffectOffsetPx
            ?? EffectOffsetPx;
        Rectangle targetRect = context?.ViewportTargetRectPx
            ?? Viewport.TargetRectPx;

        if (layer is not null)
        {
            factor = new PointF(
                factor.X + layer.EffectOffsetFactor.X,
                factor.Y + layer.EffectOffsetFactor.Y);
            pixels = new PointF(
                pixels.X + layer.EffectOffsetPx.X,
                pixels.Y + layer.EffectOffsetPx.Y);
        }

        return new PointF(
            pixels.X + factor.X * targetRect.Width,
            pixels.Y + factor.Y * targetRect.Height);
    }

    internal RectangleF GetPresentationBoundsPx()
    {
        PointF offset = GetEffectOffsetPx(layer: null);
        RectangleF viewport = GetRenderViewportTargetRectPx();

        return new RectangleF(
            viewport.Left + offset.X,
            viewport.Top + offset.Y,
            viewport.Width,
            viewport.Height);
    }

    internal Rectangle GetRenderViewportTargetRectPx() =>
        GetCurrentRenderContext()?.ViewportTargetRectPx
        ?? Viewport.TargetRectPx;

    private RenderContext? GetCurrentRenderContext()
    {
        RenderContext? context = RenderContext.Current;
        return context is not null && ReferenceEquals(context.View, this)
            ? context
            : null;
    }

    #endregion Coordinate conversion methods
}
