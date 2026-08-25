using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp;
using GondwanaView = Gondwana.Rendering.Views.View;

namespace Gondwana.Demos.RageToPro;

internal sealed class BedroomDrawing : DirectDrawingBase
{
    internal static readonly Rectangle PlayButton = new(64, 610, 330, 72);
    internal static readonly Rectangle[] ShopButtons =
    [
        new(820, 175, 390, 72), new(820, 260, 390, 72),
        new(820, 345, 390, 72), new(820, 430, 390, 72)
    ];

    private readonly GamerState _game;
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private readonly SKPaint _text = new() { IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };

    internal BedroomDrawing(RenderSurfaceHostBase host, GondwanaView view, Rectangle bounds, GamerState game)
        : base(host, DirectDrawingMode.View, null, view, bounds, null, "rage-to-pro-room") => _game = game;

    internal void Invalidate() => ForceRefresh();

    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        SKCanvas c = backbuffer.Canvas;
        float shake = _game.SmashTime > 0 ? MathF.Sin(_game.SmashTime * 57) * 11 : 0;
        c.Save(); c.Translate(shake, 0);
        DrawRoom(c);
        DrawDesk(c);
        DrawGamer(c);
        c.Restore();
        DrawHud(c);
    }

    private void DrawRoom(SKCanvas c)
    {
        _paint.Color = new SKColor(31, 35, 52); c.DrawRect(0, 0, 790, 720, _paint);
        _paint.Color = new SKColor(45, 49, 70); c.DrawRect(0, 470, 790, 250, _paint);
        _paint.Color = new SKColor(21, 23, 34); c.DrawRect(0, 550, 790, 170, _paint);
        _paint.Color = new SKColor(105, 74, 124); c.DrawRoundRect(new SKRect(45, 50, 220, 175), 8, 8, _paint);
        Label(c, "NO LAG", 132, 112, 26, new SKColor(255, 203, 91), SKTextAlign.Center);
        Label(c, "NO FEAR", 132, 143, 17, SKColors.White, SKTextAlign.Center);
    }

    private void DrawDesk(SKCanvas c)
    {
        _paint.Color = new SKColor(103, 70, 49); c.DrawRoundRect(new SKRect(240, 390, 720, 430), 8, 8, _paint);
        c.DrawRect(275, 425, 25, 185, _paint); c.DrawRect(670, 425, 25, 185, _paint);
        SKColor pc = _game.UpgradeLevel switch
        {
            0 => new(88, 86, 82), 1 => new(74, 81, 91), 2 => new(30, 34, 43), _ => new(24, 25, 35)
        };
        _paint.Color = pc; c.DrawRoundRect(new SKRect(548, 205, 706, 374), 10, 10, _paint);
        _paint.Color = _game.Playing ? new SKColor(67, 238, 158) : new SKColor(43, 53, 63); c.DrawRoundRect(new SKRect(563, 220, 691, 348), 5, 5, _paint);
        if (_game.UpgradeLevel >= 2)
        {
            _paint.Style = SKPaintStyle.Stroke; _paint.StrokeWidth = 5; _paint.Color = new SKColor(128, 88, 255);
            c.DrawRoundRect(new SKRect(548, 205, 706, 374), 10, 10, _paint); _paint.Style = SKPaintStyle.Fill;
        }
        Label(c, _game.Playing ? "GG" : "...", 627, 294, 42, new SKColor(18, 24, 29), SKTextAlign.Center);
        _paint.Color = new SKColor(32, 34, 42); c.DrawRect(435, 392, 165, 16, _paint);
    }

    private void DrawGamer(SKCanvas c)
    {
        float bob = _game.Playing ? MathF.Sin(_game.TotalHours * 2.2f + _game.HourProgress * MathF.Tau) * 4 : 0;
        float rage = _game.Rage / 100f;
        _paint.Color = new SKColor(24, 25, 31); c.DrawRoundRect(new SKRect(290, 300, 425, 540), 28, 28, _paint);
        _paint.Color = new SKColor(67, 111, 178); c.DrawOval(376, 390 + bob, 78, 95, _paint);
        _paint.Color = new SKColor((byte)(229 + 20 * rage), (byte)(184 - 75 * rage), (byte)(145 - 70 * rage)); c.DrawCircle(405, 300 + bob, 55, _paint);
        _paint.Color = new SKColor(48, 34, 28); c.DrawArc(new SKRect(349, 240 + bob, 461, 335 + bob), 180, 180, true, _paint);
        _paint.Color = new SKColor(24, 20, 22); c.DrawCircle(387, 295 + bob, 5, _paint); c.DrawCircle(425, 295 + bob, 5, _paint);
        _paint.StrokeWidth = 5; _paint.Style = SKPaintStyle.Stroke; _paint.Color = new SKColor(60, 28, 30);
        c.DrawArc(new SKRect(386, 310 + bob, 426, 337 + bob), rage > .65f ? 195 : 20, rage > .65f ? 150 : 140, false, _paint);
        _paint.Style = SKPaintStyle.Fill;
        if (_game.SmashTime > 0) Label(c, "!!!", 405, 205, 48, new SKColor(255, 66, 66), SKTextAlign.Center);
        else if (_game.IsPro) Label(c, "PRO", 405, 215, 25, new SKColor(255, 218, 91), SKTextAlign.Center);
    }

    private void DrawHud(SKCanvas c)
    {
        _paint.Color = new SKColor(16, 17, 25); c.DrawRect(790, 0, 490, 720, _paint);
        Label(c, "RAGE TO PRO", 840, 58, 36, new SKColor(157, 115, 255), SKTextAlign.Left);
        Label(c, $"${_game.Money:0}     {_game.TotalHours:0}h played", 840, 98, 23, new SKColor(126, 242, 168), SKTextAlign.Left);
        Label(c, $"Pay: ${_game.HourlyPay:0}/hour", 840, 130, 16, new SKColor(172, 179, 198), SKTextAlign.Left);

        for (int i = 0; i < ShopButtons.Length; i++)
        {
            Rectangle b = ShopButtons[i]; bool owned = i < _game.UpgradeLevel; bool next = i == _game.UpgradeLevel;
            _paint.Color = owned ? new SKColor(39, 94, 68) : next ? new SKColor(62, 51, 92) : new SKColor(40, 42, 55);
            c.DrawRoundRect(new SKRect(b.Left, b.Top, b.Right, b.Bottom), 12, 12, _paint);
            Label(c, owned ? $"✓  {GamerState.UpgradeNames[i]}" : $"{i + 1}  {GamerState.UpgradeNames[i]}", b.Left + 18, b.Top + 30, 18, owned ? new SKColor(157, 247, 184) : SKColors.White, SKTextAlign.Left);
            Label(c, owned ? "INSTALLED" : $"${GamerState.UpgradeCosts[i]}", b.Right - 18, b.Top + 55, 15, new SKColor(197, 183, 229), SKTextAlign.Right);
        }

        Label(c, "RAGE", 840, 545, 17, SKColors.White, SKTextAlign.Left);
        _paint.Color = new SKColor(45, 47, 60); c.DrawRoundRect(new SKRect(840, 560, 1210, 592), 12, 12, _paint);
        _paint.Color = _game.Rage < 55 ? new SKColor(91, 207, 115) : _game.Rage < 82 ? new SKColor(244, 174, 55) : new SKColor(245, 67, 75);
        c.DrawRoundRect(new SKRect(840, 560, 840 + 370 * _game.Rage / 100f, 592), 12, 12, _paint);
        Label(c, $"{_game.Rage:0}%", 1025, 583, 16, SKColors.White, SKTextAlign.Center);

        _paint.Color = _game.Playing ? new SKColor(169, 63, 76) : new SKColor(57, 139, 98);
        c.DrawRoundRect(new SKRect(PlayButton.Left, PlayButton.Top, PlayButton.Right, PlayButton.Bottom), 18, 18, _paint);
        Label(c, _game.Playing ? "PAUSE & COOL OFF" : "PLAY AN HOUR", 229, 656, 25, SKColors.White, SKTextAlign.Center);
        Label(c, "SPACE play/pause  •  1–4 shop  •  R reset  •  ESC quit", 840, 640, 14, new SKColor(159, 164, 183), SKTextAlign.Left);
        Label(c, $"Broken stuff: {_game.ThingsBroken}", 840, 675, 15, new SKColor(221, 137, 140), SKTextAlign.Left);

        if (_game.MessageTime > 0)
        {
            _paint.Color = new SKColor(9, 10, 16, 225); c.DrawRoundRect(new SKRect(55, 22, 738, 78), 16, 16, _paint);
            Label(c, _game.Message, 396, 58, 17, SKColors.White, SKTextAlign.Center);
        }
    }

    private void Label(SKCanvas c, string value, float x, float y, float size, SKColor color, SKTextAlign align)
    {
        _text.TextSize = size; _text.Color = color; _text.TextAlign = align; c.DrawText(value, x, y, _text);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _paint.Dispose(); _text.Dispose(); }
        base.Dispose(disposing);
    }
}
