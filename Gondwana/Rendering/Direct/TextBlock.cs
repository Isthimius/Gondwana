using SkiaSharp;
using System.Drawing;

namespace Gondwana.Rendering.Direct;

/// <summary>
/// Represents a block of text that can be drawn on a visible surface with various styling options.
/// </summary>
/// <remarks>The <see cref="TextBlock"/> class provides methods to configure text properties such as font, color,
/// alignment, and additional effects like shadow and outline. It supports text wrapping and vertical alignment within a
/// specified rectangular area.
/// </remarks>
/// <example>
/// var block = new TextBlock(surface, new Rectangle(0, 0, 400, 200))
///    .SetText("Gondwana welcomes you to the new frontier of text rendering!")
///    .SetFont(typeface, 24f, minSize: 14f)
///    .SetColors(Color.White, Color.Navy)
///    .SetAlignment(SKTextAlign.Center, SKParagraphAlignment.Center)
///    .EnableWrapping()
///    .SetMaxLines(4)
///    .UseShadow()
///    .UseOutline();
/// </example>
public class TextBlock : DirectDrawing
{
    public enum VerticalAlign
    {
        Top,
        Center,
        Bottom
    }

    private string _text = string.Empty;
    private SKColor _foreColor = SKColors.White;
    private SKColor _backColor = SKColors.Transparent;

    private float _fontSize = 16f;
    private float? _minFontSize = null;
    private SKTypeface? _typeface = null;

    private bool _useShadow = false;
    private bool _useOutline = false;
    private bool _wrapText = true;
    private int? _maxLines = null;

    private SKTextAlign _hAlign = SKTextAlign.Left;
    private VerticalAlign _vAlign = VerticalAlign.Top;

    public TextBlock(BackbufferBase buffer, Rectangle bounds)
        : base(buffer, bounds)
    {
    }

    public TextBlock SetText(string text) { _text = text; return this; }
    public TextBlock SetFont(SKTypeface typeface, float size, float? minSize = null) { _typeface = typeface; _fontSize = size; _minFontSize = minSize; return this; }
    public TextBlock SetColors(SKColor fg, SKColor bg) { _foreColor = fg; _backColor = bg; return this; }
    public TextBlock SetColors(Color fg, Color bg) => SetColors(fg.ToSKColor(), bg.ToSKColor());
    public TextBlock SetAlignment(SKTextAlign h, VerticalAlign v)
    {
        _hAlign = h;
        _vAlign = v;
        return this;
    }

    public TextBlock UseShadow(bool enable = true) { _useShadow = enable; return this; }
    public TextBlock UseOutline(bool enable = true) { _useOutline = enable; return this; }
    public TextBlock EnableWrapping(bool enable = true) { _wrapText = enable; return this; }
    public TextBlock SetMaxLines(int? maxLines) { _maxLines = maxLines; return this; }

    protected internal override void Render()
    {
        var canvas = Buffer.Canvas;
        var rect = Bounds.ToSKRect();

        using var bg = new SKPaint { Color = _backColor };
        canvas.DrawRect(rect, bg);

        if (_typeface == null)
            _typeface = SKTypeface.Default;

        float fontSize = _fontSize;
        var lines = BreakLinesToFit(_text, rect.Width, fontSize, out float lineHeight, out int totalLines);

        // Auto-shrink font if needed
        while (_minFontSize.HasValue && lineHeight * totalLines > rect.Height && fontSize > _minFontSize)
        {
            fontSize -= 1f;
            lines = BreakLinesToFit(_text, rect.Width, fontSize, out lineHeight, out totalLines);
        }

        // Vertical alignment
        float totalHeight = lineHeight * totalLines;
        float yOffset = _vAlign switch
        {
            VerticalAlign.Center => rect.MidY - totalHeight / 2,
            VerticalAlign.Bottom => rect.Bottom - totalHeight,
            _ => rect.Top
        };

        using var paint = new SKPaint
        {
            Typeface = _typeface,
            TextSize = fontSize,
            Color = _foreColor,
            IsAntialias = true,
            IsStroke = false,
            TextAlign = _hAlign
        };

        float y = yOffset;
        int linesDrawn = 0;

        foreach (var line in lines)
        {
            if (_maxLines.HasValue && linesDrawn >= _maxLines.Value)
                break;

            float x = _hAlign switch
            {
                SKTextAlign.Center => rect.MidX,
                SKTextAlign.Right => rect.Right,
                _ => rect.Left
            };

            if (_useShadow)
            {
                using var shadow = paint.Clone();
                shadow.Color = SKColors.Black.WithAlpha(100);
                canvas.DrawText(line, x + 2, y + 2, shadow);
            }

            if (_useOutline)
            {
                using var outline = paint.Clone();
                outline.IsStroke = true;
                outline.StrokeWidth = 1.5f;
                outline.Color = SKColors.Black;
                canvas.DrawText(line, x, y, outline);
            }

            canvas.DrawText(line, x, y, paint);
            y += lineHeight;
            linesDrawn++;
        }
    }

    private List<string> BreakLinesToFit(string text, float maxWidth, float fontSize, out float lineHeight, out int totalLines)
    {
        using var paint = new SKPaint
        {
            Typeface = _typeface,
            TextSize = fontSize,
            IsAntialias = true
        };

        lineHeight = paint.FontMetrics.Descent - paint.FontMetrics.Ascent + paint.FontMetrics.Leading;

        if (!_wrapText)
        {
            totalLines = 1;
            return new List<string> { text };
        }

        var words = text.Split(' ');
        var lines = new List<string>();
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            float width = paint.MeasureText(testLine);

            if (width <= maxWidth)
            {
                currentLine = testLine;
            }
            else
            {
                lines.Add(currentLine);
                currentLine = word;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);

        totalLines = lines.Count;
        return lines;
    }
}