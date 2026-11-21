using System.Numerics;
using Gondwana.Drawing;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using SkiaSharp;
using SpriteHorizontalAlignment = Gondwana.Drawing.Sprites.HorizontalAlignment;
using SpriteVerticalAlignment = Gondwana.Drawing.Sprites.VerticalAlignment;

namespace Gondwana.Demos.Slider;

/// <summary>Maps a platform-neutral puzzle board onto Gondwana sprites and atlas frames.</summary>
internal sealed class PuzzleRenderer : IDisposable
{
    private const int SceneTileSize = 64;
    private readonly SceneLayer _layer;
    private readonly TilesheetRegistry _registry;
    private readonly SpriteManager _sprites;
    private readonly Dictionary<int, Sprite> _pieces = [];
    private Tilesheet? _plain;
    private Tilesheet? _outlined;
    private int _columns;
    private int _rows;
    private int _cellWidth;
    private int _cellHeight;

    internal PuzzleRenderer(SceneLayer layer, TilesheetRegistry registry, SpriteManager sprites)
    {
        _layer = layer;
        _registry = registry;
        _sprites = sprites;
    }

    internal int WidthPx => _columns * _cellWidth;
    internal int HeightPx => _rows * _cellHeight;

    internal void Load(SKBitmap image, SlidingPuzzle board, bool showGrid)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width < board.Columns || image.Height < board.Rows)
            throw new InvalidDataException("The source image must be at least one pixel per puzzle cell.");

        // Preserve the image's aspect ratio. Rounding to whole cell sizes introduces
        // at most a fraction of a cell of distortion and guarantees exact frame slices.
        float scale = Math.Min(900f / image.Width, 650f / image.Height);
        int cellWidth = Math.Max(1, (int)Math.Round(image.Width * scale / board.Columns));
        int cellHeight = Math.Max(1, (int)Math.Round(image.Height * scale / board.Rows));
        int atlasWidth = cellWidth * board.Columns;
        int atlasHeight = cellHeight * board.Rows;

        var resized = new SKBitmap(atlasWidth, atlasHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(resized))
        {
            canvas.Clear(SKColors.Black);
            using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
            canvas.DrawBitmap(image, new SKRect(0, 0, atlasWidth, atlasHeight), paint);
            canvas.Flush();
        }

        var outlined = new SKBitmap(atlasWidth, atlasHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(outlined))
        {
            canvas.DrawBitmap(resized, 0f, 0f);
            using var border = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 190),
                IsAntialias = false,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f
            };

            for (int y = 0; y < board.Rows; y++)
                for (int x = 0; x < board.Columns; x++)
                    canvas.DrawRect(x * cellWidth + 1, y * cellHeight + 1,
                        cellWidth - 2, cellHeight - 2, border);
            canvas.Flush();
        }

        // Release existing sprite references before removing the source atlases.
        Clear();

        _columns = board.Columns;
        _rows = board.Rows;
        _cellWidth = cellWidth;
        _cellHeight = cellHeight;
        _plain = _registry.LoadFromBitmap("slider-plain-" + Guid.NewGuid().ToString("N"), resized);
        _outlined = _registry.LoadFromBitmap("slider-grid-" + Guid.NewGuid().ToString("N"), outlined);
        _plain.DefaultRegion.TileSize = new Size(cellWidth, cellHeight);
        _outlined.DefaultRegion.TileSize = new Size(cellWidth, cellHeight);

        for (int piece = 0; piece < board.TotalPieces; piece++)
        {
            Sprite sprite = _sprites.CreateSprite(_layer,
                FrameFor(piece, showGrid), piece.ToString());
            sprite.HorizAlign = SpriteHorizontalAlignment.Left;
            sprite.VertAlign = SpriteVerticalAlignment.Top;
            sprite.RenderSize = new Size(cellWidth, cellHeight);
            sprite.Visible = true;
            sprite.ZOrder = 20;
            _pieces.Add(piece, sprite);
        }

        SnapToBoard(board);
    }

    internal Sprite? GetPiece(int piece) => _pieces.GetValueOrDefault(piece);

    internal void SnapToBoard(SlidingPuzzle board)
    {
        for (int row = 0; row < board.Rows; row++)
            for (int col = 0; col < board.Columns; col++)
            {
                int piece = board.PieceAt(col, row);
                if (piece >= 0)
                    _pieces[piece].SetPosition(PositionAt(col, row));
            }
    }

    internal Vector2 PositionForIndex(int index)
        => PositionAt(index % _columns, index / _columns);

    internal void ShowGrid(bool show)
    {
        foreach (var (piece, sprite) in _pieces)
            sprite.CurrentFrame = FrameFor(piece, show);
    }

    private Frame FrameFor(int piece, bool show)
    {
        var atlas = show ? _outlined : _plain;
        return atlas![piece % _columns, piece / _columns];
    }

    private Vector2 PositionAt(int column, int row)
        => new(((WorldSize - WidthPx) / 2f + column * _cellWidth) / SceneTileSize,
               ((WorldSize - HeightPx) / 2f + row * _cellHeight) / SceneTileSize);

    private const int WorldSize = 20 * SceneTileSize;

    internal void Clear()
    {
        foreach (Sprite sprite in _pieces.Values)
            sprite.Dispose();
        _pieces.Clear();

        if (_plain is not null)
            _registry.Remove(_plain.Name, dispose: true);
        if (_outlined is not null)
            _registry.Remove(_outlined.Name, dispose: true);
        _plain = null;
        _outlined = null;
    }

    public void Dispose() => Clear();
}
