namespace Gondwana.Video;

/// <summary>
/// Provides data for video frame ready events, containing frame buffer information and timing data.
/// </summary>
public sealed class VideoFrameReadyEventArgs : EventArgs
{
    /// <summary>
    /// Gets a pointer to the pinned RGBA pixel buffer.
    /// </summary>
    public IntPtr Pixels { get; } // pinned RGBA buffer
    
    /// <summary>
    /// Gets the width of the video frame in pixels.
    /// </summary>
    public int Width { get; }
    
    /// <summary>
    /// Gets the height of the video frame in pixels.
    /// </summary>
    public int Height { get; }
    
    /// <summary>
    /// Gets the stride (bytes per row) of the pixel buffer.
    /// </summary>
    public int Stride { get; }
    
    /// <summary>
    /// Gets the presentation timestamp in 100-nanosecond units for precise synchronization.
    /// </summary>
    public long Pts100ns { get; } // raw 100‑ns PTS for precise sync

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoFrameReadyEventArgs"/> class.
    /// </summary>
    /// <param name="pixels">A pointer to the pinned RGBA pixel buffer.</param>
    /// <param name="width">The width of the video frame in pixels.</param>
    /// <param name="height">The height of the video frame in pixels.</param>
    /// <param name="stride">The stride (bytes per row) of the pixel buffer.</param>
    /// <param name="pts100ns">The presentation timestamp in 100-nanosecond units.</param>
    public VideoFrameReadyEventArgs(IntPtr pixels, int width, int height, int stride, long pts100ns)
    {
        Pixels = pixels; Width = width; Height = height; Stride = stride; Pts100ns = pts100ns;
    }
}