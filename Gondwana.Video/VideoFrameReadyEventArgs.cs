namespace Gondwana.Video;

public sealed class VideoFrameReadyEventArgs : EventArgs
{
    public IntPtr Pixels { get; } // pinned RGBA buffer
    public int Width { get; }
    public int Height { get; }
    public int Stride { get; }
    public long Pts100ns { get; } // raw 100‑ns PTS for precise sync

    public VideoFrameReadyEventArgs(IntPtr pixels, int width, int height, int stride, long pts100ns)
    {
        Pixels = pixels; Width = width; Height = height; Stride = stride; Pts100ns = pts100ns;
    }
}
