using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using SkiaSharp;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Renders a bitmap or image to a rectangular region with support for scaling modes, source clipping,
/// tinting, opacity, blend modes, and rotation.
/// </summary>
/// <remarks>
/// <para>
/// DirectImage provides a flexible way to display images (either <see cref="SKImage"/> or <see cref="SKBitmap"/>)
/// with extensive control over rendering appearance. It supports both world-space (scene-layer mode) and
/// screen-space (view mode) positioning, making it suitable for both in-game sprites and UI elements.
/// </para>
/// <para>
/// Key features:
/// <list type="bullet">
/// <item><description>Multiple scaling modes: stretch, fit, fill, center, and pixel-perfect.</description></item>
/// <item><description>Source rectangle clipping to display specific regions of an image (sprite sheets).</description></item>
/// <item><description>Color tinting and opacity control for visual effects.</description></item>
/// <item><description>Rotation around a configurable anchor point within the destination rectangle.</description></item>
/// <item><description>Customizable Skia blend modes and filter quality for advanced rendering.</description></item>
/// <item><description>Physics-based movement via inherited <see cref="DirectDrawingMovableBase"/> capabilities.</description></item>
/// </list>
/// </para>
/// <para>
/// Important: DirectImage does not take ownership of the provided <see cref="SKImage"/> or <see cref="SKBitmap"/>.
/// The caller is responsible for managing the lifetime of image resources and ensuring they remain valid
/// while the DirectImage is in use. Dispose of image resources only after disposing the DirectImage.
/// </para>
/// <para>
/// All setter methods return <c>this</c> to enable fluent-style method chaining. Each setter automatically
/// marks the affected screen regions as dirty to trigger rerendering on the next frame.
/// </para>
/// <para>
/// Thread safety: This class is not thread-safe. All operations should be performed on the UI thread.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create an image in world space with fit scaling and rotation
/// var image = new DirectImage(myBitmap, renderSurfaceHost, sceneLayer, worldBounds)
///     .SetScaleMode(DirectImage.ScaleMode.Fit)
///     .SetRotation(45f)
///     .SetAnchor(0.5f, 0.5f)
///     .SetTint(new SKColor(255, 200, 200))
///     .SetOpacity(200);
/// 
/// // Apply physics-based movement
/// image.Movement.ApplyImpulse(new Vector2(100, -50));
/// </code>
/// </example>
public sealed class DirectImage : DirectDrawingMovableBase
{
    /// <summary>
    /// Defines how an image is scaled to fit within the destination rectangle.
    /// </summary>
    /// <remarks>
    /// The scale mode determines the relationship between the source image dimensions and the
    /// destination rectangle, controlling whether the image is stretched, letterboxed, cropped,
    /// or displayed at native resolution.
    /// </remarks>
    public enum ScaleMode
    {
        /// <summary>
        /// Stretches the image to fill the entire destination rectangle, ignoring aspect ratio.
        /// The image may appear distorted if the aspect ratios do not match.
        /// </summary>
        Stretch,

        /// <summary>
        /// Scales the image to fit entirely within the destination rectangle while preserving aspect ratio.
        /// Letterboxing (empty space) may appear on the sides or top/bottom if aspect ratios differ.
        /// The image is centered within the destination rectangle.
        /// </summary>
        Fit,

        /// <summary>
        /// Scales the image to completely cover the destination rectangle while preserving aspect ratio.
        /// The image may be cropped on the sides or top/bottom if aspect ratios differ.
        /// The image is centered within the destination rectangle.
        /// </summary>
        Fill,

        /// <summary>
        /// Displays the image at its native resolution without scaling, centered in the destination rectangle.
        /// If the image is larger than the destination, it will be clipped.
        /// If the image is smaller, empty space will appear around it.
        /// </summary>
        Center,

        /// <summary>
        /// Scales the image by the largest integer factor that fits within the destination rectangle
        /// while preserving aspect ratio. Minimum scale factor is 1 (no scaling down).
        /// This mode is ideal for pixel art to avoid sub-pixel rendering artifacts.
        /// </summary>
        PixelPerfect
    }

    private SKImage? _image;        // primary storage
    private SKBitmap? _bitmap;      // optional if provided instead of SKImage
    private SKRect? _src;           // source sprite region (in image pixels)
    private ScaleMode _scale = ScaleMode.Stretch;

    private readonly SKPaint _paint = new()
    {
        IsAntialias = true,
        FilterQuality = SKFilterQuality.Medium,
        BlendMode = SKBlendMode.SrcOver,
        Color = SKColors.White // used for tint/alpha via color filter
    };

    // visual modifiers
    private SKColor? _tint;         // multiplied over image
    private byte _opacity = 255;    // 0..255

    // rotation
    private float _rotationDeg = 0f;

    // normalized anchor (0..1) where 0,0 = top-left of dest rect
    private float _anchorX = 0.5f, _anchorY = 0.5f;

    private DirectImage(SKBitmap bitmap,
                       RenderSurfaceHostBase renderSurfaceHost,
                       DirectDrawingMode mode,
                       SceneLayer? sceneLayer,
                       View? view,
                       Rectangle? screenBounds,
                       Rectangle? worldBounds,
                       string? name = null)
        : base(renderSurfaceHost, mode, sceneLayer, view, screenBounds, worldBounds, name)
    {
        SetBitmap(bitmap);
    }

    private DirectImage(SKImage image,
                        RenderSurfaceHostBase renderSurfaceHost,
                        DirectDrawingMode mode,
                        SceneLayer? sceneLayer,
                        View? view,
                        Rectangle? screenBounds,
                        Rectangle? worldBounds,
                        string? nickname = null)
        : base(renderSurfaceHost, mode, sceneLayer, view, screenBounds, worldBounds, nickname)
    {
        SetImage(image);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectImage"/> class for scene-layer (world-space) rendering using a bitmap.
    /// </summary>
    /// <param name="bitmap">The bitmap to display. Must not be <see langword="null"/>. The caller retains ownership and must manage its lifetime.</param>
    /// <param name="renderSurfaceHost">The render surface host that manages rendering for this image. Must not be <see langword="null"/>.</param>
    /// <param name="sceneLayer">The scene layer to which this image is attached. The image will be positioned in world coordinates relative to this layer.</param>
    /// <param name="worldBounds">The world-space bounds in pixels defining the image's position and size.</param>
    /// <param name="nickname">An optional human-readable name for this image, useful for debugging and identification.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> or <paramref name="renderSurfaceHost"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This constructor creates an image positioned in world coordinates that moves with the camera and is
    /// affected by the scene layer's parallax factor. Use this for in-game sprites, background elements,
    /// and other world-space visuals.
    /// </para>
    /// <para>
    /// The bitmap is converted to <see cref="SKImage"/> during rendering for optimal performance. The caller
    /// must ensure the bitmap remains valid for the lifetime of this DirectImage instance.
    /// </para>
    /// </remarks>
    public DirectImage (SKBitmap bitmap,
                        RenderSurfaceHostBase renderSurfaceHost,
                        SceneLayer sceneLayer,
                        Rectangle worldBounds,
                        string? nickname = null) : this(bitmap, renderSurfaceHost, DirectDrawingMode.SceneLayer, sceneLayer, null, null, worldBounds, nickname) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectImage"/> class for view (screen-space) rendering using a bitmap.
    /// </summary>
    /// <param name="bitmap">The bitmap to display. Must not be <see langword="null"/>. The caller retains ownership and must manage its lifetime.</param>
    /// <param name="renderSurfaceHost">The render surface host that manages rendering for this image. Must not be <see langword="null"/>.</param>
    /// <param name="view">The view to which this image is attached. The image will be positioned in screen coordinates relative to this view's viewport.</param>
    /// <param name="screenBounds">The screen-space bounds in pixels defining the image's position and size within the viewport.</param>
    /// <param name="nickname">An optional human-readable name for this image, useful for debugging and identification.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> or <paramref name="renderSurfaceHost"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This constructor creates an image positioned in screen coordinates that remains fixed on screen
    /// regardless of camera movement. Use this for UI elements, HUD overlays, menus, and other screen-space visuals.
    /// </para>
    /// <para>
    /// The bitmap is converted to <see cref="SKImage"/> during rendering for optimal performance. The caller
    /// must ensure the bitmap remains valid for the lifetime of this DirectImage instance.
    /// </para>
    /// </remarks>
    public DirectImage (SKBitmap bitmap,
                        RenderSurfaceHostBase renderSurfaceHost,
                        View view,
                        Rectangle screenBounds,
                        string? nickname = null) : this(bitmap, renderSurfaceHost, DirectDrawingMode.View, null, view, screenBounds, null, nickname) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectImage"/> class for scene-layer (world-space) rendering using a pre-encoded image.
    /// </summary>
    /// <param name="image">The image to display. Must not be <see langword="null"/>. The caller retains ownership and must manage its lifetime.</param>
    /// <param name="renderSurfaceHost">The render surface host that manages rendering for this image. Must not be <see langword="null"/>.</param>
    /// <param name="sceneLayer">The scene layer to which this image is attached. The image will be positioned in world coordinates relative to this layer.</param>
    /// <param name="worldBounds">The world-space bounds in pixels defining the image's position and size.</param>
    /// <param name="nickname">An optional human-readable name for this image, useful for debugging and identification.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="renderSurfaceHost"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This constructor creates an image positioned in world coordinates that moves with the camera and is
    /// affected by the scene layer's parallax factor. Use this for in-game sprites, background elements,
    /// and other world-space visuals.
    /// </para>
    /// <para>
    /// Using <see cref="SKImage"/> directly is more efficient than <see cref="SKBitmap"/> for rendering,
    /// especially when the same image is used multiple times. The caller must ensure the image remains
    /// valid for the lifetime of this DirectImage instance.
    /// </para>
    /// </remarks>
    public DirectImage(SKImage image,
                       RenderSurfaceHostBase renderSurfaceHost,
                       SceneLayer sceneLayer,
                       Rectangle worldBounds,
                       string? nickname = null) : this(image, renderSurfaceHost, DirectDrawingMode.SceneLayer, sceneLayer, null, null, worldBounds, nickname) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectImage"/> class for view (screen-space) rendering using a pre-encoded image.
    /// </summary>
    /// <param name="image">The image to display. Must not be <see langword="null"/>. The caller retains ownership and must manage its lifetime.</param>
    /// <param name="renderSurfaceHost">The render surface host that manages rendering for this image. Must not be <see langword="null"/>.</param>
    /// <param name="view">The view to which this image is attached. The image will be positioned in screen coordinates relative to this view's viewport.</param>
    /// <param name="screenBounds">The screen-space bounds in pixels defining the image's position and size within the viewport.</param>
    /// <param name="nickname">An optional human-readable name for this image, useful for debugging and identification.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="renderSurfaceHost"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This constructor creates an image positioned in screen coordinates that remains fixed on screen
    /// regardless of camera movement. Use this for UI elements, HUD overlays, menus, and other screen-space visuals.
    /// </para>
    /// <para>
    /// Using <see cref="SKImage"/> directly is more efficient than <see cref="SKBitmap"/> for rendering,
    /// especially when the same image is used multiple times. The caller must ensure the image remains
    /// valid for the lifetime of this DirectImage instance.
    /// </para>
    /// </remarks>
    public DirectImage(SKImage image,
                        RenderSurfaceHostBase renderSurfaceHost,
                        View view,
                        Rectangle screenBounds,
                        string? nickname = null) : this(image, renderSurfaceHost, DirectDrawingMode.View, null, view, screenBounds, null, nickname) { }

    /// <summary>
    /// Replaces the backing bitmap with a new one.
    /// </summary>
    /// <param name="bitmap">The new bitmap to display. Must not be <see langword="null"/>.</param>
    /// <returns>This <see cref="DirectImage"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bitmap"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This method replaces the current image source with the specified bitmap. The bitmap is converted
    /// to <see cref="SKImage"/> during rendering for optimal performance. Any previously set <see cref="SKImage"/>
    /// is discarded (but not disposed, as ownership remains with the caller).
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// The caller retains ownership of the bitmap and must ensure it remains valid while in use.
    /// </para>
    /// </remarks>
    public DirectImage SetBitmap(SKBitmap bitmap)
    {
        _bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        _image = null;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Replaces the backing image with a new one.
    /// </summary>
    /// <param name="image">The new image to display. Must not be <see langword="null"/>.</param>
    /// <returns>This <see cref="DirectImage"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This method replaces the current image source with the specified <see cref="SKImage"/>. Any previously
    /// set <see cref="SKBitmap"/> is discarded. Using <see cref="SKImage"/> directly is more efficient for
    /// rendering than converting from <see cref="SKBitmap"/>.
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// The caller retains ownership of the image and must ensure it remains valid while in use.
    /// </para>
    /// </remarks>
    public DirectImage SetImage(SKImage image)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
        _bitmap = null;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Sets the source rectangle within the image to display, enabling sprite sheet support.
    /// </summary>
    /// <param name="srcPixels">
    /// The source rectangle in image pixel coordinates, or <see langword="null"/> to display the entire image.
    /// Coordinates are relative to the top-left corner of the image.
    /// </param>
    /// <returns>This <see cref="DirectImage"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Use this method to display a specific region of a larger image, which is useful for sprite sheets,
    /// texture atlases, or animated sprites. When <see langword="null"/>, the entire image is displayed.
    /// </para>
    /// <para>
    /// The source rectangle dimensions do not need to match the destination rectangle; the specified region
    /// will be scaled according to the current <see cref="SetScaleMode"/>.
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectImage SetSourceRect(SKRect? srcPixels)
    {
        _src = srcPixels;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Sets how the image is scaled to fit within the destination rectangle.
    /// </summary>
    /// <param name="mode">The scaling mode to apply. See <see cref="ScaleMode"/> for available options.</param>
    /// <returns>This <see cref="DirectImage"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The scale mode determines how the source image (or source rectangle) is mapped to the destination
    /// bounds. Different modes preserve or ignore aspect ratio and handle size mismatches differently:
    /// </para>
    /// <para>
    /// <list type="bullet">
    /// <item><description><see cref="ScaleMode.Stretch"/> - Fills the destination, may distort the image.</description></item>
    /// <item><description><see cref="ScaleMode.Fit"/> - Preserves aspect ratio, may letterbox.</description></item>
    /// <item><description><see cref="ScaleMode.Fill"/> - Preserves aspect ratio, may crop.</description></item>
    /// <item><description><see cref="ScaleMode.Center"/> - No scaling, centers the image.</description></item>
    /// <item><description><see cref="ScaleMode.PixelPerfect"/> - Integer scaling for crisp pixel art.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectImage SetScaleMode(ScaleMode mode)
    {
        _scale = mode;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Applies a color tint (multiplicative blend) to the image.
    /// </summary>
    /// <param name="color">The tint color to apply. Each RGB channel is multiplied with the corresponding image pixel channel.</param>
    /// <returns>This <see cref="DirectImage"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The tint color is multiplied with each pixel of the source image using <see cref="SKBlendMode.Modulate"/>.
    /// White (<c>SKColors.White</c>) produces no color change, while other colors shift the image's hue.
    /// Black will result in a completely black image.
    /// </para>
    /// <para>
    /// The alpha channel of the tint color is ignored; use <see cref="SetOpacity"/> to control transparency.
    /// To remove the tint and restore original colors, call <see cref="ClearTint"/>.
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectImage SetTint(SKColor color)
    {
        _tint = color;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Removes any color tint, restoring the image's original colors.
    /// </summary>
    /// <returns>This <see cref="DirectImage"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// After calling this method, the image will be rendered with its original colors (equivalent to a white tint).
    /// Opacity settings from <see cref="SetOpacity"/> remain unaffected.
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectImage ClearTint()
    {
        _tint = null;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Sets the overall opacity (alpha transparency) of the image.
    /// </summary>
    /// <param name="alpha">The opacity value from 0 (fully transparent) to 255 (fully opaque).</param>
    /// <returns>This <see cref="DirectImage"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The opacity is applied uniformly to the entire image after any tint has been applied. An opacity
    /// of 0 makes the image invisible, while 255 renders it at full opacity.
    /// </para>
    /// <para>
    /// This opacity is independent of the <see cref="DirectDrawingBase.Opacity"/> property inherited from
    /// the base class. If both are set, their effects are combined multiplicatively during rendering.
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectImage SetOpacity(byte alpha)
    {
        _opacity = alpha;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Sets the Skia blend mode used when compositing this image onto the backbuffer.
    /// </summary>
    /// <param name="mode">The blend mode to apply. See <see cref="SKBlendMode"/> for available options.</param>
    /// <returns>This <see cref="DirectImage"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The blend mode determines how the image's pixels are combined with the existing backbuffer contents.
    /// Common modes include:
    /// <list type="bullet">
    /// <item><description><see cref="SKBlendMode.SrcOver"/> (default) - Standard alpha blending.</description></item>
    /// <item><description><see cref="SKBlendMode.Screen"/> - Brightening effect, useful for light effects.</description></item>
    /// <item><description><see cref="SKBlendMode.Multiply"/> - Darkening effect, useful for shadows.</description></item>
    /// <item><description><see cref="SKBlendMode.Plus"/> - Additive blending for glow effects.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectImage SetBlendMode(SKBlendMode mode)
    {
        _paint.BlendMode = mode;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Sets the filter quality used when scaling the image.
    /// </summary>
    /// <param name="q">
    /// The filter quality level. Higher quality produces smoother results but may be slower.
    /// See <see cref="SKFilterQuality"/> for available options.
    /// </param>
    /// <returns>This <see cref="DirectImage"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Filter quality affects how the image is sampled when scaling. Options include:
    /// <list type="bullet">
    /// <item><description><see cref="SKFilterQuality.None"/> - Nearest-neighbor sampling, ideal for pixel art.</description></item>
    /// <item><description><see cref="SKFilterQuality.Low"/> - Bilinear filtering, faster but may appear blurry.</description></item>
    /// <item><description><see cref="SKFilterQuality.Medium"/> (default) - Good balance of quality and performance.</description></item>
    /// <item><description><see cref="SKFilterQuality.High"/> - Highest quality, slowest performance.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// For pixel art games, use <see cref="SKFilterQuality.None"/> combined with <see cref="ScaleMode.PixelPerfect"/>
    /// to preserve crisp pixel boundaries.
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectImage SetFilterQuality(SKFilterQuality q)
    {
        _paint.FilterQuality = q;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Sets the rotation angle in degrees around the anchor point.
    /// </summary>
    /// <param name="degrees">The rotation angle in degrees. Positive values rotate clockwise, negative values rotate counter-clockwise.</param>
    /// <returns>This <see cref="DirectImage"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The image is rotated around the anchor point specified by <see cref="SetAnchor"/>. By default,
    /// the anchor is at the center (0.5, 0.5), so the image rotates around its center point.
    /// </para>
    /// <para>
    /// A rotation of 0 degrees means no rotation. Values are not clamped; you can specify any angle,
    /// and it will wrap around (e.g., 360 degrees is equivalent to 0 degrees).
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectImage SetRotation(float degrees)
    {
        _rotationDeg = degrees;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Sets the rotation and scaling anchor point within the destination rectangle using normalized coordinates.
    /// </summary>
    /// <param name="ax">The horizontal anchor position from 0.0 (left edge) to 1.0 (right edge). Values are clamped to this range.</param>
    /// <param name="ay">The vertical anchor position from 0.0 (top edge) to 1.0 (bottom edge). Values are clamped to this range.</param>
    /// <returns>This <see cref="DirectImage"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The anchor point determines the origin for rotation transformations. It is specified in normalized
    /// coordinates relative to the destination rectangle:
    /// <list type="bullet">
    /// <item><description>(0.0, 0.0) - Top-left corner</description></item>
    /// <item><description>(0.5, 0.5) - Center (default)</description></item>
    /// <item><description>(1.0, 1.0) - Bottom-right corner</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// For example, setting the anchor to (1.0, 0.5) and rotating will cause the image to rotate around
    /// its right-center edge, creating a door-opening effect.
    /// </para>
    /// <para>
    /// Values outside the 0..1 range are clamped. The affected screen regions are automatically marked
    /// as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectImage SetAnchor(float ax, float ay)
    {
        _anchorX = Math.Clamp(ax, 0f, 1f);
        _anchorY = Math.Clamp(ay, 0f, 1f);
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Renders the image to the backbuffer with all configured transformations and effects applied.
    /// </summary>
    /// <param name="backbuffer">The backbuffer providing the canvas and rendering context.</param>
    /// <param name="destRectScreen">The destination rectangle in screen pixel coordinates where the image should be rendered.</param>
    /// <remarks>
    /// <para>
    /// This method is called automatically by the rendering pipeline. It performs the following operations:
    /// <list type="number">
    /// <item><description>Converts the source bitmap to <see cref="SKImage"/> if necessary.</description></item>
    /// <item><description>Determines the source rectangle (full image or specified sub-rect).</description></item>
    /// <item><description>Computes the destination rectangle based on the scale mode.</description></item>
    /// <item><description>Applies tint and opacity via color filter.</description></item>
    /// <item><description>Applies rotation transformation around the anchor point if rotation is non-zero.</description></item>
    /// <item><description>Draws the image to the canvas with all effects applied.</description></item>
    /// <item><description>Disposes temporary <see cref="SKImage"/> instances created from bitmaps.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Do not call this method directly from game code. To trigger a redraw, call <see cref="DirectDrawingBase.ForceRefresh"/>
    /// or modify properties that affect appearance.
    /// </para>
    /// </remarks>
    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        var canvas = backbuffer.Canvas;

        // get SKImage to draw
        var img = _image ?? (_bitmap != null ? SKImage.FromBitmap(_bitmap) : null);
        if (img is null)
            return;

        // source rect (pixels in image space)
        SKRect src = _src ?? new SKRect(0, 0, img.Width, img.Height);

        // destination rect (screen space), computed by scale mode
        SKRect dst = ComputeDestRect(destRectScreen.ToPixelAlignedRect(), src, _scale);

        // apply tint/opacity via color filter
        // combine tint with opacity (multiply in linear-ish sRGB)
        var tint = _tint ?? SKColors.White;
        var withAlpha = new SKColor(tint.Red, tint.Green, tint.Blue, _opacity);
        using var cf = SKColorFilter.CreateBlendMode(withAlpha, SKBlendMode.Modulate);
        _paint.ColorFilter = cf;

        // rotation about anchor inside dst
        if (_rotationDeg != 0f)
        {
            float ax = dst.Left + _anchorX * dst.Width;
            float ay = dst.Top + _anchorY * dst.Height;
            canvas.Save();
            canvas.RotateDegrees(_rotationDeg, ax, ay);
        }

        // draw
        canvas.DrawImage(img, src, dst, _paint);

        if (_rotationDeg != 0f)
            canvas.Restore();

        // if we created a transient SKImage from a bitmap, dispose it now
        if (_image is null && img is not null)
            img.Dispose();
    }

    private static SKRect ComputeDestRect(Rectangle bounds, SKRect src, ScaleMode mode)
    {
        var dst = bounds.ToSKRect();

        if (mode == ScaleMode.Stretch)
            return dst;

        if (mode == ScaleMode.Center)  // center, no scale
        {
            float w = MathF.Min(dst.Width, src.Width);
            float h = MathF.Min(dst.Height, src.Height);
            float dx = dst.MidX - w * 0.5f, dy = dst.MidY - h * 0.5f;

            return new SKRect(dx, dy, dx + w, dy + h);
        }

        float srcW = src.Width, srcH = src.Height;
        float dstW = dst.Width, dstH = dst.Height;

        float scaleX = dstW / srcW;
        float scaleY = dstH / srcH;

        float scale = mode switch
        {
            ScaleMode.Fit => MathF.Min(scaleX, scaleY),
            ScaleMode.Fill => MathF.Max(scaleX, scaleY),
            ScaleMode.PixelPerfect => MathF.Max(1f, MathF.Floor(MathF.Min(scaleX, scaleY))), _ => 1f
        };

        float w2 = srcW * scale, h2 = srcH * scale;
        float x = dst.MidX - w2 * 0.5f;
        float y = dst.MidY - h2 * 0.5f;

        return new SKRect(x, y, x + w2, y + h2);
    }
}
