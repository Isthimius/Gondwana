using System.Drawing;
using SkiaSharp;
using SkiaSharp.Extended.Svg;

namespace Gondwana.Drawing;

/// <summary>
/// Represents a loaded SVG asset that can be rasterized into cached bitmaps at requested sizes.
/// </summary>
public sealed class SvgResource : IDisposable
{
    private readonly SKPicture _picture;
    private readonly object _cacheLock = new();
    private SKBitmap? _cachedBitmap;
    private int _cachedWidth;
    private int _cachedHeight;
    private bool _disposed;

    private SvgResource(SKPicture picture, SizeF intrinsicSize)
    {
        _picture = picture;
        IntrinsicSize = intrinsicSize;
    }

    /// <summary>
    /// Loads an SVG resource from a file path.
    /// </summary>
    /// <param name="path">The SVG file path.</param>
    /// <returns>A loaded <see cref="SvgResource"/> instance.</returns>
    public static SvgResource Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    internal static SvgResource Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var svg = new SKSvg();
        svg.Load(stream);

        if (svg.Picture is null)
            throw new InvalidOperationException("Failed to parse SVG content.");

        var cull = svg.Picture.CullRect;
        var intrinsic = new SizeF(
            cull.Width > 0 ? cull.Width : 1f,
            cull.Height > 0 ? cull.Height : 1f);

        return new SvgResource(svg.Picture, intrinsic);
    }

    /// <summary>
    /// Gets the intrinsic (unscaled) SVG size in pixels.
    /// </summary>
    public SizeF IntrinsicSize { get; }

    /// <summary>
    /// Rasterizes this SVG to an explicit pixel size.
    /// </summary>
    /// <param name="width">Output width in pixels.</param>
    /// <param name="height">Output height in pixels.</param>
    /// <returns>A cached rasterized bitmap for the requested size.</returns>
    public SKBitmap Rasterize(int width, int height)
    {
        ThrowIfDisposed();

        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than 0.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than 0.");

        lock (_cacheLock)
        {
            if (_cachedBitmap is not null && _cachedWidth == width && _cachedHeight == height)
                return _cachedBitmap;

            var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            var src = _picture.CullRect;
            if (src.Width <= 0 || src.Height <= 0)
            {
                src = new SKRect(0, 0, Math.Max(1f, IntrinsicSize.Width), Math.Max(1f, IntrinsicSize.Height));
            }

            canvas.Scale(width / src.Width, height / src.Height);
            canvas.DrawPicture(_picture);
            canvas.Flush();

            _cachedBitmap?.Dispose();
            _cachedBitmap = bitmap;
            _cachedWidth = width;
            _cachedHeight = height;
            return _cachedBitmap;
        }
    }

    /// <summary>
    /// Rasterizes this SVG to its intrinsic size multiplied by a scale factor.
    /// </summary>
    /// <param name="scale">Scale factor where 1.0 maps to intrinsic size.</param>
    /// <returns>A cached rasterized bitmap for the requested scale.</returns>
    public SKBitmap Rasterize(float scale = 1.0f)
    {
        ThrowIfDisposed();

        if (scale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be greater than 0.");

        int width = Math.Max(1, (int)MathF.Round(IntrinsicSize.Width * scale));
        int height = Math.Max(1, (int)MathF.Round(IntrinsicSize.Height * scale));
        return Rasterize(width, height);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_cacheLock)
        {
            _cachedBitmap?.Dispose();
            _picture.Dispose();
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
