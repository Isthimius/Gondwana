using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Rendering.Views;
using Gondwana.SkiaSharp;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Gondwana.Rendering.Backbuffers;

/// <summary>
/// Represents a base class for managing a graphical backbuffer, and is the in-memory surface where
/// rendering operations are performed before being presented to the display.
/// </summary>
/// <remarks>This abstract class serves as the foundation for backbuffer implementations, offering methods and
/// properties to facilitate rendering operations, manage graphical state, and interact with graphical elements such as
/// tiles. Derived classes must implement the <see cref="Canvas"/>, <see cref="DrawTileFrame(Tile)"/>, and <see
/// cref="Snapshot"/> members to define specific rendering behavior.</remarks>
public abstract class BackbufferBase : IDisposable
{
    private int _width;
    private int _height;

    private BackbufferBase()
    { }

    protected BackbufferBase(int width, int height)
        : this()
    {
        _width = width;
        _height = height;
    }

    public abstract SKCanvas Canvas { get; }

    protected internal abstract SKImage Snapshot();

    protected internal abstract void BeginFrame();

    protected internal abstract void DrawTileFrame(Tile tile, RectangleF destRectScreen);

    protected internal abstract void EndFrame();

    /// <summary>
    /// Gets or sets the paint object used to render fog effects.
    /// </summary>
    public SKPaint FogPaint { get; set; } = new()
    {
        Color = new SKColor(0, 0, 0, 128),
        IsAntialias = true
    };

    /// <summary>
    /// Gets or sets the paint settings used to render grid lines.
    /// </summary>
    public SKPaint GridLinePaint { get; set; } = new()
    {
        Color = SKColors.White,
        IsStroke = true,
        StrokeWidth = 1
    };

    /// <summary>
    /// Gets or sets the paint settings used to render collision boxes.
    /// </summary>
    public SKPaint CollisionBoxPaint { get; set; } = new()
    {
        Color = SKColors.Green,
        IsStroke = true,
        StrokeWidth = 1
    };

    /// <summary>
    /// Gets the current Backbuffer width in a thread-safe manner.
    /// </summary>
    public int Width => Volatile.Read(ref _width);

    /// <summary>
    /// Gets the current Backbuffer height in a thread-safe manner.
    /// </summary>
    public int Height => Volatile.Read(ref _height);

    protected internal virtual void RequestResize(int width, int height)
    { /* no-op by default */ }

    protected internal event Action<int, int>? SizeChanged;

    protected void UpdateSize(int width, int height)
    {
        Volatile.Write(ref _width, width);
        Volatile.Write(ref _height, height);

        SizeChanged?.Invoke(width, height);
    }

    /// <summary>
    /// Union of all rectangle areas redrawn on the current frame, to be rendered to the UI adapter.
    /// <para />***** IMPORTANT: DirtyRectangle is ALWAYS in adapter/control SCREEN pixels. *****
    /// </summary>
    protected internal Rectangle DirtyRectangle { get; private set; }

    private SKColor _clearColor = SKColors.Black;
    protected readonly SKPaint _fillPaint = new()
    {
        IsAntialias = false,
        BlendMode = SKBlendMode.Src,
        Style = SKPaintStyle.Fill,
        FilterQuality = SKFilterQuality.None,
    };

    /// <summary>
    /// Gets or sets the color used to clear the drawing surface.
    /// </summary>
    public SKColor ClearColor
    {
        get => _clearColor;
        set
        {
            _clearColor = value;
            _fillPaint.Color = value;
        }
    }

    internal void ClearRect(Rectangle rectPx)
    {
        if (rectPx.IsEmpty)
            return;

        _fillPaint.Color = ClearColor;

        // Screen-pixel space
        Canvas.Save();
        Canvas.ResetMatrix();

        // Rect is expected to be in the current canvas coordinate space.
        Canvas.DrawRect(rectPx.ToSKRect(), _fillPaint);

        // mark area as dirty so it gets presented to the UI adapter
        AddToBackbufferDirtyRectangle(rectPx);

        Canvas.Restore();
    }

    internal void DrawDrawables(View view, IEnumerable<IDrawable> drawables, Rectangle clipRect)
    {
        Canvas.Save();
        Canvas.ClipRect(clipRect.ToSKRect());

        var tiles = new List<Tile>();

        foreach (var drawable in drawables)
        {
            if (!drawable.Visible)
                continue;

            var destRectScreen = drawable.GetDrawLocationScreen(view);
            drawable.Draw(this, destRectScreen);

            AddToBackbufferDirtyRectangle(destRectScreen.ToPixelAlignedRect());

            if (drawable is Tile tile)
                tiles.Add(tile);
        }

        PostDrawTiles(view, tiles);

        Canvas.Restore();
    }

    private void PostDrawTiles(View view, List<Tile> tiles)
    {
        foreach (var tile in tiles)
        {
            // WORLD -> SCREEN conversion
            var ptsScreen = tile.OutlinePointsWorld
                .Select(p => view.WorldPxToScreenPx(tile.SceneLayer, new PointF(p.X, p.Y)))
                .Select(sp => new SKPoint(sp.X, sp.Y))
                .ToArray();

            // close polygon when needed
            static SKPoint[] Enclose(SKPoint[] pts)
            {
                if (pts.Length == 0) return pts;
                var arr = new SKPoint[pts.Length + 1];
                Array.Copy(pts, arr, pts.Length);
                arr[^1] = pts[0];
                return arr;
            }

            if (tile.EnableFog)
            {
                using var path = new SKPath();
                path.AddPoly(ptsScreen, close: true);
                Canvas.DrawPath(path, FogPaint);
            }

            if (tile.SceneLayer.ShowGridLines && tile.Visible && tile.IsPositionFixed)
                Canvas.DrawPoints(SKPointMode.Polygon, Enclose(ptsScreen), GridLinePaint);

            if (tile.SceneLayer.ShowCollisionBoxes && tile.Visible)
                Canvas.DrawPoints(SKPointMode.Polygon, Enclose(ptsScreen), CollisionBoxPaint);
        }
    }

    /// <summary>
    /// ***** IMPORTANT: should ALWAYS be in adapter/control SCREEN pixels. *****
    /// This is used to signal to the UI adapter what needs to be repainted.
    /// </summary>
    protected internal void AddToBackbufferDirtyRectangle(Rectangle area)
    {
        if (area.IsEmpty)
            return;

        DirtyRectangle = DirtyRectangle.IsEmpty
            ? area
            : Rectangle.Union(DirtyRectangle, area);
    }

    protected internal void ClearDirtyRectangle()
    {
        DirtyRectangle = Rectangle.Empty;
    }

    /// <summary>
    /// Converts the current image to a byte array in the specified format and quality.
    /// </summary>
    /// <remarks>This method creates a snapshot of the current image and encodes it into the specified format.
    /// The resulting byte array can be used for saving the image to a file, transmitting it over a network, or other
    /// purposes requiring a binary representation of the image.</remarks>
    /// <param name="format">The format to encode the image in. The default is <see cref="SKEncodedImageFormat.Png"/>.</param>
    /// <param name="quality">The quality of the encoded image, ranging from 0 (lowest quality) to 100 (highest quality). This parameter is
    /// ignored for formats that do not support quality settings. The default is 100.</param>
    /// <returns>A byte array containing the encoded image data.</returns>
    public virtual byte[] ToByteArray(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
    {
        using var image = Snapshot();
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }

    public virtual void Dispose()
    {
        _fillPaint.Dispose();
        FogPaint.Dispose();
        GridLinePaint.Dispose();
        CollisionBoxPaint.Dispose();
    }
}