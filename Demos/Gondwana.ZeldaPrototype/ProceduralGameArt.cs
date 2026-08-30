using SkiaSharp;

namespace Gondwana.ZeldaPrototype;

internal static class ProceduralGameArt
{
    private const int FrameCount = GameArt.Flower + 1;

    internal static SKBitmap CreateTilesheetBitmap()
    {
        var bitmap = new SKBitmap(
            GameArt.TileSize * FrameCount,
            GameArt.TileSize,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        DrawGround(canvas, GameArt.Grass, new SKColor(74, 137, 69), new SKColor(88, 157, 77));
        DrawGround(canvas, GameArt.Path, new SKColor(176, 142, 90), new SKColor(149, 115, 75));
        DrawGround(canvas, GameArt.Water, new SKColor(43, 111, 173), new SKColor(92, 173, 218));
        DrawTree(canvas, GameArt.Tree);
        DrawRock(canvas, GameArt.Rock);
        DrawGround(canvas, GameArt.DungeonFloor, new SKColor(75, 68, 73), new SKColor(91, 82, 88));
        DrawGround(canvas, GameArt.DungeonWall, new SKColor(44, 39, 48), new SKColor(103, 92, 105));
        DrawEntrance(canvas, GameArt.Entrance);
        DrawHero(canvas, GameArt.PlayerUp, Facing.Up);
        DrawHero(canvas, GameArt.PlayerDown, Facing.Down);
        DrawHero(canvas, GameArt.PlayerLeft, Facing.Left);
        DrawHero(canvas, GameArt.PlayerRight, Facing.Right);
        DrawSlime(canvas, GameArt.Slime);
        DrawBat(canvas, GameArt.Bat);
        DrawElder(canvas, GameArt.Elder);
        DrawSword(canvas, GameArt.SwordUp, Facing.Up);
        DrawSword(canvas, GameArt.SwordDown, Facing.Down);
        DrawSword(canvas, GameArt.SwordLeft, Facing.Left);
        DrawSword(canvas, GameArt.SwordRight, Facing.Right);
        DrawPotion(canvas, GameArt.Potion);
        DrawKey(canvas, GameArt.Key);
        DrawBoss(canvas, GameArt.Boss);
        DrawGate(canvas, GameArt.Gate);
        DrawRelic(canvas, GameArt.Relic);
        DrawFlower(canvas, GameArt.Flower);

        canvas.Flush();
        return bitmap;
    }

    private static int Left(int frame) => frame * GameArt.TileSize;

    private static SKPaint Paint(SKColor color) => new()
    {
        Color = color,
        IsAntialias = false,
        Style = SKPaintStyle.Fill
    };

    private static void DrawGround(SKCanvas canvas, int frame, SKColor baseColor, SKColor accent)
    {
        int x = Left(frame);
        using var paint = Paint(baseColor);
        canvas.DrawRect(x, 0, 32, 32, paint);
        paint.Color = accent;
        canvas.DrawRect(x + 3, 6, 11, 2, paint);
        canvas.DrawRect(x + 18, 17, 12, 2, paint);
        canvas.DrawRect(x + 8, 26, 10, 2, paint);
    }

    private static void DrawTree(SKCanvas canvas, int frame)
    {
        int x = Left(frame);
        using var paint = Paint(new SKColor(85, 55, 32));
        canvas.DrawRect(x + 13, 19, 7, 13, paint);
        paint.Color = new SKColor(26, 84, 43);
        canvas.DrawCircle(x + 16, 12, 12, paint);
        paint.Color = new SKColor(67, 145, 65);
        canvas.DrawCircle(x + 11, 9, 6, paint);
        canvas.DrawCircle(x + 22, 8, 6, paint);
    }

    private static void DrawRock(SKCanvas canvas, int frame)
    {
        int x = Left(frame);
        using var paint = Paint(new SKColor(76, 86, 91));
        using var path = new SKPath();
        path.MoveTo(x + 4, 26);
        path.LineTo(x + 8, 10);
        path.LineTo(x + 17, 4);
        path.LineTo(x + 28, 12);
        path.LineTo(x + 29, 26);
        path.Close();
        canvas.DrawPath(path, paint);
        paint.Color = new SKColor(127, 139, 142);
        canvas.DrawRect(x + 11, 10, 9, 4, paint);
    }

    private static void DrawEntrance(SKCanvas canvas, int frame)
    {
        int x = Left(frame);
        using var paint = Paint(new SKColor(49, 44, 52));
        canvas.DrawRect(x + 3, 5, 26, 27, paint);
        paint.Color = new SKColor(119, 108, 107);
        canvas.DrawRect(x + 3, 3, 26, 5, paint);
        canvas.DrawRect(x + 3, 8, 4, 24, paint);
        canvas.DrawRect(x + 25, 8, 4, 24, paint);
        paint.Color = new SKColor(10, 9, 15);
        canvas.DrawRect(x + 9, 12, 14, 20, paint);
    }

    private static void DrawHero(SKCanvas canvas, int frame, Facing facing)
    {
        int x = Left(frame);
        using var paint = Paint(new SKColor(224, 190, 128));
        canvas.DrawRect(x + 10, 6, 12, 10, paint);
        paint.Color = new SKColor(36, 107, 60);
        canvas.DrawRect(x + 8, 14, 16, 12, paint);
        canvas.DrawRect(x + 11, 3, 10, 4, paint);
        paint.Color = new SKColor(48, 51, 60);
        canvas.DrawRect(x + 9, 26, 6, 5, paint);
        canvas.DrawRect(x + 18, 26, 6, 5, paint);
        paint.Color = SKColors.White;
        int eyeY = facing == Facing.Up ? 8 : 10;
        int eyeX = facing == Facing.Left ? 11 : facing == Facing.Right ? 19 : 12;
        canvas.DrawRect(x + eyeX, eyeY, 3, 3, paint);
        if (facing is Facing.Up or Facing.Down)
            canvas.DrawRect(x + 18, eyeY, 3, 3, paint);
    }

    private static void DrawSlime(SKCanvas canvas, int frame)
    {
        int x = Left(frame);
        using var paint = Paint(new SKColor(88, 183, 101));
        canvas.DrawOval(new SKRect(x + 4, 9, x + 28, 29), paint);
        paint.Color = new SKColor(26, 38, 35);
        canvas.DrawRect(x + 10, 18, 3, 4, paint);
        canvas.DrawRect(x + 20, 18, 3, 4, paint);
    }

    private static void DrawBat(SKCanvas canvas, int frame)
    {
        int x = Left(frame);
        using var paint = Paint(new SKColor(91, 62, 113));
        using var wings = new SKPath();
        wings.MoveTo(x + 16, 16);
        wings.LineTo(x + 2, 7);
        wings.LineTo(x + 7, 23);
        wings.LineTo(x + 16, 19);
        wings.LineTo(x + 30, 7);
        wings.LineTo(x + 25, 23);
        wings.Close();
        canvas.DrawPath(wings, paint);
        paint.Color = new SKColor(143, 92, 159);
        canvas.DrawCircle(x + 16, 18, 7, paint);
    }

    private static void DrawElder(SKCanvas canvas, int frame)
    {
        int x = Left(frame);
        using var paint = Paint(new SKColor(214, 176, 119));
        canvas.DrawRect(x + 10, 5, 12, 11, paint);
        paint.Color = new SKColor(222, 222, 209);
        canvas.DrawRect(x + 8, 3, 16, 5, paint);
        canvas.DrawRect(x + 9, 14, 14, 8, paint);
        paint.Color = new SKColor(82, 73, 135);
        canvas.DrawRect(x + 7, 20, 18, 11, paint);
    }

    private static void DrawSword(SKCanvas canvas, int frame, Facing facing)
    {
        int x = Left(frame);
        using var blade = Paint(new SKColor(236, 242, 242));
        using var hilt = Paint(new SKColor(236, 183, 64));
        if (facing is Facing.Up or Facing.Down)
        {
            int top = facing == Facing.Up ? 2 : 9;
            canvas.DrawRect(x + 15, top, 3, 21, blade);
            canvas.DrawRect(x + 11, facing == Facing.Up ? 21 : 8, 11, 3, hilt);
        }
        else
        {
            int left = facing == Facing.Left ? 2 : 9;
            canvas.DrawRect(x + left, 15, 21, 3, blade);
            canvas.DrawRect(x + (facing == Facing.Left ? 21 : 8), 11, 3, 11, hilt);
        }
    }

    private static void DrawPotion(SKCanvas canvas, int frame)
    {
        int x = Left(frame);
        using var paint = Paint(new SKColor(230, 225, 195));
        canvas.DrawRect(x + 12, 4, 9, 5, paint);
        canvas.DrawRect(x + 9, 9, 15, 19, paint);
        paint.Color = new SKColor(193, 45, 67);
        canvas.DrawRect(x + 11, 15, 11, 11, paint);
    }

    private static void DrawKey(SKCanvas canvas, int frame)
    {
        int x = Left(frame);
        using var paint = Paint(new SKColor(244, 195, 55));
        canvas.DrawCircle(x + 11, 11, 7, paint);
        paint.Color = new SKColor(74, 137, 69);
        canvas.DrawCircle(x + 11, 11, 3, paint);
        paint.Color = new SKColor(244, 195, 55);
        canvas.DrawRect(x + 16, 9, 13, 4, paint);
        canvas.DrawRect(x + 24, 13, 4, 6, paint);
    }

    private static void DrawBoss(SKCanvas canvas, int frame)
    {
        int x = Left(frame);
        using var paint = Paint(new SKColor(112, 35, 46));
        canvas.DrawCircle(x + 16, 17, 13, paint);
        paint.Color = new SKColor(181, 55, 61);
        canvas.DrawRect(x + 6, 12, 20, 13, paint);
        paint.Color = new SKColor(232, 196, 102);
        canvas.DrawRect(x + 8, 2, 4, 9, paint);
        canvas.DrawRect(x + 20, 2, 4, 9, paint);
    }

    private static void DrawGate(SKCanvas canvas, int frame)
    {
        int x = Left(frame);
        using var paint = Paint(new SKColor(116, 84, 53));
        for (int offset = 3; offset < 30; offset += 11)
            canvas.DrawRect(x + offset, 0, 5, 32, paint);
        paint.Color = new SKColor(190, 146, 72);
        canvas.DrawRect(x + 1, 8, 30, 4, paint);
        canvas.DrawRect(x + 1, 23, 30, 4, paint);
    }

    private static void DrawRelic(SKCanvas canvas, int frame)
    {
        int x = Left(frame);
        using var glow = Paint(new SKColor(255, 223, 83, 85));
        canvas.DrawCircle(x + 16, 16, 14, glow);
        using var paint = Paint(new SKColor(255, 205, 51));
        using var path = new SKPath();
        path.MoveTo(x + 16, 3);
        path.LineTo(x + 21, 12);
        path.LineTo(x + 29, 16);
        path.LineTo(x + 21, 20);
        path.LineTo(x + 16, 29);
        path.LineTo(x + 11, 20);
        path.LineTo(x + 3, 16);
        path.LineTo(x + 11, 12);
        path.Close();
        canvas.DrawPath(path, paint);
    }

    private static void DrawFlower(SKCanvas canvas, int frame)
    {
        int x = Left(frame);
        using var paint = Paint(new SKColor(74, 137, 69));
        canvas.DrawRect(x, 0, 32, 32, paint);
        paint.Color = new SKColor(242, 241, 226);
        canvas.DrawCircle(x + 16, 11, 4, paint);
        canvas.DrawCircle(x + 11, 16, 4, paint);
        canvas.DrawCircle(x + 21, 16, 4, paint);
        paint.Color = new SKColor(235, 219, 92);
        canvas.DrawCircle(x + 16, 16, 4, paint);
    }
}
