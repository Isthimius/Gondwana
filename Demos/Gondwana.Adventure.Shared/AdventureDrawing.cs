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
        var floor = new SKColor(197, 195, 181);
        Fill(canvas, paint, floor, 0, 0, AdventureWorld.Width, AdventureWorld.Floor);
        var wall = new SKColor(room.Color);

        foreach (RectangleF rect in AdventureWorld.Walls(room))
            Fill(canvas, paint, wall, rect.X, rect.Y, rect.Width, rect.Height);

        if (AdventureWorld.IsCastle(room.Id))
        {
            for (int x = 400; x < 880; x += 80)
                Fill(canvas, paint, wall, x, 40, 45, 40);

            var gate = AdventureWorld.Gate;
            var color = _game.IsGateOpen(room.Id) ? new SKColor(64, 100, 62) : SKColors.Black;
            Fill(canvas, paint, color, gate.X, gate.Y, gate.Width, gate.Height);

            if (!_game.IsGateOpen(room.Id))
            {
                for (float x = gate.Left + 6; x < gate.Right; x += 14)
                    Fill(canvas, paint, wall, x, gate.Top, 5, gate.Height);
            }
        }

        if (AdventureWorld.Exterior(room.Id) != 0)
        {
            var gate = AdventureWorld.Gate;
            Fill(canvas, paint, SKColors.DarkSlateGray, gate.X, gate.Y, gate.Width, gate.Height);
        }

        if (room.Id == 3 && !_game.SecretOpen)
            Fill(canvas, paint, wall, AdventureWorld.Width - 6, 80, 6, 496);

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

        if (room.Dark)
        {
            using var darkness = new SKPath { FillType = SKPathFillType.EvenOdd };
            darkness.AddRect(new SKRect(0, 0, AdventureWorld.Width, AdventureWorld.Floor));
            darkness.AddCircle(_game.Player.X, _game.Player.Y, 116);
            paint.Color = new SKColor(32, 32, 35);
            canvas.DrawPath(darkness, paint);
        }

        Vector2 player = _game.Player;
        Fill(canvas, paint, SKColors.Gold, player.X - 10, player.Y - 10, 20, 20);
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
                Fill(canvas, paint, color, x - 55, y - 22, 110, 8);
                Fill(canvas, paint, color, x - 55, y + 14, 110, 8);
                Fill(canvas, paint, color, x - 55, y - 22, 8, 44);
                Fill(canvas, paint, color, x + 47, y - 22, 8, 44);
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
