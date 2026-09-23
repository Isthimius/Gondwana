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
    internal IntPtr Context { get; private set; }
    private GCHandle _handle;

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
        try
        {
            // LibVLCSharp 3 declares cleanup's void* as ref IntPtr. Use a real native
            // pointer cell, so cleanup receives the handle value while lock/display
            // receive the cell address. Never dereference a GCHandle as native memory.
            Context = Marshal.AllocHGlobal(IntPtr.Size);
            _handle = GCHandle.Alloc(this);
            Marshal.WriteIntPtr(Context, GCHandle.ToIntPtr(_handle));
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        if (Pixels == IntPtr.Zero) return;
        NativeMemory.AlignedFree((void*)Pixels);
        Pixels = IntPtr.Zero;
        if (_handle.IsAllocated) _handle.Free();
        if (Context != IntPtr.Zero) Marshal.FreeHGlobal(Context);
        Context = IntPtr.Zero;
    }
}
