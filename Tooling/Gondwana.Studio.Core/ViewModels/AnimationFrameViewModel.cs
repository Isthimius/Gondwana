namespace Gondwana.Studio.ViewModels;

/// <summary>
/// Represents a single frame within an animation sequence.
/// </summary>
public sealed class AnimationFrameViewModel : ViewModelBase
{
    /// <summary>
    /// Gets or sets the zero-based tile index used to render this frame.
    /// </summary>
    public int TileIndex { get; set; }
    /// <summary>
    /// Gets or sets the display name of the tile used by this frame.
    /// </summary>
    public string TileName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the duration this frame is displayed, in milliseconds.
    /// </summary>
    public int DurationMs { get; set; } = 100;
}
