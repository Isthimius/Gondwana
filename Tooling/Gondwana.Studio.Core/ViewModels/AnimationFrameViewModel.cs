namespace Gondwana.Studio.ViewModels;

/// <summary>
/// AnimationFrameViewModel.
/// </summary>
public sealed class AnimationFrameViewModel : ViewModelBase
{
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public int TileIndex { get; set; }
    /// <summary>
    /// Gets or sets Empty.
    /// </summary>
    public string TileName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public int DurationMs { get; set; } = 100;
}
