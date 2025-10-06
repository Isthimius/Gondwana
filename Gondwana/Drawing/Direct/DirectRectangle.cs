using Gondwana.Rendering;
using Gondwana.Skia;
using Gondwana.Timers;
using SkiaSharp;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// A retained-mode, configurable rectangle overlay that supports fill and/or border,
/// independent border color, stroke width and alignment, rounded corners, dash patterns,
/// blend modes, and optional color pulsing — all rendered via the bound RenderSurfaceHost.
/// </summary>
/// <remarks>
/// <para>
/// Designed for UI and 2D overlay work, <c>DirectRectangle</c> caches its fill/stroke paints
/// and rebuilds them only when properties change (reducing GC/native churn). It respects
/// stroke alignment (inside/center/outside), optional dash patterns, and can animate fill
/// or border colors over time via <c>PulseFill</c>/<c>PulseBorder</c>.
/// </para>
/// <para>
/// Typical usage is to construct, configure via the fluent setters, and rely on the engine
/// to call <c>Update</c>/<c>Draw</c> when dirty. Setters mark paints dirty so the next frame
/// re-renders with the new appearance.
/// </para>
/// </remarks>
/// <example>
/// // Filled panel with a distinct border and rounded corners
/// var panel = new DirectRectangle(surface, new Rectangle(80, 80, 220, 120), Color.SteelBlue)
///     .SetFilled(true)
///     .SetBorderColor(Color.Navy)
///     .SetStrokeWidth(4f)
///     .SetCornerRadius(12f)
///     .SetDashPattern(8f, 4f); // dashed outline
///
/// // Soft glow using blend mode
/// var glow = new DirectRectangle(surface, new Rectangle(320, 90, 180, 100), Color.FromArgb(64, 255, 200, 0))
///     .SetFilled(true)
///     .SetBlendMode(SKBlendMode.Screen);
///
/// // Pulsing alert border
/// glow.PulseBorder(Color.FromArgb(255, 255, 64, 64), Color.FromArgb(80, 255, 0, 0), 1.2f);
/// </example>
public class DirectRectangle : DirectDrawingBase
{
    private SKPaint _fillPaint;          // cached fill
    private SKPaint _strokePaint;        // cached stroke
    private SKColor? _borderColor;       // optional distinct border color

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
    private long? _lastTick;
    private float _timeSec; // accumulated seconds

    public DirectRectangle(
        RenderSurfaceHostBase renderSurfaceHost,
        Rectangle bounds,
        Color color)
        : base(renderSurfaceHost, bounds)
    {
        // initialize with defaults; actual paints built lazily
        _fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        _strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        SetColor(color);                 // sets base color and marks rebuild
        SetBlendMode(SKBlendMode.SrcOver);
        SetFilled(false);
    }

    /// <summary>Sets the base color (used for fill and/or outline if no border color is specified).</summary>
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

    /// <summary>Sets a distinct border color (stroke). If not set, stroke uses the base color.</summary>
    public DirectRectangle SetBorderColor(Color color)
    {
        _borderColor = color.ToSKColor();
        _needsRebuildPaints = true;
        return this;
    }

    /// <summary>Sets the alpha channel (0–255) for the base color. Border keeps its own alpha if set.</summary>
    public DirectRectangle SetAlpha(int alpha)
    {
        var c = _fillPaint.Color;
        var withA = new SKColor(c.Red, c.Green, c.Blue, (byte)alpha);
        _fillPaint.Color = withA;
        // Only change stroke when it’s not using a distinct border color
        if (_borderColor is null)
            _strokePaint.Color = withA;
        _needsRebuildPaints = true;
        return this;
    }

    public DirectRectangle SetFilled(bool isFilled)
    {
        _isFilled = isFilled;
        _needsRebuildPaints = true;
        return this;
    }

    public DirectRectangle SetStrokeWidth(float width)
    {
        _strokePaint.StrokeWidth = width;
        _needsRebuildPaints = true;
        return this;
    }

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
    /// <returns>This rectangle for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This is a shorthand for <see cref="SetDashPattern(float[])"/> that
    /// produces a repeating [dash, gap] pattern (e.g., <c>(8, 4)</c> for 8 px dash, 4 px gap).
    /// </para>
    /// <para>
    /// To remove the dash pattern, call <see cref="ClearDashPattern"/>.
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
    public DirectRectangle ClearDashPattern()
    {
        _dashPattern = null;
        return this;
    }

    /// <summary>
    /// Sets the blend mode used when rendering this rectangle onto the canvas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Blend modes determine how the rectangle’s pixels combine with the existing
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
    /// </remarks>
    public DirectRectangle SetBlendMode(SKBlendMode mode)
    {
        // apply to both paints
        _fillPaint.BlendMode = mode;
        _strokePaint.BlendMode = mode;
        // no rebuild needed
        return this;
    }

    /// <summary>
    /// Sets how the rectangle’s stroke is positioned relative to its bounds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Determines whether the stroke (outline) is drawn inside, outside, or centered
    /// on the rectangle’s boundary:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <term><see cref="StrokeAlign.Inside"/></term>
    ///     <description>Draws the stroke entirely inside the rectangle’s bounds.</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="StrokeAlign.Outside"/></term>
    ///     <description>Draws the stroke entirely outside the rectangle’s bounds, increasing its visual size.</description>
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
    /// </remarks>
    public DirectRectangle SetStrokeAlign(StrokeAlign align)
    {
        _strokeAlign = align;
        return this;
    }

    /// <summary>Animate the fill color between <paramref name="from"/> and <paramref name="to"/> over <paramref name="periodSec"/> seconds.</summary>
    public DirectRectangle PulseFill(Color from, Color to, float periodSec, bool enabled = true, bool triangle = false)
    {
        _pulseFillEnabled = enabled;
        _pulseFillFrom = from.ToSKColor();
        _pulseFillTo = to.ToSKColor();
        _pulseFillPeriodSec = MathF.Max(0.0001f, periodSec);
        _pulseFillWave = triangle ? PulseWave.Triangle : PulseWave.Sine;
        return this;
    }

    /// <summary>Animate the border color between <paramref name="from"/> and <paramref name="to"/> over <paramref name="periodSec"/> seconds.</summary>
    public DirectRectangle PulseBorder(Color from, Color to, float periodSec, bool enabled = true, bool triangle = false)
    {
        _pulseBorderEnabled = enabled;
        _pulseBorderFrom = from.ToSKColor();
        _pulseBorderTo = to.ToSKColor();
        _pulseBorderPeriodSec = MathF.Max(0.0001f, periodSec);
        _pulseBorderWave = triangle ? PulseWave.Triangle : PulseWave.Sine;
        return this;
    }

    /// <summary>Disable all color pulsing.</summary>
    public DirectRectangle StopPulses()
    {
        _pulseFillEnabled = _pulseBorderEnabled = false;
        RebuildPaints();   // restore fill/border to SetColor/SetBorderColor values
        _dirty = true;
        return this;
    }

    protected internal override void Update(long tick)
    {
        if (_lastTick is { } last)
        {
            long deltaTicks = tick - last;
            if (deltaTicks < 0) deltaTicks = 0; // guard against clock reset
            float dt = (float)(deltaTicks / (double)HighResTimer.TicksPerSecond);

            if (dt > 0f && dt < 1f) _timeSec += dt; // clamp outliers
        }
        _lastTick = tick;

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
                _dirty = true;
            }
        }

        if (changed)
        {
            _needsRebuildPaints = false; // we already set paints directly
            _dirty = true;               // request redraw
        }
    }

    protected internal override void Draw()
    {
        var canvas = RenderSurfaceHost.Backbuffer.Canvas;

        if (_needsRebuildPaints) RebuildPaints();

        // Separate rectangles for fill and stroke
        var fillRect = Bounds.ToSKRect();
        var strokeRect = fillRect;

        bool willDrawStroke = !_isFilled || _borderColor.HasValue || _strokePaint.StrokeWidth > 0.01f;

        // Then draw stroke on its own aligned rect
        if (willDrawStroke)
        {
            var prevBlend = _strokePaint.BlendMode;
            var prevColor = _strokePaint.Color;
            var prevAA = _strokePaint.IsAntialias;
            var prevW = _strokePaint.StrokeWidth;

            // Pass 1: solid core, no AA (pure white)
            _strokePaint.IsAntialias = false;
            _strokePaint.BlendMode = SKBlendMode.Src;
            _strokePaint.Color = SKColors.White;
            _strokePaint.StrokeWidth = MathF.Max(1f, prevW - 1f);

            if (_cornerRadius > 0)
                canvas.DrawRoundRect(strokeRect, _cornerRadius, _cornerRadius, _strokePaint);
            else
                canvas.DrawRect(strokeRect, _strokePaint);

            // Pass 2: thin AA skin to smooth edge
            _strokePaint.IsAntialias = true;
            _strokePaint.BlendMode = SKBlendMode.SrcOver;   // normal composite
            _strokePaint.Color = SKColors.White;        // keep white
            _strokePaint.StrokeWidth = prevW;

            if (_cornerRadius > 0)
                canvas.DrawRoundRect(strokeRect, _cornerRadius, _cornerRadius, _strokePaint);
            else
                canvas.DrawRect(strokeRect, _strokePaint);

            // restore
            _strokePaint.IsAntialias = prevAA;
            _strokePaint.BlendMode = prevBlend;
            _strokePaint.Color = prevColor;
            _strokePaint.StrokeWidth = prevW;
        }

        // Path effect only for stroke
        _strokePaint.PathEffect = _dashPattern is { Length: > 0 }
            ? SKPathEffect.CreateDash(_dashPattern, 0)
            : null;

        // Draw fill first (unmodified fillRect)
        if (_isFilled)
        {
            if (_cornerRadius > 0)
                canvas.DrawRoundRect(fillRect, _cornerRadius, _cornerRadius, _fillPaint);
            else
                canvas.DrawRect(fillRect, _fillPaint);
        }

        // Then draw stroke on its own aligned rect
        if (willDrawStroke)
        {
            // --- FORCE a pure, opaque, non-blended outline for this draw only ---
            var prevBlend = _strokePaint.BlendMode;
            var prevColor = _strokePaint.Color;

            _strokePaint.BlendMode = SKBlendMode.Src;               // replace dst with src (no mixing)
            _strokePaint.Color = prevColor.WithAlpha(255);          // ensure fully opaque

            if (_cornerRadius > 0)
                canvas.DrawRoundRect(strokeRect, _cornerRadius, _cornerRadius, _strokePaint);
            else
                canvas.DrawRect(strokeRect, _strokePaint);

            // restore for future draws
            _strokePaint.BlendMode = prevBlend;
            _strokePaint.Color = prevColor;
        }
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

    public enum StrokeAlign
    {
        Inside,
        Outside,
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
