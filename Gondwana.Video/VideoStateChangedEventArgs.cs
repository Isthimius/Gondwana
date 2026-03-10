namespace Gondwana.Video;

/// <summary>
/// Provides data for video state changed events.
/// </summary>
public sealed class VideoStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the current state of the video player.
    /// </summary>
    public string State { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoStateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="state">The current state of the video player.</param>
    public VideoStateChangedEventArgs(string state) => State = state;
}