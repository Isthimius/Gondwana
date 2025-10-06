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
    private readonly SKPaint _paint;
    private bool _isFilled;
    private float _cornerRadius;
    private float[]? _dashPattern;
    private StrokeAlign _strokeAlign = StrokeAlign.Center;
    private SKColor? _borderColor;

    public DirectRectangle(
        RenderSurfaceHostBase renderSurfaceHost,
        Rectangle bounds,
        Color color)
        : base(renderSurfaceHost, bounds)
    {
        _paint = new SKPaint
        {
            Color = color.ToSKColor(),
            IsStroke = true,
            StrokeWidth = 1,
            IsAntialias = true,
            BlendMode = SKBlendMode.SrcOver
        };
    }

    public DirectRectangle SetColor(Color color)
    {
        _paint.Color = color.ToSKColor();
        return this;
    }

    public DirectRectangle SetAlpha(int alpha)
    {
        var c = _paint.Color;
        _paint.Color = new SKColor(c.Red, c.Green, c.Blue, (byte)alpha);
        return this;
    }

    public DirectRectangle SetFilled(bool isFilled)
    {
        _isFilled = isFilled;
        _paint.IsStroke = !isFilled;
        return this;
    }

    public DirectRectangle SetStrokeWidth(float width)
    {
        _paint.StrokeWidth = width;
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
        return this;
    }

    public DirectRectangle SetBlendMode(SKBlendMode mode)
    {
        _paint.BlendMode = mode;
        return this;
    }

    public DirectRectangle SetStrokeAlign(StrokeAlign align)
    {
        _strokeAlign = align;
        return this;
    }

    /// <summary>
    /// Sets a distinct border color, allowing fill and outline colors to differ.
    /// </summary>
    public DirectRectangle SetBorderColor(Color color)
    {
        _borderColor = color.ToSKColor();
        return this;
    }

    protected internal override void Draw()
    {
        var canvas = RenderSurfaceHost.Backbuffer.Canvas;
        var rect = Bounds.ToSKRect();

        // Handle stroke alignment
        if (_strokeAlign != StrokeAlign.Center && !_isFilled)
        {
            float offset = _paint.StrokeWidth / 2f;
            if (_strokeAlign == StrokeAlign.Inside)
                rect.Inflate(-offset, -offset);
            else if (_strokeAlign == StrokeAlign.Outside)
                rect.Inflate(offset, offset);
        }

        _paint.PathEffect = _dashPattern is { Length: > 0 }
            ? SKPathEffect.CreateDash(_dashPattern, 0)
            : null;

        // --- Draw fill ---
        if (_isFilled)
        {
            using var fillPaint = new SKPaint
            {
                Color = _paint.Color, // base color
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                BlendMode = _paint.BlendMode
            };

            if (_cornerRadius > 0)
                canvas.DrawRoundRect(rect, _cornerRadius, _cornerRadius, fillPaint);
            else
                canvas.DrawRect(rect, fillPaint);
        }

        // --- Draw border (always stroke) ---
        if (_borderColor.HasValue || !_isFilled)
        {
            var borderPaint = _paint.Clone();
            borderPaint.IsStroke = true;
            borderPaint.Style = SKPaintStyle.Stroke;
            borderPaint.Color = _borderColor ?? _paint.Color;

            if (_cornerRadius > 0)
                canvas.DrawRoundRect(rect, _cornerRadius, _cornerRadius, borderPaint);
            else
                canvas.DrawRect(rect, borderPaint);
        }
    }

    public enum StrokeAlign
    {
        Inside,
        Outside,
        Center
    }
}