using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Skia;
using SkiaSharp;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Represents a drawable rectangle with customizable properties such as color, stroke width, corner radius, etc.
/// </summary>
/// <remarks>The <see cref="DirectRectangle"/> class provides methods to configure various visual aspects of a
/// rectangle, including its fill state, stroke width, corner radius, dash pattern, blend mode, and stroke alignment. It
/// is designed to be used with a RenderSurfaceHost and can be rendered onto a canvas.</remarks>
///
/// <example>
///
/// *** Basic Outlined Rectangle:
/// var box = new DirectRectangle(surface, new Rectangle(50, 50, 120, 80), Color.DarkGreen)
///    .SetFilled(false)
///    .SetStrokeWidth(2);
///
/// *** Semi-Transparent Filled Rectangle:
/// var highlight = new DirectRectangle(surface, new Rectangle(200, 50, 100, 100), Color.Yellow)
///    .SetFilled(true)
///    .SetAlpha(128); // 50% transparent
///
/// *** Rounded Rectangle with Dashed Border:
/// var panel = new DirectRectangle(surface, new Rectangle(50, 160, 180, 80), Color.CornflowerBlue)
///    .SetFilled(false)
///    .SetCornerRadius(12f)
///    .SetDashPattern(8, 4); // dash 8px, gap 4px
///
/// *** Highlighted Outline with Outside Stroke and Blend Mode:
/// var glowBox = new DirectRectangle(surface, new Rectangle(250, 160, 120, 80), Color.Red)
///    .SetFilled(false)
///    .SetStrokeWidth(6)
///    .SetStrokeAlign(DirectRectangle.StrokeAlign.Outside)
///    .SetBlendMode(SKBlendMode.Screen); // additive/lighten effect
///
/// *** Animated Pulse (per-frame logic example):
/// float pulse = 1.0f + (float)Math.Sin(tick / 10.0) * 0.5f;
///
/// glowBox
///    .SetStrokeWidth(3f + pulse)
///    .SetAlpha((int)(128 + 127 * Math.Sin(tick / 10.0)));
///
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

    public DirectRectangle SetDashPattern(params float[] dashes)
    {
        _dashPattern = dashes;
        // path effect is applied on draw; no rebuild needed
        return this;
    }

    public DirectRectangle SetBlendMode(SKBlendMode mode)
    {
        // apply to both paints
        _fillPaint.BlendMode = mode;
        _strokePaint.BlendMode = mode;
        // no rebuild needed
        return this;
    }

    public DirectRectangle SetStrokeAlign(StrokeAlign align)
    {
        _strokeAlign = align;
        return this;
    }

    protected internal override void Draw()
    {
        if (_needsRebuildPaints) RebuildPaints();

        var canvas = RenderSurfaceHost.Backbuffer.Canvas;
        var rect = Bounds.ToSKRect();

        // Adjust rect based on stroke align (only matters for visible stroke)
        bool willDrawStroke = !_isFilled || _borderColor.HasValue || _strokePaint.StrokeWidth > 0.01f;
        if (willDrawStroke && _strokeAlign != StrokeAlign.Center)
        {
            float offset = _strokePaint.StrokeWidth / 2f;
            if (_strokeAlign == StrokeAlign.Inside) rect.Inflate(-offset, -offset);
            else if (_strokeAlign == StrokeAlign.Outside) rect.Inflate(offset, offset);
        }

        // Path effect applies only to stroke
        _strokePaint.PathEffect = _dashPattern is { Length: > 0 }
            ? SKPathEffect.CreateDash(_dashPattern, 0)
            : null;

        if (_cornerRadius > 0)
        {
            var rr = new SKRoundRect(rect, _cornerRadius);
            if (_isFilled) canvas.DrawRoundRect(rr, _fillPaint);
            if (willDrawStroke) canvas.DrawRoundRect(rr, _strokePaint);
        }
        else
        {
            if (_isFilled) canvas.DrawRect(rect, _fillPaint);
            if (willDrawStroke) canvas.DrawRect(rect, _strokePaint);
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
}
