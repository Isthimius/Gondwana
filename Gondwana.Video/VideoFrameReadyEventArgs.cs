namespace Gondwana.Video;

/// <summary>
/// Provides data for video frame ready events, containing frame buffer information and timing data.
/// </summary>
public sealed class VideoFrameReadyEventArgs : EventArgs
{
    /// <summary>
    /// Gets a pointer to the BGRX pixel buffer (8 bits per channel; the fourth byte is unused).
    /// </summary>
    public IntPtr Pixels { get; } // Valid only for the duration of FrameReady; consumers must copy.
    
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
    /// Gets an optional presentation timestamp in 100-nanosecond units; zero means unavailable.
    /// </summary>
    public long Pts100ns { get; } // LibVLC vmem does not supply a frame PTS.

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoFrameReadyEventArgs"/> class.
    /// </summary>
    /// <param name="pixels">A pointer to the BGRX pixel buffer (8 bits per channel; the fourth byte is unused).</param>
    /// <param name="width">The width of the video frame in pixels.</param>
    /// <param name="height">The height of the video frame in pixels.</param>
    /// <param name="stride">The stride (bytes per row) of the pixel buffer.</param>
    /// <param name="pts100ns">The presentation timestamp in 100-nanosecond units.</param>
    public VideoFrameReadyEventArgs(IntPtr pixels, int width, int height, int stride, long pts100ns)
    {
        Pixels = pixels; Width = width; Height = height; Stride = stride; Pts100ns = pts100ns;
    }
}
