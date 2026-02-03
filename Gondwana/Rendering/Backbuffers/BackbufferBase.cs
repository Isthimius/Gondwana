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
/// tiles. Derived classes must implement the <see cref="Canvas"/>, <see cref="DrawTileFrame(Tile, RectangleF)"/>, and <see
/// cref="Snapshot"/> members to define specific rendering behavior.</remarks>
public abstract class BackbufferBase : IDisposable
{
    private int _width;
    private int _height;

    private BackbufferBase()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackbufferBase"/> class with the specified dimensions.
    /// </summary>
    /// <param name="width">The width of the backbuffer in pixels.</param>
    /// <param name="height">The height of the backbuffer in pixels.</param>
    /// <remarks>
    /// This constructor establishes the initial size of the backbuffer. Derived classes can override
    /// <see cref="RequestResize"/> to implement dynamic resizing behavior if needed.
    /// </remarks>
    protected BackbufferBase(int width, int height)
        : this()
    {
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Gets the SkiaSharp canvas used for drawing operations on this backbuffer.
    /// </summary>
    /// <value>
    /// An <see cref="SKCanvas"/> instance that provides the drawing surface for rendering operations.
    /// </value>
    /// <remarks>
    /// <para>
    /// Derived classes must implement this property to provide access to the underlying drawing canvas.
    /// All rendering operations, including tiles, sprites, and direct drawing instances, are performed
    /// on this canvas.
    /// </para>
    /// <para>
    /// The canvas should be configured with appropriate coordinate transformations, clipping regions,
    /// and rendering states before drawing operations begin.
    /// </para>
    /// </remarks>
    public abstract SKCanvas Canvas { get; }

    /// <summary>
    /// Creates an immutable snapshot of the current backbuffer contents as an <see cref="SKImage"/>.
    /// </summary>
    /// <returns>
    /// An <see cref="SKImage"/> representing the current state of the backbuffer. The caller is responsible
    /// for disposing this image when no longer needed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Derived classes must implement this method to capture the current backbuffer contents as an image.
    /// This is typically used for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Presenting the backbuffer to the display adapter</description></item>
    /// <item><description>Saving screenshots or exporting rendered content</description></item>
    /// <item><description>Creating texture atlases or sprite sheets from rendered content</description></item>
    /// </list>
    /// <para>
    /// The returned image is a point-in-time snapshot and will not reflect subsequent rendering operations.
    /// </para>
    /// </remarks>
    protected internal abstract SKImage Snapshot();

    /// <summary>
    /// Prepares the backbuffer for a new rendering frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived classes must implement this method to perform any initialization required at the start
    /// of each frame, such as:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Clearing the canvas or specific regions</description></item>
    /// <item><description>Resetting transformation matrices</description></item>
    /// <item><description>Setting up render states</description></item>
    /// <item><description>Initializing performance counters or diagnostics</description></item>
    /// </list>
    /// <para>
    /// This method is called by the rendering system before any drawable content is rendered to the backbuffer.
    /// </para>
    /// </remarks>
    protected internal abstract void BeginFrame();

    /// <summary>
    /// Draws a single tile frame to the backbuffer at the specified screen location.
    /// </summary>
    /// <param name="tile">The <see cref="Tile"/> to render, containing the source graphics and rendering properties.</param>
    /// <param name="destRectScreen">
    /// The destination rectangle in screen pixel coordinates where the tile should be drawn.
    /// This rectangle defines the position and size of the rendered tile on the backbuffer.
    /// </param>
    /// <remarks>
    /// <para>
    /// Derived classes must implement this method to handle the actual rendering of tile graphics
    /// to the canvas. The implementation should:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Extract the appropriate tile frame from the tile's tilesheet</description></item>
    /// <item><description>Apply any tile-specific rendering properties (opacity, transformations, etc.)</description></item>
    /// <item><description>Draw the tile graphics to the destination rectangle</description></item>
    /// <item><description>Handle edge cases such as missing graphics or invalid tile data</description></item>
    /// </list>
    /// <para>
    /// This method is called for each visible tile during the rendering pass and should be optimized
    /// for performance as it may be invoked hundreds or thousands of times per frame.
    /// </para>
    /// </remarks>
    protected internal abstract void DrawTileFrame(Tile tile, RectangleF destRectScreen);

    /// <summary>
    /// Finalizes the current rendering frame and prepares the backbuffer for presentation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived classes must implement this method to perform any cleanup or finalization required
    /// at the end of each frame, such as:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Flushing pending draw operations</description></item>
    /// <item><description>Recording performance metrics</description></item>
    /// <item><description>Preparing the backbuffer for presentation to the display adapter</description></item>
    /// <item><description>Cleaning up temporary rendering resources</description></item>
    /// </list>
    /// <para>
    /// This method is called by the rendering system after all drawable content has been rendered
    /// to the backbuffer and before the backbuffer is presented to the UI adapter.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Requests that the backbuffer be resized to the specified dimensions.
    /// </summary>
    /// <param name="width">The new width in pixels.</param>
    /// <param name="height">The new height in pixels.</param>
    /// <remarks>
    /// <para>
    /// Derived classes can override this method to implement backbuffer resizing behavior, such as
    /// reallocating graphics resources, updating render targets, or adjusting viewport settings.
    /// The base implementation is a no-op.
    /// </para>
    /// <para>
    /// Implementations should call <see cref="UpdateSize"/> after successfully resizing to ensure
    /// the <see cref="Width"/> and <see cref="Height"/> properties reflect the new dimensions and
    /// the <see cref="SizeChanged"/> event is raised.
    /// </para>
    /// <para>
    /// Resizing may be triggered by window resize events, display mode changes, or programmatic
    /// requests from the rendering system.
    /// </para>
    /// </remarks>
    protected internal virtual void RequestResize(int width, int height)
    { /* no-op by default */ }

    /// <summary>
    /// Occurs when the backbuffer dimensions change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This event is raised when <see cref="UpdateSize"/> is called, typically after a successful
    /// resize operation. Subscribers can use this event to respond to size changes by:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Adjusting camera viewports or projection matrices</description></item>
    /// <item><description>Updating UI layout or HUD elements</description></item>
    /// <item><description>Reallocating size-dependent resources</description></item>
    /// <item><description>Triggering a full scene refresh</description></item>
    /// </list>
    /// <para>
    /// The event provides the new width and height as parameters.
    /// </para>
    /// </remarks>
    protected internal event Action<int, int>? SizeChanged;

    /// <summary>
    /// Updates the stored backbuffer dimensions and raises the <see cref="SizeChanged"/> event.
    /// </summary>
    /// <param name="width">The new width in pixels.</param>
    /// <param name="height">The new height in pixels.</param>
    /// <remarks>
    /// <para>
    /// This method should be called by derived classes after successfully resizing the backbuffer
    /// to ensure that the <see cref="Width"/> and <see cref="Height"/> properties are updated
    /// in a thread-safe manner and subscribers are notified of the size change.
    /// </para>
    /// <para>
    /// The method uses volatile writes to ensure visibility across threads and raises the
    /// <see cref="SizeChanged"/> event with the new dimensions.
    /// </para>
    /// </remarks>
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
            var worldPts = tile.OutlinePointsWorld;
            var ptsScreen = new SKPoint[worldPts.Length];

            for (int i = 0; i < worldPts.Length; i++)
            {
                var p = worldPts[i];
                var sp = view.WorldPxToScreenPx(
                    tile.SceneLayer,
                    new PointF(p.X, p.Y)
                );

                ptsScreen[i] = new SKPoint(sp.X, sp.Y);
            }

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
            {
                var colRectScreen = tile.GetCollisionAreaScreen(view).ToSKRect();
                Canvas.DrawRect(colRectScreen, CollisionBoxPaint);
            }
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

        area.Inflate(area.Width, area.Height);

        DirtyRectangle = DirtyRectangle.IsEmpty
            ? area
            : Rectangle.Union(DirtyRectangle, area);
    }

    /// <summary>
    /// Clears the dirty rectangle tracking, resetting it to empty.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method should be called after the backbuffer has been successfully presented to the
    /// UI adapter to reset dirty region tracking for the next frame. The dirty rectangle accumulates
    /// all regions that have been modified during rendering and must be cleared at the start of
    /// each frame or after presentation.
    /// </para>
    /// <para>
    /// Failure to clear the dirty rectangle will result in incorrect dirty region tracking and
    /// potential over-rendering or visual artifacts.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Releases all resources used by the <see cref="BackbufferBase"/> instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method disposes of managed resources including paint objects used for fog effects,
    /// grid lines, collision boxes, and fill operations. Derived classes can override this method
    /// to dispose of additional resources but should call the base implementation to ensure
    /// proper cleanup of base class resources.
    /// </para>
    /// <para>
    /// After disposal, the backbuffer should not be used for further rendering operations.
    /// </para>
    /// </remarks>
    public virtual void Dispose()
    {
        _fillPaint.Dispose();
        FogPaint.Dispose();
        GridLinePaint.Dispose();
        CollisionBoxPaint.Dispose();
    }
}