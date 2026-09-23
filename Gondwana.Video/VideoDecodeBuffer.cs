using System.Runtime.InteropServices;

namespace Gondwana.Video;

// One allocation per negotiated format, owned until LibVLC calls format cleanup.
internal sealed unsafe class VideoDecodeBuffer : IDisposable
{
    internal int Width { get; }
    internal int Height { get; }
    internal int Stride { get; }
    internal int Lines { get; }
    internal IntPtr Pixels { get; private set; }
    internal VideoDecodeBuffer(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Height = height;
        Stride = checked((checked(width * 4) + 31) & ~31);
        Lines = checked((height + 31) & ~31);
        int bytes = checked(Stride * Lines);
        Pixels = (IntPtr)NativeMemory.AlignedAlloc((nuint)bytes, 32);
        if (Pixels == IntPtr.Zero) throw new OutOfMemoryException();
        NativeMemory.Clear((void*)Pixels, (nuint)bytes);
    }

    public void Dispose()
    {
        if (Pixels == IntPtr.Zero) return;
        NativeMemory.AlignedFree((void*)Pixels);
        Pixels = IntPtr.Zero;
    }
}
