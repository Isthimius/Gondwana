using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using SkiaSharp;

namespace Gondwana.Widgets.Menus;

// Geometry avoids depending on whether the platform default font contains menu symbols.
internal sealed class MenuIndicatorDrawing : DirectDrawingMovableBase
{
    internal enum Shape { Check, Radio, Arrow }
    private readonly Shape _shape;
    private readonly MenuBarTheme _theme;
    private bool _active;
    private SKColor _color;

    internal MenuIndicatorDrawing(RenderSurfaceHostBase host, View view, Rectangle bounds,
        Shape shape, MenuBarTheme theme, string nickname)
        : base(host, DirectDrawingMode.View, null, view, bounds, null, nickname)
    {
        _shape = shape;
        _theme = theme;
    }

    internal void SetState(bool active, SKColor color)
    {
        _active = active;
        _color = color;
        ForceRefresh();
    }

    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        if (!_active) return;
        float size = Math.Min(_theme.IndicatorSize, Math.Min(destRectScreen.Width, destRectScreen.Height));
        float x = destRectScreen.X + (destRectScreen.Width - size) / 2;
        float y = destRectScreen.Y + (destRectScreen.Height - size) / 2;
        using var paint = new SKPaint
        {
            Color = _color,
            IsAntialias = true,
            StrokeWidth = _theme.IndicatorStrokeWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Style = _shape == Shape.Radio ? SKPaintStyle.Fill : SKPaintStyle.Stroke
        };
        if (_shape == Shape.Radio)
        {
            backbuffer.Canvas.DrawCircle(x + size / 2, y + size / 2, size / 4, paint);
            return;
        }
        using var path = new SKPath();
        if (_shape == Shape.Check)
        {
            path.MoveTo(x + size * .15f, y + size * .5f);
            path.LineTo(x + size * .4f, y + size * .75f);
            path.LineTo(x + size * .85f, y + size * .2f);
        }
        else
        {
            path.MoveTo(x + size * .35f, y + size * .2f);
            path.LineTo(x + size * .65f, y + size * .5f);
            path.LineTo(x + size * .35f, y + size * .8f);
        }
        backbuffer.Canvas.DrawPath(path, paint);
    }
}
