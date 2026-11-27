using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using SkiaSharp;

namespace Gondwana.Demos.Adventure;

/// <summary>Procedural, deliberately low-resolution arcade-style artwork.</summary>
internal sealed class AdventureDrawing : DirectDrawingBase
{
    private readonly AdventureGame _game;

    internal AdventureDrawing(RenderSurfaceHostBase host, View view, AdventureGame game)
        : base(host, DirectDrawingMode.View, null, view,
            new Rectangle(0, 0, AdventureWorld.Width, AdventureWorld.Floor), null, "adventure-playfield")
    {
        _game = game;
    }

    internal void Invalidate() => ForceRefresh();

    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        var canvas = backbuffer.Canvas;
        var room = AdventureWorld.GetRoom(_game.RoomId);
        using var paint = new SKPaint { IsAntialias = false };
        canvas.Save();
        canvas.Translate(destRectScreen.Left, destRectScreen.Top);
        canvas.ClipRect(new SKRect(0, 0, AdventureWorld.Width, AdventureWorld.Floor));
        var floor = new SKColor(AdventureWorld.BackgroundColor);
        Fill(canvas, paint, floor, 0, 0, AdventureWorld.Width, AdventureWorld.Floor);
        var wall = new SKColor(room.Color);

        if (room.Dark)
        {
            // Invisible maze walls are only revealed near the player, rather
            // than replacing the entire room with a black screen.
            canvas.Save();
            Vector2 p = _game.Player;
            canvas.ClipRect(new SKRect(p.X - 116, p.Y - 116, p.X + 116, p.Y + 116));
            foreach (RectangleF rect in AdventureWorld.Walls(room, _game.SecretOpen))
                Fill(canvas, paint, new SKColor(0xff6c6c6c), rect.X, rect.Y, rect.Width, rect.Height);

            canvas.Restore();
        }
        else
        {
            foreach (RectangleF rect in AdventureWorld.Walls(room, _game.SecretOpen))
                Fill(canvas, paint, wall, rect.X, rect.Y, rect.Width, rect.Height);
        }

        if (AdventureWorld.IsCastle(room.Id) && !_game.IsGateOpen(room.Id))
        {
            var gate = AdventureWorld.Gate;
            Fill(canvas, paint, SKColors.Black, gate.X, gate.Y, gate.Width, gate.Height);

            for (float x = gate.Left + 6; x < gate.Right; x += 14)
                Fill(canvas, paint, wall, x, gate.Top, 5, gate.Height);
        }

        foreach (AdventureGame.Item item in _game.Items.Where(i => i.Room == room.Id))
            DrawItem(canvas, paint, item);

        foreach (AdventureGame.Dragon dragon in _game.Dragons.Where(d => d.Room == room.Id))
            DrawDragon(canvas, paint, dragon);

        if (_game.BatRoom == room.Id)
        {
            Vector2 b = _game.BatPosition;
            Fill(canvas, paint, SKColors.Black, b.X - 7, b.Y - 9, 14, 27);
            Fill(canvas, paint, SKColors.Black, b.X - 30, b.Y - 16, 23, 10);
            Fill(canvas, paint, SKColors.Black, b.X + 7, b.Y - 16, 23, 10);
            Fill(canvas, paint, SKColors.Black, b.X - 23, b.Y - 6, 16, 9);
            Fill(canvas, paint, SKColors.Black, b.X + 7, b.Y - 6, 16, 9);
        }

        Vector2 player = _game.Player;
        Fill(canvas, paint, new SKColor(0xffd0d050), player.X - AdventureWorld.PlayerRadius, player.Y - AdventureWorld.PlayerRadius,
            AdventureWorld.PlayerRadius * 2, AdventureWorld.PlayerRadius * 2);
        canvas.Restore();
    }

    private static void DrawItem(SKCanvas canvas, SKPaint paint, AdventureGame.Item item)
    {
        float x = item.Position.X;
        float y = item.Position.Y;
        var color = item.Kind switch
        {
            AdventureGame.ItemKind.GoldKey => SKColors.Goldenrod,
            AdventureGame.ItemKind.WhiteKey => SKColors.White,
            AdventureGame.ItemKind.BlackKey => SKColors.Black,
            AdventureGame.ItemKind.Sword => SKColors.Goldenrod,
            AdventureGame.ItemKind.Bridge => new SKColor(135, 62, 170),
            AdventureGame.ItemKind.Magnet => SKColors.Black,
            AdventureGame.ItemKind.Chalice => SKColors.Gold,
            _ => new SKColor(96, 96, 96)
        };

        switch (item.Kind)
        {
            case AdventureGame.ItemKind.GoldKey:
            case AdventureGame.ItemKind.WhiteKey:
            case AdventureGame.ItemKind.BlackKey:
                Fill(canvas, paint, SKColors.DarkSlateGray, x - 12, y - 9, 14, 18);
                Fill(canvas, paint, color, x - 10, y - 7, 10, 14);
                Fill(canvas, paint, color, x, y - 3, 29, 6);
                Fill(canvas, paint, color, x + 16, y + 3, 6, 8);
                Fill(canvas, paint, color, x + 23, y + 3, 6, 6);
                break;
            case AdventureGame.ItemKind.Sword:
                Fill(canvas, paint, color, x - 25, y - 3, 52, 6);
                Fill(canvas, paint, color, x - 16, y - 12, 6, 24);
                Fill(canvas, paint, SKColors.SaddleBrown, x - 26, y - 3, 10, 6);
                break;
            case AdventureGame.ItemKind.Bridge:
                const int halfWidth = AdventureWorld.BridgeHalfWidth;
                const int halfHeight = AdventureWorld.BridgeHalfHeight;
                Fill(canvas, paint, color, x - halfWidth, y - halfHeight, 8, halfHeight * 2);
                Fill(canvas, paint, color, x + halfWidth - 8, y - halfHeight, 8, halfHeight * 2);
                Fill(canvas, paint, color, x - halfWidth, y - halfHeight, 18, 8);
                Fill(canvas, paint, color, x + halfWidth - 18, y - halfHeight, 18, 8);
                Fill(canvas, paint, color, x - halfWidth, y + halfHeight - 8, 18, 8);
                Fill(canvas, paint, color, x + halfWidth - 18, y + halfHeight - 8, 18, 8);
                break;
            case AdventureGame.ItemKind.Magnet:
                Fill(canvas, paint, color, x - 14, y - 13, 28, 7);
                Fill(canvas, paint, color, x - 14, y - 6, 7, 25);
                Fill(canvas, paint, color, x + 7, y - 6, 7, 25);
                break;
            case AdventureGame.ItemKind.Chalice:
                Fill(canvas, paint, color, x - 16, y - 15, 32, 8);
                Fill(canvas, paint, color, x - 10, y - 7, 20, 9);
                Fill(canvas, paint, color, x - 4, y + 2, 8, 13);
                Fill(canvas, paint, color, x - 13, y + 15, 26, 6);
                break;
            default:
                Fill(canvas, paint, color, x - 2, y - 2, 4, 4);
                break;
        }
    }

    private static void DrawDragon(SKCanvas canvas, SKPaint paint, AdventureGame.Dragon dragon)
    {
        float x = dragon.Position.X;
        float y = dragon.Position.Y;
        var color = new SKColor(dragon.Color);

        if (!dragon.Alive)
        {
            Fill(canvas, paint, color, x - 24, y - 4, 48, 12);
            return;
        }

        Fill(canvas, paint, color, x - 19, y - 10, 38, 28);
        Fill(canvas, paint, color, x + 8, y - 39, 11, 38);
        Fill(canvas, paint, color, x + 6, y - 42, 27, 14);
        Fill(canvas, paint, color, x - 33, y - 22, 17, 16);
        Fill(canvas, paint, color, x - 14, y + 18, 8, 16);
        Fill(canvas, paint, color, x + 7, y + 18, 8, 16);
        Fill(canvas, paint, SKColors.Black, x + 19, y - 39, 5, 5);
        Fill(canvas, paint, SKColors.Black, x + 24, y - 27, 11, 5);
    }

    private static void Fill(SKCanvas canvas, SKPaint paint, SKColor color, float x, float y, float w, float h)
    {
        paint.Color = color;
        canvas.DrawRect(x, y, w, h, paint);
    }
}
