using System.Runtime.InteropServices;
using SkiaSharp;

namespace Gondwana.Video;

// Decoder callbacks own only this mailbox. Skia and engine state belong to Update/Draw.
internal sealed class VideoFrameMailbox : IDisposable
{
    private readonly object _gate = new();
    private byte[] _pixels = [];
    private int _width, _height;
    private bool _pending, _clear, _disposed;

    internal void Publish(VideoFrameReadyEventArgs frame)
    {
        lock (_gate)
        {
            if (_disposed) return;
            int rowBytes = checked(frame.Width * 4);
            int length = checked(rowBytes * frame.Height);
            if (frame.Width <= 0 || frame.Height <= 0 || frame.Stride < rowBytes || frame.Pixels == IntPtr.Zero)
                throw new ArgumentException("Invalid video frame dimensions, stride, or pointer.", nameof(frame));
            if (_pixels.Length != length) _pixels = new byte[length];
            for (int y = 0; y < frame.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(frame.Pixels, checked(y * frame.Stride)), _pixels, y * rowBytes, rowBytes);
                // RV32's X byte is undefined. Skia raster copies can preserve that byte even
                // with Opaque metadata, so establish real opaque BGRA without swapping RGB.
                for (int x = y * rowBytes + 3; x < (y + 1) * rowBytes; x += 4) _pixels[x] = 255;
            }
            _width = frame.Width;
            _height = frame.Height;
            _pending = true;
        }
    }

    internal void Reset()
    {
        lock (_gate) { _pending = false; _clear = true; }
    }

    // Called only by the engine thread. Holding the gate for the copy bounds storage to
    // one reusable pending buffer and one render-owned bitmap, without per-frame arrays.
    internal bool Consume(ref SKBitmap? bitmap)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            bool changed = _clear;
            if (_clear) { bitmap?.Dispose(); bitmap = null; _clear = false; }
            if (!_pending) return changed;
            if (bitmap is null || bitmap.Width != _width || bitmap.Height != _height)
            {
                bitmap?.Dispose();
                bitmap = new SKBitmap(new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Opaque));
            }
            int rowBytes = checked(_width * 4);
            for (int y = 0; y < _height; y++)
                Marshal.Copy(_pixels, y * rowBytes, IntPtr.Add(bitmap.GetPixels(), y * bitmap.RowBytes), rowBytes);
            bitmap.NotifyPixelsChanged();
            _pending = false;
            return true;
        }
    }

    public void Dispose()
    {
        lock (_gate) { _disposed = true; _pending = false; _pixels = []; }
    }
}
