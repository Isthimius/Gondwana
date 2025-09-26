using Gondwana.Video;              // IVideoPlayer
using SkiaSharp;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Gondwana.Rendering.Direct;

/// <summary>
/// Draws video frames provided by IVideoPlayer onto the backbuffer.
/// Audio is handled by the IVideoPlayer implementation (e.g., VLC system output on desktop).
/// </summary>
public sealed class DirectVideo : DirectDrawingBase
{
    private readonly IVideoPlayer _player;
    private readonly object _frameLock = new();

    private SKBitmap? _frame;                 // RGBA target
    private int _srcW, _srcH, _srcStride;     // last frame metadata

    // ---- knobs ----
    public StretchMode Stretch { get; set; } = StretchMode.Fill;

    private float _opacity = 1f;              // 0..1
    public float Opacity
    {
        get => _opacity;
        set => _opacity = Math.Clamp(value, 0f, 1f);
    }

    private double _playbackRate = 1.0;
    public double PlaybackRate
    {
        get => _playbackRate;
        set
        {
            _playbackRate = value;
            _player.SetRate(value);
        }
    }

    public bool Loop
    {
        get => _player.Loop;
        set => _player.Loop = value;
    }

    // --- ctors ---

    /// <summary>
    /// Use this when you already have an IVideoPlayer instance (e.g., resolved via DI).
    /// </summary>
    public DirectVideo(
        RenderSurfaceHost<BitmapBackbuffer> surface,
        Rectangle bounds,
        IVideoPlayer player,
        Uri source)
        : base(surface, bounds)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        HookPlayer();
        _player.Open(source);
        _player.Play();
    }

    /// <summary>
    /// Use this when you have a factory that abstracts platform differences.
    /// e.g., desktop/mobile -> VLC impl, web -> HTML5/WebCodecs impl.
    /// </summary>
    public DirectVideo(
        RenderSurfaceHost<BitmapBackbuffer> surface,
        Rectangle bounds,
        Func<IVideoPlayer> playerFactory,
        Uri source)
        : base(surface, bounds)
    {
        if (playerFactory is null) throw new ArgumentNullException(nameof(playerFactory));
        _player = playerFactory();
        HookPlayer();
        _player.Open(source);
        _player.Play();
    }

    private void HookPlayer()
    {
        _player.FrameReady += OnFrameReady;
        _player.Ended += (_, __) => { /* engine could fire a scene event here if needed */ };
    }

    // Change the access modifier of the Render method to match the base class definition.
    protected override void Render()
    {
        SKBitmap? bmp;
        lock (_frameLock) bmp = _frame;
        if (bmp is null) return;

        var canvas = RenderSurfaceHost.Backbuffer.Canvas;
        var dest = ComputeDestRect(Bounds, bmp.Width, bmp.Height, Stretch);

        using var paint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(Opacity * 255)) };
        canvas.DrawBitmap(bmp, dest, paint);
    }

    private static SKRect ComputeDestRect(Rectangle bounds, int srcW, int srcH, StretchMode mode)
    {
        var b = bounds;
        var dst = new SKRect(b.Left, b.Top, b.Right, b.Bottom);
        if (mode == StretchMode.Fill || srcW <= 0 || srcH <= 0) return dst;

        float bw = b.Width, bh = b.Height;
        float arSrc = (float)srcW / srcH;
        float arDst = bw / bh;

        if (mode == StretchMode.None)
            return new SKRect(b.Left, b.Top, b.Left + srcW, b.Top + srcH);

        if (mode == StretchMode.Uniform)
        {
            if (arSrc > arDst)
            {
                float w = bw, h = w / arSrc, y = b.Top + (bh - h) / 2f;
                return new SKRect(b.Left, y, b.Left + w, y + h);
            }
            else
            {
                float h = bh, w = h * arSrc, x = b.Left + (bw - w) / 2f;
                return new SKRect(x, b.Top, x + w, b.Top + h);
            }
        }

        // UniformToFill
        if (arSrc < arDst)
        {
            float w = bw, h = w / arSrc, y = b.Top + (bh - h) / 2f;
            return new SKRect(b.Left, y, b.Left + w, y + h);
        }
        else
        {
            float h = bh, w = h * arSrc, x = b.Left + (bw - w) / 2f;
            return new SKRect(x, b.Top, x + w, b.Top + h);
        }
    }

    // --- frame ingestion ---

    private void OnFrameReady(object? sender, VideoFrameReadyEventArgs e)
    {
        // Copy the RGBA buffer into a reusable SKBitmap.
        // NOTE: do not hold the lock while touching Skia; just protect shared refs/copies.
        SKBitmap? local;
        lock (_frameLock)
        {
            if (_frame is null || _srcW != e.Width || _srcH != e.Height)
            {
                _frame?.Dispose();
                _frame = new SKBitmap(new SKImageInfo(e.Width, e.Height, SKColorType.Rgba8888));
                _srcW = e.Width;
                _srcH = e.Height;
                _srcStride = e.Stride;
            }
            local = _frame;
        }

        if (local is null) return;

        CopyFrameToBitmap(e.Pixels, e.Stride, local, _srcW, _srcH);

        // Mark dirty so the direct-draw manager repaints our area.
        _dirty = true;
        ForceRefresh();
    }

    private static void CopyFrameToBitmap(IntPtr srcPixels, int srcStride, SKBitmap dst, int width, int height)
    {
#if !BROWSER && !NO_UNSAFE
        // Fast path: single memcpy using pointers
        unsafe
        {
            using var pix = dst.PeekPixels();
            var dstPtr = (byte*)pix.GetPixels().ToPointer();
            Buffer.MemoryCopy(
                source: (void*)srcPixels,
                destination: dstPtr,
                destinationSizeInBytes: pix.RowBytes * height,
                sourceBytesToCopy: (long)srcStride * height);
        }
#else
        // Safe path: row-by-row Marshal.Copy (no unsafe)
        using var pix = dst.PeekPixels();
        IntPtr dstBase = pix.GetPixels();
        int dstRB = (int)pix.RowBytes;
        int srcRB = srcStride;
        int rowBytes = Math.Min(dstRB, srcRB);

        var rowBuf = System.Buffers.ArrayPool<byte>.Shared.Rent(rowBytes);
        try
        {
            for (int y = 0; y < height; y++)
            {
                var srcRow = IntPtr.Add(srcPixels, y * srcRB);
                var dstRow = IntPtr.Add(dstBase, y * dstRB);
                Marshal.Copy(srcRow, rowBuf, 0, rowBytes);
                Marshal.Copy(rowBuf, 0, dstRow, rowBytes);
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rowBuf, clearArray: false);
        }
#endif
    }

    // --- control surface (thin passthrough to player) ---

    public void Play() => _player.Play();
    public void Pause() => _player.Pause();
    public void Stop() => _player.Stop();
    public void Seek(TimeSpan position) => _player.Seek(position);

    // --- cleanup ---

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        _player.FrameReady -= OnFrameReady;
        _player.Dispose();

        lock (_frameLock)
        {
            _frame?.Dispose();
            _frame = null;
        }
    }
}
