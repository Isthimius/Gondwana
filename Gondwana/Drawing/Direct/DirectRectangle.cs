using System.Drawing;
using System.Runtime.CompilerServices;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.Timers;
using SkiaSharp;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Renders a configurable rectangle with support for fill, border, rounded corners, dash patterns,
/// color pulsing, and pattern fills.
/// </summary>
/// <remarks>
/// <para>
/// DirectRectangle provides a high-performance, retained-mode rectangle rendering solution with extensive
/// customization options. It supports both world-space (scene-layer mode) and screen-space (view mode)
/// positioning, making it suitable for both in-game UI elements and world-space markers.
/// </para>
/// <para>
/// Key features:
/// <list type="bullet">
/// <item><description>Configurable fill color and optional distinct border color with independent alpha channels.</description></item>
/// <item><description>Stroke width and alignment control (inside, outside, or centered on the rectangle boundary).</description></item>
/// <item><description>Rounded corners with configurable radius.</description></item>
/// <item><description>Dash patterns for creating dashed or dotted borders.</description></item>
/// <item><description>Color pulsing animations for fill and/or border (sine or triangle wave).</description></item>
/// <item><description>Pattern fills using tiled bitmaps with configurable tiling modes and scaling.</description></item>
/// <item><description>Blend mode support for advanced compositing effects (screen, multiply, additive, etc.).</description></item>
/// <item><description>Physics-based movement via inherited <see cref="DirectDrawingMovableBase"/> capabilities.</description></item>
/// </list>
/// </para>
/// <para>
/// Performance characteristics: DirectRectangle caches <see cref="SKPaint"/> instances and rebuilds them
/// only when properties change, minimizing GC pressure and native interop overhead. Stroke alignment and
/// dash patterns are computed per-frame but use efficient math operations.
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
/// // Filled panel with a distinct border and rounded corners
/// var panel = new DirectRectangle(Color.SteelBlue, renderSurfaceHost, view, screenBounds)
///     .SetFilled(true)
///     .SetBorderColor(Color.Navy)
///     .SetStrokeWidth(4f)
///     .SetCornerRadius(12f)
///     .SetDashPattern(8f, 4f); // dashed outline
///
/// // Soft glow using blend mode
/// var glow = new DirectRectangle(Color.FromArgb(64, 255, 200, 0), renderSurfaceHost, view, screenBounds2)
///     .SetFilled(true)
///     .SetBlendMode(SKBlendMode.Screen);
///
/// // Pulsing alert border
/// glow.PulseBorder(Color.FromArgb(255, 255, 64, 64), Color.FromArgb(80, 255, 0, 0), 1.2f);
/// </code>
/// </example>
public class DirectRectangle : DirectDrawingMovableBase
{
    /// <summary>
    /// Defines how an image fills the rectangle interior.
    /// </summary>
    public enum ImageFillMode
    {
        /// <summary>Stretches the image to the full rectangle without preserving aspect ratio.</summary>
        Stretch,

        /// <summary>Fits the entire image inside the rectangle while preserving aspect ratio.</summary>
        Fit,

        /// <summary>Covers the rectangle while preserving aspect ratio and clips any overflow.</summary>
        Fill,

        /// <summary>Draws the image at native size, centered and clipped to the rectangle.</summary>
        Center,

        /// <summary>Uses the largest whole-number scale that fits, never scaling below native size.</summary>
        PixelPerfect,

        /// <summary>Repeats the image as tiles across the rectangle.</summary>
        Repeat
    }

    private readonly SKPaint _fillPaint;        // cached fill
    private readonly SKPaint _strokePaint;      // cached stroke
    private readonly SKPaint _imagePaint = new()
    {
        IsAntialias = true,
        FilterQuality = SKFilterQuality.Medium,
        BlendMode = SKBlendMode.SrcOver
    };

    private SKColor? _borderColor;              // optional distinct border color
    private SKShader? _fillShader;              // optional fill shader
    private SKImage? _fillImage;
    private SKBitmap? _fillBitmap;
    private ImageFillMode _imageFillMode = ImageFillMode.Stretch;
    private float _imageScale = 1f;
    private SKPoint _imageOffsetPx;
    private bool _resourcesDisposed;

    private bool _isFilled;
    private float _cornerRadius;
    private float[]? _dashPattern;
    private StrokeAlign _strokeAlign = StrokeAlign.Center;
    private bool _needsRebuildPaints = true; // mark when properties change

    // --- Pulse settings ---
    private bool _pulseFillEnabled;
    private SKColor _pulseFillFrom, _pulseFillTo;
    private float _pulseFillPeriodSec = 1f;

    private bool _pulseBorderEnabled;
    private SKColor _pulseBorderFrom, _pulseBorderTo;
    private float _pulseBorderPeriodSec = 1f;

    private enum PulseWave { Sine, Triangle }
    private PulseWave _pulseFillWave = PulseWave.Sine;
    private PulseWave _pulseBorderWave = PulseWave.Sine;

    // --- Time keeping for Update(tick) ---
    private long _pulseLastTick = 0;
    private float _timeSec; // accumulated seconds

    private DirectRectangle(Color color,
                           RenderSurfaceHostBase renderSurfaceHost,
                           DirectDrawingMode mode,
                           SceneLayer? sceneLayer,
                           View? view,
                           Rectangle? screenBounds,
                           Rectangle? worldBounds,
                           string? nickname = null)
        : base(renderSurfaceHost, mode, sceneLayer, view, screenBounds, worldBounds, nickname)
    {
        // initialize with defaults; actual paints built lazily
        _fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        _strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        SetColor(color);                 // sets base color and marks rebuild
        SetBlendMode(SKBlendMode.SrcOver);
        SetFilled(false);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectRectangle"/> class for scene-layer (world-space) rendering.
    /// </summary>
    /// <param name="color">The base color used for fill and/or stroke (if no distinct border color is set).</param>
    /// <param name="renderSurfaceHost">The render surface host that manages rendering for this rectangle. Must not be <see langword="null"/>.</param>
    /// <param name="sceneLayer">The scene layer to which this rectangle is attached. The rectangle will be positioned in world coordinates relative to this layer.</param>
    /// <param name="worldBounds">The world-space bounds in pixels defining the rectangle's position and size.</param>
    /// <param name="nickname">An optional human-readable name for this rectangle, useful for debugging and identification.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderSurfaceHost"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This constructor creates a rectangle positioned in world coordinates that moves with the camera and is
    /// affected by the scene layer's parallax factor. Use this for in-game UI elements, selection markers,
    /// health bars, and other world-space visuals.
    /// </para>
    /// <para>
    /// By default, the rectangle is created unfilled (outline only) with a 1-pixel stroke width. Use
    /// <see cref="SetFilled"/> and <see cref="SetStrokeWidth"/> to customize the appearance.
    /// </para>
    /// </remarks>
    public DirectRectangle(Color color,
                           RenderSurfaceHostBase renderSurfaceHost,
                           SceneLayer sceneLayer,
                           Rectangle worldBounds,
                           string? nickname = null)
        : this(color, renderSurfaceHost, DirectDrawingMode.SceneLayer, sceneLayer, null, null, worldBounds, nickname) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectRectangle"/> class for view (screen-space) rendering.
    /// </summary>
    /// <param name="color">The base color used for fill and/or stroke (if no distinct border color is set).</param>
    /// <param name="renderSurfaceHost">The render surface host that manages rendering for this rectangle. Must not be <see langword="null"/>.</param>
    /// <param name="view">The view to which this rectangle is attached. The rectangle will be positioned in screen coordinates relative to this view's viewport.</param>
    /// <param name="screenBounds">The screen-space bounds in pixels defining the rectangle's position and size within the viewport.</param>
    /// <param name="nickname">An optional human-readable name for this rectangle, useful for debugging and identification.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderSurfaceHost"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This constructor creates a rectangle positioned in screen coordinates that remains fixed on screen
    /// regardless of camera movement. Use this for HUD panels, buttons, borders, focus indicators, and other
    /// screen-space UI elements.
    /// </para>
    /// <para>
    /// By default, the rectangle is created unfilled (outline only) with a 1-pixel stroke width. Use
    /// <see cref="SetFilled"/> and <see cref="SetStrokeWidth"/> to customize the appearance.
    /// </para>
    /// </remarks>
    public DirectRectangle(Color color,
                           RenderSurfaceHostBase renderSurfaceHost,
                           View view,
                           Rectangle screenBounds,
                           string? nickname = null)
        : this(color, renderSurfaceHost, DirectDrawingMode.View, null, view, screenBounds, null, nickname) { }

    /// <summary>
    /// Sets the base color used for both fill and stroke (unless a distinct border color is specified).
    /// </summary>
    /// <param name="color">The new base color.</param>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// This color is used for the fill when <see cref="SetFilled"/> is true, and for the stroke (border)
    /// unless a distinct border color has been set via <see cref="SetBorderColor"/>.
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// If color pulsing is active, calling this method will not affect the pulsing animation until
    /// <see cref="StopPulses"/> is called.
    /// </para>
    /// </remarks>
    public DirectRectangle SetColor(Color color)
    {
        // store as stroke/ fill base via rebuild
        var sk = color.ToSKColor();
        // Set on one paint; rebuild will propagate
        _fillPaint.Color = sk;
        _strokePaint.Color = sk;
        _needsRebuildPaints = true;
        return this;
    }

    /// <summary>
    /// Sets a distinct border (stroke) color, independent of the fill color.
    /// </summary>
    /// <param name="color">The border color to apply.</param>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// When a border color is set, the stroke uses this color instead of the base color set by
    /// <see cref="SetColor"/>. This allows for rectangles with different fill and outline colors,
    /// such as a white panel with a dark border.
    /// </para>
    /// <para>
    /// The border color has its own alpha channel, independent of the fill's alpha. To remove the
    /// distinct border color and revert to using the base color, call <see cref="SetColor"/> again
    /// without setting a border color, or set the border color to match the base color.
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// If border pulsing is active, calling this method will not affect the pulsing animation until
    /// <see cref="StopPulses"/> is called.
    /// </para>
    /// </remarks>
    public DirectRectangle SetBorderColor(Color color)
    {
        _borderColor = color.ToSKColor();
        _needsRebuildPaints = true;
        return this;
    }

    /// <summary>
    /// Sets the alpha (transparency) channel of the base color.
    /// </summary>
    /// <param name="alpha">The alpha value from 0 (fully transparent) to 255 (fully opaque).</param>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method modifies only the alpha channel of the base color, preserving the RGB components.
    /// It affects the fill transparency and, if no distinct border color is set, also affects the
    /// stroke transparency.
    /// </para>
    /// <para>
    /// If a distinct border color has been set via <see cref="SetBorderColor"/>, its alpha remains
    /// unchanged by this method. To modify the border alpha independently, call <see cref="SetBorderColor"/>
    /// with a color containing the desired alpha value.
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectRectangle SetAlpha(int alpha)
    {
        var c = _fillPaint.Color;
        var withA = new SKColor(c.Red, c.Green, c.Blue, (byte)alpha);
        _fillPaint.Color = withA;
        // Only change stroke when it's not using a distinct border color
        if (_borderColor is null)
            _strokePaint.Color = withA;
        _needsRebuildPaints = true;
        return this;
    }

    /// <summary>
    /// Sets whether the rectangle interior is filled with the base color (or pattern).
    /// </summary>
    /// <param name="isFilled">
    /// <see langword="true"/> to fill the rectangle interior; <see langword="false"/> to draw only the outline (stroke).
    /// </param>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// When filled, the rectangle's interior is drawn using the fill paint (base color or pattern).
    /// When not filled, only the stroke (outline) is visible if <see cref="SetStrokeWidth"/> is greater than zero.
    /// </para>
    /// <para>
    /// You can combine fill and stroke to create rectangles with both a filled interior and a visible border,
    /// which is common for UI panels and buttons.
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectRectangle SetFilled(bool isFilled)
    {
        _isFilled = isFilled;
        _needsRebuildPaints = true;
        return this;
    }

    /// <summary>
    /// Sets the width (thickness) of the rectangle's stroke (outline) in pixels.
    /// </summary>
    /// <param name="width">The stroke width in pixels. A value of 0 results in no visible stroke.</param>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The stroke width determines how thick the rectangle's outline appears. Combined with
    /// <see cref="SetStrokeAlign"/>, you can control whether the stroke is drawn inside, outside,
    /// or centered on the rectangle boundary.
    /// </para>
    /// <para>
    /// For pixel-perfect rendering at 1-pixel width, consider disabling antialiasing or using
    /// integer coordinates for the rectangle bounds.
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectRectangle SetStrokeWidth(float width)
    {
        _strokePaint.StrokeWidth = width;
        _needsRebuildPaints = true;
        return this;
    }

    /// <summary>
    /// Sets the radius for rounded corners in pixels.
    /// </summary>
    /// <param name="radius">
    /// The corner radius in pixels. A value of 0 produces sharp (90-degree) corners.
    /// Larger values create more rounded corners.
    /// </param>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Rounded corners are applied uniformly to all four corners of the rectangle. The radius specifies
    /// how far from the corner the curve begins. If the radius is larger than half the rectangle's width
    /// or height, the corners may overlap, producing unexpected results.
    /// </para>
    /// <para>
    /// Rounded corners work with both filled and outlined rectangles, as well as with stroke alignment
    /// modes. The corner radius is adjusted internally based on stroke alignment to maintain visual
    /// consistency.
    /// </para>
    /// <para>
    /// No dirty-rectangle marking is performed by this method alone; the effect becomes visible on the
    /// next render pass if the rectangle is already dirty from other property changes.
    /// </para>
    /// </remarks>
    public DirectRectangle SetCornerRadius(float radius)
    {
        _cornerRadius = radius;
        return this;
    }

    /// <summary>
    /// Sets a simple repeating dash pattern using dash and gap lengths, in pixels.
    /// </summary>
    /// <param name="dashLength">Length of each visible dash, in pixels.</param>
    /// <param name="gapLength">Length of the transparent gap between dashes, in pixels.</param>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// This is a shorthand for creating a repeating [dash, gap] pattern (e.g., <c>(8, 4)</c> for 8 px dash, 4 px gap).
    /// The pattern is applied to the stroke (outline) only and has no effect on filled rectangles without a stroke.
    /// </para>
    /// <para>
    /// Dash patterns are useful for creating dashed borders, focus indicators, or selection outlines.
    /// The pattern repeats continuously around the rectangle's perimeter.
    /// </para>
    /// <para>
    /// To remove the dash pattern and revert to a solid stroke, call <see cref="ClearDashPattern"/>.
    /// </para>
    /// <para>
    /// No dirty-rectangle marking is performed by this method alone; the effect becomes visible on the
    /// next render pass if the rectangle is already dirty from other property changes.
    /// </para>
    /// </remarks>
    public DirectRectangle SetDashPattern(float dashLength, float gapLength)
    {
        _dashPattern = new[] { dashLength, gapLength };
        // path effect is applied on draw; no rebuild needed
        return this;
    }

    /// <summary>
    /// Removes any existing dash pattern, reverting to a solid stroke.
    /// </summary>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// After calling this method, the rectangle's stroke will be drawn as a solid, continuous line
    /// instead of a dashed pattern.
    /// </para>
    /// <para>
    /// No dirty-rectangle marking is performed by this method alone; the effect becomes visible on the
    /// next render pass if the rectangle is already dirty from other property changes.
    /// </para>
    /// </remarks>
    public DirectRectangle ClearDashPattern()
    {
        _dashPattern = null;
        return this;
    }

    /// <summary>
    /// Sets the blend mode used when rendering this rectangle onto the canvas.
    /// </summary>
    /// <param name="mode">The blend mode to apply. See <see cref="SKBlendMode"/> for available options.</param>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Blend modes determine how the rectangle's pixels combine with the existing
    /// pixels on the render surface. For example:
    /// </para>
    /// <list type="bullet">
    ///   <item><term><see cref="SKBlendMode.SrcOver"/></term> – Default; draws over existing content with transparency.</item>
    ///   <item><term><see cref="SKBlendMode.Screen"/></term> – Brightens overlapping areas; useful for glow effects.</item>
    ///   <item><term><see cref="SKBlendMode.Multiply"/></term> – Darkens overlapping colors; good for shading overlays.</item>
    ///   <item><term><see cref="SKBlendMode.Plus"/></term> – Additive blending; great for light or energy effects.</item>
    /// </list>
    /// <para>
    /// This mode applies to both the fill and stroke paints. Changing it affects how
    /// the rectangle visually interacts with whatever was previously drawn.
    /// </para>
    /// <para>
    /// No dirty-rectangle marking is performed by this method alone; the effect becomes visible on the
    /// next render pass if the rectangle is already dirty from other property changes.
    /// </para>
    /// </remarks>
    public DirectRectangle SetBlendMode(SKBlendMode mode)
    {
        // apply to both paints
        _fillPaint.BlendMode = mode;
        _strokePaint.BlendMode = mode;
        _imagePaint.BlendMode = mode;
        // no rebuild needed
        return this;
    }

    /// <summary>
    /// Sets how the rectangle's stroke is positioned relative to its bounds.
    /// </summary>
    /// <param name="align">The stroke alignment mode. See <see cref="StrokeAlign"/> for available options.</param>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Determines whether the stroke (outline) is drawn inside, outside, or centered
    /// on the rectangle's boundary:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <term><see cref="StrokeAlign.Inside"/></term>
    ///     <description>Draws the stroke entirely inside the rectangle's bounds.</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="StrokeAlign.Outside"/></term>
    ///     <description>Draws the stroke entirely outside the rectangle's bounds, increasing its visual size.</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="StrokeAlign.Center"/></term>
    ///     <description>Centers the stroke on the boundary line (default Skia behavior).</description>
    ///   </item>
    /// </list>
    /// <para>
    /// This only affects visible strokes (outlined or bordered rectangles). Filled rectangles
    /// are not impacted by stroke alignment.
    /// </para>
    /// <para>
    /// No dirty-rectangle marking is performed by this method alone; the effect becomes visible on the
    /// next render pass if the rectangle is already dirty from other property changes.
    /// </para>
    /// </remarks>
    public DirectRectangle SetStrokeAlign(StrokeAlign align)
    {
        _strokeAlign = align;
        return this;
    }

    /// <summary>
    /// Initiates an animated color transition (pulse) for the rectangle's fill between two colors.
    /// </summary>
    /// <param name="from">The starting color of the pulse animation.</param>
    /// <param name="to">The ending color of the pulse animation.</param>
    /// <param name="periodSec">The duration of one complete pulse cycle in seconds. Minimum value is 0.0001 seconds.</param>
    /// <param name="enabled">
    /// <see langword="true"/> to enable the pulse animation (default); <see langword="false"/> to disable it.
    /// </param>
    /// <param name="triangle">
    /// <see langword="true"/> to use a triangle wave (linear ramp up then down); <see langword="false"/> to use
    /// a sine wave (smooth ease in/out, default).
    /// </param>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The fill pulse creates a smooth color transition that repeats continuously. The color interpolates
    /// from <paramref name="from"/> to <paramref name="to"/> and back, creating an attention-grabbing
    /// effect useful for alerts, health indicators, or animated UI elements.
    /// </para>
    /// <para>
    /// The animation is updated each frame via <see cref="Update"/> and uses high-resolution timing
    /// for smooth, consistent animation regardless of frame rate.
    /// </para>
    /// <para>
    /// To stop all pulsing (fill and border), call <see cref="StopPulses"/>. Disabling via
    /// <paramref name="enabled"/> = <see langword="false"/> stops the animation but preserves the settings
    /// for later re-enabling.
    /// </para>
    /// </remarks>
    public DirectRectangle PulseFill(Color from, Color to, float periodSec, bool enabled = true, bool triangle = false)
    {
        _pulseFillEnabled = enabled;
        _pulseFillFrom = from.ToSKColor();
        _pulseFillTo = to.ToSKColor();
        _pulseFillPeriodSec = MathF.Max(0.0001f, periodSec);
        _pulseFillWave = triangle ? PulseWave.Triangle : PulseWave.Sine;
        return this;
    }

    /// <summary>
    /// Initiates an animated color transition (pulse) for the rectangle's border between two colors.
    /// </summary>
    /// <param name="from">The starting color of the pulse animation.</param>
    /// <param name="to">The ending color of the pulse animation.</param>
    /// <param name="periodSec">The duration of one complete pulse cycle in seconds. Minimum value is 0.0001 seconds.</param>
    /// <param name="enabled">
    /// <see langword="true"/> to enable the pulse animation (default); <see langword="false"/> to disable it.
    /// </param>
    /// <param name="triangle">
    /// <see langword="true"/> to use a triangle wave (linear ramp up then down); <see langword="false"/> to use
    /// a sine wave (smooth ease in/out, default).
    /// </param>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The border pulse creates a smooth color transition that repeats continuously. The color interpolates
    /// from <paramref name="from"/> to <paramref name="to"/> and back, creating an attention-grabbing
    /// effect useful for selection indicators, focus outlines, or warning borders.
    /// </para>
    /// <para>
    /// The animation is updated each frame via <see cref="Update"/> and uses high-resolution timing
    /// for smooth, consistent animation regardless of frame rate.
    /// </para>
    /// <para>
    /// To stop all pulsing (fill and border), call <see cref="StopPulses"/>. Disabling via
    /// <paramref name="enabled"/> = <see langword="false"/> stops the animation but preserves the settings
    /// for later re-enabling.
    /// </para>
    /// </remarks>
    public DirectRectangle PulseBorder(Color from, Color to, float periodSec, bool enabled = true, bool triangle = false)
    {
        _pulseBorderEnabled = enabled;
        _pulseBorderFrom = from.ToSKColor();
        _pulseBorderTo = to.ToSKColor();
        _pulseBorderPeriodSec = MathF.Max(0.0001f, periodSec);
        _pulseBorderWave = triangle ? PulseWave.Triangle : PulseWave.Sine;
        return this;
    }

    /// <summary>
    /// Stops all active color pulsing animations (fill and border), reverting to static colors.
    /// </summary>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// After calling this method, the rectangle's fill and border colors are restored to the values
    /// set by <see cref="SetColor"/> and <see cref="SetBorderColor"/>. The pulse animation state
    /// (period, wave type, colors) is preserved but disabled, so you can re-enable it later by
    /// calling <see cref="PulseFill"/> or <see cref="PulseBorder"/> again.
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectRectangle StopPulses()
    {
        _pulseFillEnabled = _pulseBorderEnabled = false;
        RebuildPaints();   // restore fill/border to SetColor/SetBorderColor values
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Sets the rectangle's fill to use a tiled bitmap pattern instead of a solid color.
    /// </summary>
    /// <param name="bitmap">The source bitmap to tile across the rectangle's interior.</param>
    /// <param name="tileX">The horizontal tiling mode (Repeat, Mirror, or Clamp). Default is Repeat.</param>
    /// <param name="tileY">The vertical tiling mode (Repeat, Mirror, or Clamp). Default is Repeat.</param>
    /// <param name="scale">Optional uniform scale factor applied to the bitmap. 1.0 = native size (default).</param>
    /// <param name="offsetPx">Optional offset in pixels to shift the pattern origin. Null = no offset (default).</param>
    /// <param name="filterQuality">The filter quality to use when scaling the pattern. Default is None (nearest-neighbor).</param>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Pattern fills replace the solid color fill with a repeating texture. The bitmap is tiled according
    /// to the specified tile modes and scaled/offset as configured. This is useful for textured panels,
    /// patterned backgrounds, or decorative UI elements.
    /// </para>
    /// <para>
    /// The base color set by <see cref="SetColor"/> tints the pattern multiplicatively. Set it to white
    /// to preserve the pattern's original colors, or use another color to apply a tint.
    /// </para>
    /// <para>
    /// The pattern uses the rectangle's current coordinate space (world or screen). The <paramref name="scale"/>
    /// parameter affects the pattern's size, while <paramref name="offsetPx"/> shifts the pattern's origin,
    /// allowing for animated or scrolling textures.
    /// </para>
    /// <para>
    /// The bitmap is not owned by this DirectRectangle; the caller must manage its lifetime and ensure
    /// it remains valid while in use.
    /// </para>
    /// <para>
    /// This method automatically enables filled mode (<see cref="SetFilled"/>(true)) to make the pattern visible.
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// <para>
    /// To remove the pattern and return to solid color fill, call <see cref="ClearFillPattern"/>.
    /// </para>
    /// </remarks>
    public DirectRectangle SetFillPattern(SKBitmap bitmap,
                                          SKShaderTileMode tileX = SKShaderTileMode.Repeat,
                                          SKShaderTileMode tileY = SKShaderTileMode.Repeat,
                                          float scale = 1f,
                                          SKPoint? offsetPx = null,
                                          SKFilterQuality filterQuality = SKFilterQuality.None)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        _fillImage = null;
        _fillBitmap = null;

        _fillPaint.Shader = null;
        _fillShader?.Dispose();

        scale = float.IsFinite(scale) && scale > 0f
            ? scale
            : throw new ArgumentOutOfRangeException(nameof(scale), "Pattern scale must be finite and greater than zero.");

        var m = SKMatrix.CreateScale(scale, scale);
        if (offsetPx is { } o)
            m = m.PostConcat(SKMatrix.CreateTranslation(o.X, o.Y));

        _fillShader = SKShader.CreateBitmap(bitmap, tileX, tileY, m);
        _fillPaint.Shader = _fillShader;
        _fillPaint.FilterQuality = filterQuality; // or Low/Medium/High

        // Ensure we're in filled mode for visibility
        _isFilled = true;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Fills the rectangle with a bitmap using the requested scaling or repeat behavior.
    /// </summary>
    /// <param name="bitmap">The bitmap to draw. The caller retains ownership.</param>
    /// <param name="mode">How the bitmap is fitted to or repeated within the rectangle.</param>
    /// <param name="scale">
    /// A uniform scale applied in <see cref="ImageFillMode.Repeat"/> mode. Ignored by other modes.
    /// </param>
    /// <param name="offsetPx">An optional repeat-pattern offset relative to the rectangle's upper-left corner.</param>
    /// <param name="filterQuality">The sampling quality used when the bitmap is scaled.</param>
    /// <returns>This rectangle for fluent configuration.</returns>
    /// <remarks>
    /// The bitmap is clipped to the rectangle, including rounded corners. This method
    /// automatically enables filled mode. Call <see cref="ClearFillImage"/> to return
    /// to the configured solid-color fill.
    /// </remarks>
    public DirectRectangle SetFillImage(
        SKBitmap bitmap,
        ImageFillMode mode = ImageFillMode.Stretch,
        float scale = 1f,
        SKPoint? offsetPx = null,
        SKFilterQuality filterQuality = SKFilterQuality.Medium)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        SetFillImageCore(
            image: null,
            bitmap: bitmap,
            mode: mode,
            scale: scale,
            offsetPx: offsetPx,
            filterQuality: filterQuality);

        return this;
    }

    /// <summary>
    /// Fills the rectangle with an image using the requested scaling or repeat behavior.
    /// </summary>
    /// <param name="image">The image to draw. The caller retains ownership.</param>
    /// <param name="mode">How the image is fitted to or repeated within the rectangle.</param>
    /// <param name="scale">
    /// A uniform scale applied in <see cref="ImageFillMode.Repeat"/> mode. Ignored by other modes.
    /// </param>
    /// <param name="offsetPx">An optional repeat-pattern offset relative to the rectangle's upper-left corner.</param>
    /// <param name="filterQuality">The sampling quality used when the image is scaled.</param>
    /// <returns>This rectangle for fluent configuration.</returns>
    /// <remarks>
    /// The image is clipped to the rectangle, including rounded corners. This method
    /// automatically enables filled mode. Call <see cref="ClearFillImage"/> to return
    /// to the configured solid-color fill.
    /// </remarks>
    public DirectRectangle SetFillImage(
        SKImage image,
        ImageFillMode mode = ImageFillMode.Stretch,
        float scale = 1f,
        SKPoint? offsetPx = null,
        SKFilterQuality filterQuality = SKFilterQuality.Medium)
    {
        ArgumentNullException.ThrowIfNull(image);

        SetFillImageCore(
            image: image,
            bitmap: null,
            mode: mode,
            scale: scale,
            offsetPx: offsetPx,
            filterQuality: filterQuality);

        return this;
    }

    /// <summary>
    /// Removes the current bitmap or image fill and returns to solid-color filling.
    /// </summary>
    /// <returns>This rectangle for fluent configuration.</returns>
    public DirectRectangle ClearFillImage()
    {
        _fillImage = null;
        _fillBitmap = null;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Removes the current pattern fill and reverts to solid color rendering.
    /// </summary>
    /// <returns>This <see cref="DirectRectangle"/> instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// After calling this method, the rectangle's fill will use the solid color set by <see cref="SetColor"/>
    /// instead of the tiled bitmap pattern. The pattern shader is disposed to free resources.
    /// </para>
    /// <para>
    /// The affected screen regions are automatically marked as dirty to trigger rerendering on the next frame.
    /// </para>
    /// </remarks>
    public DirectRectangle ClearFillPattern()
    {
        _fillPaint.Shader = null;
        _fillShader?.Dispose();
        _fillShader = null;
        ForceRefresh();
        return this;
    }

    /// <summary>
    /// Performs per-frame update logic, including pulse animation advancement and physics integration.
    /// </summary>
    /// <param name="tick">The current engine tick value from <see cref="HighResTimer"/>.</param>
    /// <remarks>
    /// <para>
    /// This method overrides <see cref="DirectDrawingMovableBase.Update"/> to add color pulsing animation
    /// support. It performs the following operations:
    /// <list type="number">
    /// <item><description>Calculates elapsed time since the last update using high-resolution timing.</description></item>
    /// <item><description>Advances the pulse time accumulator for sine/triangle wave generation.</description></item>
    /// <item><description>Updates the fill color if fill pulsing is enabled, interpolating between the pulse colors.</description></item>
    /// <item><description>Updates the border color if border pulsing is enabled, interpolating between the pulse colors.</description></item>
    /// <item><description>Marks affected regions as dirty if color changes occurred.</description></item>
    /// <item><description>Calls <c>base.Update(tick)</c> to perform physics integration and fade/reveal animations.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The pulse animation uses a continuous time accumulator that wraps around the specified period,
    /// ensuring smooth, consistent animation. Color interpolation is performed in RGB space with linear
    /// blending (including alpha channel).
    /// </para>
    /// <para>
    /// Override this method in derived classes to add custom per-frame logic. Always call
    /// <c>base.Update(tick)</c> to preserve pulsing, movement, and fade/reveal functionality.
    /// </para>
    /// </remarks>
    public override void Update(long tick)
    {
        if (tick <= _lastTick)
            return;

        // Compute dt from ticks (seconds)
        float dt = 0f;

        // no previous tick assume first frame, so skip dt-based updates
        if (_pulseLastTick > 0)
        {
            dt = HighResTimer.GetDuration(_pulseLastTick, tick);
            _timeSec += dt;
        }

        _pulseLastTick = tick;

        bool changed = false;

        if (_pulseFillEnabled)
        {
            float t = PulseT(_timeSec, _pulseFillPeriodSec, _pulseFillWave);
            var c = LerpColor(_pulseFillFrom, _pulseFillTo, t);
            if (_fillPaint.Color != c) { _fillPaint.Color = c; changed = true; }
        }

        if (_pulseBorderEnabled)
        {
            float t = PulseT(_timeSec, _pulseBorderPeriodSec, _pulseBorderWave);
            var c = LerpColor(_pulseBorderFrom, _pulseBorderTo, t);

            if (_strokePaint.Color != c)
            {
                _strokePaint.Color = c;   // paint-only override
                ForceRefresh();
            }
        }

        if (changed)
        {
            _needsRebuildPaints = false;    // we already set paints directly
            ForceRefresh();                 // request redraw
        }

        base.Update(tick);
    }

    /// <summary>
    /// Renders the rectangle to the backbuffer with all configured properties and effects applied.
    /// </summary>
    /// <param name="backbuffer">The backbuffer providing the canvas and rendering context.</param>
    /// <param name="destRectScreen">The destination rectangle in screen pixel coordinates where the rectangle should be rendered.</param>
    /// <remarks>
    /// <para>
    /// This method is called automatically by the rendering pipeline. It performs the following operations:
    /// <list type="number">
    /// <item><description>Rebuilds paint objects if properties have changed since the last draw.</description></item>
    /// <item><description>Calculates stroke rectangle based on alignment mode (inside, outside, center).</description></item>
    /// <item><description>Adjusts corner radius based on stroke alignment to maintain visual consistency.</description></item>
    /// <item><description>Applies dash pattern to the stroke paint if configured.</description></item>
    /// <item><description>Draws the filled rectangle if <see cref="SetFilled"/> is true (using color or pattern).</description></item>
    /// <item><description>Draws the stroke (border) with opaque rendering to avoid blend artifacts.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The stroke is rendered using <see cref="SKBlendMode.Src"/> (source replace) mode with full opacity
    /// to ensure crisp borders without color blending artifacts, regardless of the configured blend mode.
    /// This is restored after the stroke is drawn.
    /// </para>
    /// <para>
    /// Do not call this method directly from game code. To trigger a redraw, call <see cref="DirectDrawingBase.ForceRefresh"/>
    /// or modify properties that affect appearance.
    /// </para>
    /// </remarks>
    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        var canvas = backbuffer.Canvas;

        if (_needsRebuildPaints)
            RebuildPaints();

        var fillRect = destRectScreen.ToSKRect();
        var strokeRect = fillRect;

        bool willDrawStroke = !_isFilled || _borderColor.HasValue || _strokePaint.StrokeWidth > 0.01f;
        float half = _strokePaint.StrokeWidth * 0.5f;

        // 1) APPLY STROKE ALIGNMENT (use HALF the width; path is centered)
        if (willDrawStroke && _strokeAlign != StrokeAlign.Center)
        {
            if (_strokeAlign == StrokeAlign.Inside)
                strokeRect.Inflate(-half, -half);
            else if (_strokeAlign == StrokeAlign.Outside)
                strokeRect.Inflate(half, half);
        }

        // 1.5) corner radius for the stroke path, adjusted to keep the inner/outer arcs aligned to the fill
        float strokeCornerRadius;
        switch (_strokeAlign)
        {
            case StrokeAlign.Outside:
                strokeCornerRadius = MathF.Max(0f, _cornerRadius - half);
                break;
            case StrokeAlign.Inside:
                strokeCornerRadius = _cornerRadius + half;
                break;
            case StrokeAlign.Center:
            default:
                strokeCornerRadius = _cornerRadius;
                break;
        }

        // 2) Dash for stroke only
        _strokePaint.PathEffect = _dashPattern is { Length: > 0 }
            ? SKPathEffect.CreateDash(_dashPattern, 0)
            : null;

        // 3) Draw fill (unmodified rect)
        if (_isFilled)
        {
            if (_fillImage is not null || _fillBitmap is not null)
            {
                DrawImageFill(canvas, fillRect);
            }
            else if (_cornerRadius > 0)
            {
                canvas.DrawRoundRect(fillRect, strokeCornerRadius, strokeCornerRadius, _fillPaint);
            }
            else
            {
                canvas.DrawRect(fillRect, _fillPaint);
            }
        }

        // 4) Draw stroke on its aligned rect (pure/opaque so it stays white)
        if (willDrawStroke)
        {
            var prevBlend = _strokePaint.BlendMode;
            var prevColor = _strokePaint.Color;
            var prevAA = _strokePaint.IsAntialias;

            _strokePaint.IsAntialias = false;
            _strokePaint.BlendMode = SKBlendMode.Src;

            // If pulsing, keep color from Update(),
            // else use base border color (or previous).
            var strokeColor = _pulseBorderEnabled ? _strokePaint.Color
                                                  : (_borderColor ?? prevColor);

            _strokePaint.Color = strokeColor.WithAlpha(255);

            if (_cornerRadius > 0)
                canvas.DrawRoundRect(strokeRect, _cornerRadius, _cornerRadius, _strokePaint);
            else
                canvas.DrawRect(strokeRect, _strokePaint);

            _strokePaint.IsAntialias = prevAA;
            _strokePaint.BlendMode = prevBlend;
            _strokePaint.Color = prevColor;
            _strokePaint.StrokeJoin = SKStrokeJoin.Round;
            _strokePaint.StrokeCap = SKStrokeCap.Round;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_resourcesDisposed)
        {
            _resourcesDisposed = true;
            _fillPaint.Shader = null;
            _fillShader?.Dispose();
            _fillShader = null;
            _fillPaint.Dispose();
            _strokePaint.Dispose();
            _imagePaint.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SetFillImageCore(
        SKImage? image,
        SKBitmap? bitmap,
        ImageFillMode mode,
        float scale,
        SKPoint? offsetPx,
        SKFilterQuality filterQuality)
    {
        if (!float.IsFinite(scale) || scale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale), "Image scale must be finite and greater than zero.");

        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));

        _fillPaint.Shader = null;
        _fillShader?.Dispose();
        _fillShader = null;

        _fillImage = image;
        _fillBitmap = bitmap;
        _imageFillMode = mode;
        _imageScale = scale;
        _imageOffsetPx = offsetPx ?? SKPoint.Empty;
        _imagePaint.FilterQuality = filterQuality;
        _isFilled = true;

        ForceRefresh();
    }

    private void DrawImageFill(
        SKCanvas canvas,
        SKRect fillRect)
    {
        SKImage? image = _fillImage;
        bool disposeImage = false;

        if (image is null && _fillBitmap is not null)
        {
            image = SKImage.FromBitmap(_fillBitmap);
            disposeImage = true;
        }

        if (image is null || image.Width <= 0 || image.Height <= 0)
            return;

        canvas.Save();

        try
        {
            using var clipPath = new SKPath();

            if (_cornerRadius > 0f)
                clipPath.AddRoundRect(fillRect, _cornerRadius, _cornerRadius);
            else
                clipPath.AddRect(fillRect);

            canvas.ClipPath(
                clipPath,
                SKClipOperation.Intersect,
                antialias: _cornerRadius > 0f);

            var source = new SKRect(
                0f,
                0f,
                image.Width,
                image.Height);

            if (_imageFillMode == ImageFillMode.Repeat)
            {
                DrawRepeatedImage(
                    canvas,
                    image,
                    source,
                    fillRect);
            }
            else
            {
                SKRect destination = ComputeImageDestination(
                    fillRect,
                    source,
                    _imageFillMode);

                canvas.DrawImage(
                    image,
                    source,
                    destination,
                    _imagePaint);
            }
        }
        finally
        {
            canvas.Restore();

            if (disposeImage)
                image.Dispose();
        }
    }

    private void DrawRepeatedImage(
        SKCanvas canvas,
        SKImage image,
        SKRect source,
        SKRect fillRect)
    {
        float tileWidth = image.Width * _imageScale;
        float tileHeight = image.Height * _imageScale;

        float offsetX = PositiveModulo(_imageOffsetPx.X, tileWidth);
        float offsetY = PositiveModulo(_imageOffsetPx.Y, tileHeight);

        float startX = fillRect.Left + offsetX;
        float startY = fillRect.Top + offsetY;

        if (startX > fillRect.Left)
            startX -= tileWidth;

        if (startY > fillRect.Top)
            startY -= tileHeight;

        for (float y = startY; y < fillRect.Bottom; y += tileHeight)
        {
            for (float x = startX; x < fillRect.Right; x += tileWidth)
            {
                canvas.DrawImage(
                    image,
                    source,
                    new SKRect(
                        x,
                        y,
                        x + tileWidth,
                        y + tileHeight),
                    _imagePaint);
            }
        }
    }

    private static SKRect ComputeImageDestination(
        SKRect bounds,
        SKRect source,
        ImageFillMode mode)
    {
        if (mode == ImageFillMode.Stretch)
            return bounds;

        if (mode == ImageFillMode.Center)
        {
            float x = bounds.MidX - source.Width * 0.5f;
            float y = bounds.MidY - source.Height * 0.5f;

            return new SKRect(
                x,
                y,
                x + source.Width,
                y + source.Height);
        }

        float scaleX = bounds.Width / source.Width;
        float scaleY = bounds.Height / source.Height;

        float scale = mode switch
        {
            ImageFillMode.Fit => MathF.Min(scaleX, scaleY),
            ImageFillMode.Fill => MathF.Max(scaleX, scaleY),
            ImageFillMode.PixelPerfect => MathF.Max(
                1f,
                MathF.Floor(MathF.Min(scaleX, scaleY))),
            _ => 1f
        };

        float width = source.Width * scale;
        float height = source.Height * scale;
        float left = bounds.MidX - width * 0.5f;
        float top = bounds.MidY - height * 0.5f;

        return new SKRect(
            left,
            top,
            left + width,
            top + height);
    }

    private static float PositiveModulo(float value, float modulus)
    {
        float remainder = value % modulus;
        return remainder < 0f
            ? remainder + modulus
            : remainder;
    }

    /// <summary>
    /// Rebuilds cached paints when properties affecting color/alpha/stroke need syncing.
    /// </summary>
    private void RebuildPaints()
    {
        // Ensure styles/AA set
        _fillPaint.IsAntialias = true;
        _fillPaint.Style = SKPaintStyle.Fill;

        _strokePaint.IsAntialias = true;
        _strokePaint.Style = SKPaintStyle.Stroke;

        // Only rebuild stroke color if not pulsing border
        if (!_pulseBorderEnabled)
        {
            // If border color set, use it for stroke; else match fill/base color
            if (_borderColor.HasValue)
            {
                var sc = _strokePaint.Color; // preserve alpha if you want; otherwise:
                _strokePaint.Color = _borderColor.Value;
            }
            else
            {
                _strokePaint.Color = _fillPaint.Color; // same as fill if no explicit border
            }

            _needsRebuildPaints = false;
        }
    }

    /// <summary>
    /// Defines how the stroke (outline) is positioned relative to the rectangle's boundary.
    /// </summary>
    /// <remarks>
    /// Stroke alignment determines whether the stroke width expands inward, outward, or is
    /// centered on the rectangle's edge.
    /// </remarks>
    public enum StrokeAlign
    {
        /// <summary>
        /// Draws the stroke entirely inside the rectangle's bounds. The outer edge of the
        /// stroke aligns with the rectangle boundary.
        /// </summary>
        Inside,

        /// <summary>
        /// Draws the stroke entirely outside the rectangle's bounds. The inner edge of the
        /// stroke aligns with the rectangle boundary, increasing the visual size.
        /// </summary>
        Outside,

        /// <summary>
        /// Centers the stroke on the rectangle's boundary. Half the stroke width is drawn
        /// inside and half outside (default Skia behavior).
        /// </summary>
        Center
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float PulseT(float timeSec, float periodSec, PulseWave wave)
    {
        float phase = (timeSec / periodSec) % 1f;
        if (wave == PulseWave.Sine)
        {
            // (sin(2πx)+1)/2 in [0..1]
            return 0.5f * (1f + MathF.Sin(phase * MathF.PI * 2f));
        }
        else // Triangle: ramp up 0..1 then down 1..0
        {
            return phase < 0.5f ? (phase * 2f) : (1f - ((phase - 0.5f) * 2f));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKColor LerpColor(SKColor a, SKColor b, float t01)
    {
        t01 = Math.Clamp(t01, 0f, 1f);

        byte r = (byte)(a.Red + (b.Red - a.Red) * t01);
        byte g = (byte)(a.Green + (b.Green - a.Green) * t01);
        byte bch = (byte)(a.Blue + (b.Blue - a.Blue) * t01);
        byte aA = (byte)(a.Alpha + (b.Alpha - a.Alpha) * t01);
        
        return new SKColor(r, g, bch, aA);
    }
}
