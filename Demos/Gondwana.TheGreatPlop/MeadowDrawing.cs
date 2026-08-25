using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using SkiaSharp;
using GondwanaView = Gondwana.Rendering.Views.View;

namespace Gondwana.Demos.TheGreatPlop;

internal sealed class MeadowDrawing : DirectDrawingBase
{
    internal static readonly Rectangle PlopButton = new(500, 595, 205, 102);
    private static readonly string[] Upgrades = ["1  ALFALFA $45", "2  CHILI $90", "3  PLUTONIUM $160", "4  DUAL CORE $240", "5  COMPRESS $350", "6  BEETLE $600"];
    private readonly PlopState _game;
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private readonly SKPaint _text = new() { IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };

    internal MeadowDrawing(RenderSurfaceHostBase host, GondwanaView view, Rectangle bounds, PlopState game)
        : base(host, DirectDrawingMode.View, null, view, bounds, null, "great-plop-world") => _game = game;

    internal void Invalidate() => ForceRefresh();

    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        SKCanvas c = backbuffer.Canvas;
        float jolt = _game.Shake * 9f;
        float sx = MathF.Sin(_game.TimeOfDay * 91f) * jolt;
        float sy = MathF.Cos(_game.TimeOfDay * 73f) * jolt;
        c.Save(); c.Translate(sx, sy);
        DrawSky(c); DrawMeadow(c); DrawStructures(c); DrawPlops(c); DrawCow(c); DrawAtmosphere(c);
        c.Restore();
        DrawDashboard(c);
    }

    private void DrawSky(SKCanvas c)
    {
        float daylight = Daylight;
        SKColor top = Lerp(new SKColor(5, 10, 34), new SKColor(72, 176, 226), daylight);
        SKColor horizon = Lerp(new SKColor(24, 31, 55), new SKColor(205, 236, 201), daylight);
        using var shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, 330), [top, horizon], [0f, 1f], SKShaderTileMode.Clamp);
        _paint.Shader = shader; c.DrawRect(0, 0, 1280, 340, _paint); _paint.Shader = null;
        float angle = (_game.TimeOfDay / 24f) * MathF.Tau - MathF.PI;
        float orbX = 640 + MathF.Cos(angle) * 520, orbY = 300 + MathF.Sin(angle) * 235;
        _paint.Color = daylight > .35f ? new SKColor(255, 235, 151) : new SKColor(218, 226, 245);
        c.DrawCircle(orbX, orbY, daylight > .35f ? 25 : 19, _paint);
        for (int i = 0; i < 9; i++)
        {
            float x = 115 + i * 137 + MathF.Sin(_game.TimeOfDay + i) * 16;
            float y = 78 + (i % 3) * 42;
            _paint.Color = new SKColor(255, 255, 255, (byte)(35 + daylight * 95));
            c.DrawOval(x, y, 55, 13, _paint); c.DrawOval(x + 32, y - 7, 39, 14, _paint);
        }
    }

    private void DrawMeadow(SKCanvas c)
    {
        using var shader = SKShader.CreateLinearGradient(new SKPoint(0, 240), new SKPoint(0, 570),
            [Lerp(new SKColor(17, 45, 39), new SKColor(76, 158, 62), Daylight), Lerp(new SKColor(8, 26, 23), new SKColor(38, 112, 45), Daylight)], [0f, 1f], SKShaderTileMode.Clamp);
        _paint.Shader = shader; c.DrawRect(0, 225, 1280, 350, _paint); _paint.Shader = null;
        float wind = MathF.Sin(_game.TimeOfDay * 4.3f);
        _paint.StrokeWidth = 2; _paint.Color = new SKColor(131, 203, 89, (byte)(80 + Daylight * 100));
        for (int y = 250; y < 555; y += 17)
            for (int x = (y % 34); x < 1280; x += 23)
                c.DrawLine(x, y, x + wind * 5 + MathF.Sin(x) * 2, y - 10 - x % 7, _paint);
        for (int i = 0; i < 74; i++)
        {
            float x = (i * 173) % 1240 + 20, y = 260 + (i * 83) % 285;
            if (Vector2.DistanceSquared(new(x, y), _game.Cow) < 2300) continue;
            _paint.Color = SKColors.White; c.DrawCircle(x, y, 2.4f, _paint);
            _paint.Color = new SKColor(247, 205, 48); c.DrawCircle(x, y, 1.1f, _paint);
        }
    }

    private void DrawStructures(SKCanvas c)
    {
        // Sell chute and its sharply dressed proprietor.
        _paint.Color = new SKColor(34, 29, 25); c.DrawRoundRect(new SKRect(20, 75, 185, 240), 18, 18, _paint);
        _paint.Color = new SKColor(104, 74, 47); c.DrawRoundRect(new SKRect(38, 100, 165, 225), 11, 11, _paint);
        _paint.Color = new SKColor(9, 10, 12); c.DrawOval(102, 189, 54, 32, _paint);
        Label(c, "SELL CHUTE", 102, 94, 20, SKColors.White, SKTextAlign.Center);
        DrawBeetle(c, new Vector2(175, 172), 1f);
        // Owl shop.
        _paint.Color = new SKColor(91, 53, 30); c.DrawRect(1032, 108, 1262, 250, _paint);
        _paint.Color = new SKColor(139, 82, 39); for (int x = 1042; x < 1260; x += 31) c.DrawRect(x, 116, 18, 130, _paint);
        _paint.Color = new SKColor(57, 35, 25); c.DrawPath(RoofPath(), _paint);
        Label(c, "OWL'S ODDITIES", 1147, 144, 20, new SKColor(251, 221, 135), SKTextAlign.Center);
        DrawOwl(c, new Vector2(1160, 205));
    }

    private void DrawPlops(SKCanvas c)
    {
        foreach (Plop p in _game.Plops.OrderBy(p => p.Position.Y))
        {
            float r = p.Radius, stretch = 1f + p.Squash;
            _paint.Color = new SKColor(0, 0, 0, 65); c.DrawOval(p.Position.X + 10, p.Position.Y + r * .62f, r * 1.05f, r * .37f, _paint);
            if (p.Radioactive) { _paint.Color = new SKColor(102, 255, 62, 55); c.DrawCircle(p.Position.X, p.Position.Y, r * 1.3f, _paint); }
            _paint.Color = p.Radioactive ? new SKColor(73, 219, 54) : p.Flaming ? new SKColor(133, 58, 25) : new SKColor(102, 58, 35);
            c.DrawOval(p.Position.X, p.Position.Y, r * stretch, r * (.78f / stretch), _paint);
            _paint.Color = p.Radioactive ? new SKColor(178, 255, 92) : new SKColor(170, 101, 57);
            c.DrawOval(p.Position.X - r * .22f, p.Position.Y - r * .18f, r * .45f, r * .18f, _paint);
            if (p.Flaming)
            {
                _paint.Color = new SKColor(255, 112, 28, 180);
                for (int i = 0; i < 4; i++) c.DrawOval(p.Position.X - r * .5f + i * r * .32f, p.Position.Y - r * .7f, r * .14f, r * .35f, _paint);
            }
            Label(c, $"${p.Value:0}", p.Position.X, p.Position.Y + 6, Math.Clamp(r * .24f, 10, 23), SKColors.White, SKTextAlign.Center);
        }
        if (_game.Beetle && _game.Plops.Count > 0)
        {
            Plop target = _game.Plops.OrderBy(p => p.Position.X).First();
            DrawBeetle(c, target.Position + new Vector2(target.Radius + 15, 2), .65f);
        }
    }

    private void DrawCow(SKCanvas c)
    {
        Vector2 p = _game.Cow;
        float stride = MathF.Sin(_game.TimeOfDay * 24f) * (_game.Charging ? 7 : 3);
        if (_game.Stun > 0) p += new Vector2(MathF.Sin(_game.Stun * 37) * 8, 0);
        if (_game.Charging) p += new Vector2(MathF.Sin(_game.Pressure * 150) * _game.Pressure * 7, 0);
        _paint.Color = new SKColor(0, 0, 0, 55); c.DrawOval(p.X, p.Y + 42, 61, 18, _paint);
        _paint.Color = _game.Charging ? Lerp(SKColors.White, new SKColor(240, 58, 45), _game.Pressure) : new SKColor(245, 238, 216);
        c.DrawOval(p.X, p.Y, 61 + MathF.Abs(stride), 42, _paint);
        _paint.Color = new SKColor(43, 38, 34); c.DrawOval(p.X - 22, p.Y - 5, 17, 21, _paint); c.DrawOval(p.X + 19, p.Y + 11, 14, 16, _paint);
        _paint.Color = new SKColor(245, 238, 216); c.DrawOval(p.X + 48, p.Y - 18, 31, 28, _paint);
        _paint.Color = new SKColor(226, 156, 151); c.DrawOval(p.X + 58, p.Y - 5, 22, 12, _paint);
        _paint.Color = new SKColor(13, 13, 16); c.DrawCircle(p.X + 50, p.Y - 24, 5, _paint); c.DrawCircle(p.X + 67, p.Y - 21, 5, _paint);
        _paint.StrokeWidth = 9; _paint.StrokeCap = SKStrokeCap.Round; c.DrawLine(p.X - 31, p.Y + 28, p.X - 34 - stride, p.Y + 51, _paint); c.DrawLine(p.X + 27, p.Y + 27, p.X + 31 + stride, p.Y + 51, _paint);
        _paint.StrokeWidth = 2; Label(c, _game.Stun > 0 ? "...moo?" : _game.Charging ? "HRRRNNNG" : "PLOP BESSIE", p.X, p.Y - 55, 14, SKColors.White, SKTextAlign.Center);
    }

    private void DrawAtmosphere(SKCanvas c)
    {
        float night = 1f - Daylight;
        if (night > .2f)
        {
            _paint.Color = new SKColor(8, 15, 40, (byte)(night * 115)); c.DrawRect(0, 0, 1280, 575, _paint);
            for (int i = 0; i < 20; i++)
            {
                float x = 35 + (i * 239) % 1200, y = 270 + (i * 97) % 250;
                float glow = .5f + .5f * MathF.Sin(_game.TimeOfDay * 8 + i);
                _paint.Color = new SKColor(246, 255, 126, (byte)(night * glow * 180)); c.DrawCircle(x, y, 3 + glow * 2, _paint);
            }
        }
    }

    private void DrawDashboard(SKCanvas c)
    {
        _paint.Color = new SKColor(30, 27, 25); c.DrawRect(0, 570, 1280, 150, _paint);
        _paint.Color = new SKColor(68, 61, 53); c.DrawRect(0, 570, 1280, 8, _paint);
        Label(c, $"${_game.Money:0}", 30, 616, 32, new SKColor(142, 244, 128), SKTextAlign.Left);
        Label(c, $"{_game.TimeOfDay:00.0}h", 32, 652, 15, new SKColor(195, 204, 210), SKTextAlign.Left);
        Label(c, "WASD MOVE  •  PUSH PLOPS INTO CHUTE  •  HOLD SPACE / BUTTON", 30, 687, 13, new SKColor(169, 165, 154), SKTextAlign.Left);

        // Springs and plush hydraulic button.
        bool hover = PlopButton.Contains(_game.Mouse);
        float press = _game.Charging ? 13 : hover ? MathF.Sin(_game.TimeOfDay * 30) * 3 : 0;
        _paint.Color = new SKColor(117, 109, 97); _paint.StrokeWidth = 6;
        for (int x = 525; x < 690; x += 38) { using var spring = new SKPath(); spring.MoveTo(x, 690); spring.LineTo(x + 11, 674); spring.LineTo(x - 3, 659); spring.LineTo(x + 10, 642); c.DrawPath(spring, _paint); }
        _paint.Color = new SKColor(48, 32, 26); c.DrawRoundRect(new SKRect(490, 626, 715, 706), 28, 28, _paint);
        _paint.Color = hover ? new SKColor(146, 87, 50) : new SKColor(116, 65, 38); c.DrawRoundRect(new SKRect(505, 595 + press, 700, 672 + press), 34, 34, _paint);
        _paint.Color = new SKColor(203, 132, 79); c.DrawRoundRect(new SKRect(520, 605 + press, 685, 626 + press), 12, 12, _paint);
        Label(c, _game.Charging ? "HOLD IT..." : "PLOP", 602, 650 + press, 28, SKColors.White, SKTextAlign.Center);

        // Pressure vessel.
        _paint.Color = new SKColor(16, 18, 20); c.DrawRoundRect(new SKRect(745, 591, 812, 701), 25, 25, _paint);
        float fill = Math.Clamp(_game.Pressure, 0, 1.13f) / 1.13f;
        SKColor liquid = _game.Pressure < .65f ? new SKColor(91, 197, 80) : _game.Pressure < .9f ? new SKColor(240, 172, 41) : new SKColor(113, 33, 135);
        _paint.Color = liquid; c.DrawRoundRect(new SKRect(754, 692 - fill * 91, 803, 692), 15, 15, _paint);
        _paint.Style = SKPaintStyle.Stroke; _paint.StrokeWidth = 4; _paint.Color = _game.Pressure > 1 ? SKColors.White : new SKColor(190, 207, 212); c.DrawRoundRect(new SKRect(745, 591, 812, 701), 25, 25, _paint); _paint.Style = SKPaintStyle.Fill;
        Label(c, _game.Pressure >= .9f ? "TITANIC" : "PRESSURE", 778, 584, 12, _game.Pressure >= .9f ? new SKColor(235, 121, 255) : SKColors.White, SKTextAlign.Center);

        bool[] owned = [_game.Alfalfa, _game.Chili, _game.Plutonium, _game.DualCore, _game.Compressor, _game.Beetle];
        for (int i = 0; i < Upgrades.Length; i++)
        {
            int x = 845 + (i % 2) * 205, y = 588 + (i / 2) * 39;
            _paint.Color = owned[i] ? new SKColor(47, 105, 67) : new SKColor(62, 57, 53); c.DrawRoundRect(new SKRect(x, y, x + 194, y + 31), 7, 7, _paint);
            Label(c, owned[i] ? "✓ " + Upgrades[i] : Upgrades[i], x + 9, y + 21, 12, owned[i] ? new SKColor(175, 255, 190) : new SKColor(224, 211, 191), SKTextAlign.Left);
        }
        if (_game.MessageTime > 0)
        {
            _paint.Color = new SKColor(14, 12, 12, 215); c.DrawRoundRect(new SKRect(300, 24, 980, 73), 18, 18, _paint);
            Label(c, _game.Message, 640, 57, 20, SKColors.White, SKTextAlign.Center);
        }
        if (_game.Stun > 0)
        {
            _paint.Color = new SKColor(235, 238, 232, (byte)(80 + 120 * _game.Stun / 3)); c.DrawRect(0, 0, 1280, 570, _paint);
            Label(c, $"BLOWOUT!  {_game.Stun:0.0}s", 640, 310, 48, new SKColor(93, 32, 31), SKTextAlign.Center);
        }
    }

    private void DrawBeetle(SKCanvas c, Vector2 p, float s)
    {
        _paint.Color = new SKColor(20, 25, 22); c.DrawOval(p.X, p.Y, 22 * s, 28 * s, _paint);
        _paint.Color = new SKColor(46, 83, 56); c.DrawOval(p.X, p.Y + 5 * s, 17 * s, 21 * s, _paint);
        _paint.Color = SKColors.Black; c.DrawRect(p.X - 20 * s, p.Y - 33 * s, 40 * s, 11 * s, _paint); c.DrawRect(p.X - 11 * s, p.Y - 52 * s, 22 * s, 22 * s, _paint);
        _paint.Color = new SKColor(236, 236, 225); c.DrawPath(TiePath(p, s), _paint);
    }

    private void DrawOwl(SKCanvas c, Vector2 p)
    {
        _paint.Color = new SKColor(108, 72, 42); c.DrawOval(p.X, p.Y, 34, 38, _paint);
        _paint.Color = new SKColor(236, 214, 157); c.DrawCircle(p.X - 13, p.Y - 10, 12, _paint); c.DrawCircle(p.X + 13, p.Y - 10, 12, _paint);
        _paint.Color = new SKColor(20, 15, 12); c.DrawCircle(p.X - 13, p.Y - 10, 5, _paint); c.DrawCircle(p.X + 13, p.Y - 10, 5, _paint);
        _paint.Color = new SKColor(213, 130, 42); using var beak = new SKPath(); beak.MoveTo(p.X - 5, p.Y); beak.LineTo(p.X + 10, p.Y); beak.LineTo(p.X + 1, p.Y + 11); beak.Close(); c.DrawPath(beak, _paint);
    }

    private float Daylight => Math.Clamp((MathF.Sin((_game.TimeOfDay - 6) / 24f * MathF.Tau) + .2f) * .72f, 0, 1);
    private static SKColor Lerp(SKColor a, SKColor b, float t) => new((byte)(a.Red + (b.Red - a.Red) * t), (byte)(a.Green + (b.Green - a.Green) * t), (byte)(a.Blue + (b.Blue - a.Blue) * t), (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    private void Label(SKCanvas c, string text, float x, float y, float size, SKColor color, SKTextAlign align) { _text.TextSize = size; _text.Color = color; _text.TextAlign = align; c.DrawText(text, x, y, _text); }
    private static SKPath RoofPath() { var p = new SKPath(); p.MoveTo(1008, 120); p.LineTo(1147, 52); p.LineTo(1280, 120); p.Close(); return p; }
    private static SKPath TiePath(Vector2 p, float s) { var path = new SKPath(); path.MoveTo(p.X - 5 * s, p.Y - 3 * s); path.LineTo(p.X + 5 * s, p.Y - 3 * s); path.LineTo(p.X + 2 * s, p.Y + 16 * s); path.LineTo(p.X - 2 * s, p.Y + 16 * s); path.Close(); return path; }

    protected override void Dispose(bool disposing) { if (disposing) { _paint.Dispose(); _text.Dispose(); } base.Dispose(disposing); }
}
