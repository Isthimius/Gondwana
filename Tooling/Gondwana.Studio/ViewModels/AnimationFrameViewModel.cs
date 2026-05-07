namespace Gondwana.Studio.ViewModels;

public sealed class AnimationFrameViewModel : ViewModelBase
{
    public int TileIndex { get; set; }
    public string TileName { get; set; } = string.Empty;
    public int DurationMs { get; set; } = 100;
}
