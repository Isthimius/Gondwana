using SkiaSharp;
using System.Drawing;

namespace Gondwana.Rendering.Direct;

public class Text : DirectDrawing
{
    private string _text;
    private string _fontFamily;
    private float _fontSize;
    private SKColor _foreColor;
    private SKColor _backColor;

    public Text(VisibleSurfaceBase surface, string text, Font font, Rectangle bounds, Color foreColor, Color backColor)
        : base(surface, bounds)
    {
        _text = text;
        _fontFamily = font.FontFamily.Name;
        _fontSize = font.Size;
        _foreColor = foreColor.ToSKColor();
        _backColor = backColor.ToSKColor();
    }

    public string TextDisplay
    {
        get => _text;
        set
        {
            _text = value;
            ForceRefresh();
        }
    }

    protected internal override void Render()
    {
        var canvas = _surface.Buffer.Canvas;
        var rect = Bounds.ToSKRect();

        // Fill background
        using var bgPaint = new SKPaint { Color = _backColor };
        canvas.DrawRect(rect, bgPaint);

        // Draw text
        using var font = SKFontManager.Default.MatchFamily(_fontFamily) is { } tf
            ? SKTypeface.FromFamilyName(_fontFamily)
            : SKTypeface.Default;

        using var paint = new SKPaint
        {
            Typeface = font,
            TextSize = _fontSize,
            IsAntialias = true,
            Color = _foreColor,
            IsStroke = false
        };

        var x = rect.Left;
        var y = rect.MidY + _fontSize / 2f; // approximate centering
        canvas.DrawText(_text, x, y, paint);
    }
}
