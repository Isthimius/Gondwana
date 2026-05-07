using Avalonia;
using Avalonia.Media;

namespace Gondwana.Studio.ViewModels;

public sealed class ScenePaintedTileViewModel : ViewModelBase
{
    public int GridX { get; set; }
    public int GridY { get; set; }
    public double PixelX { get; set; }
    public double PixelY { get; set; }
    public double Width { get; set; } = 16;
    public double Height { get; set; } = 16;
    public int TileIndex { get; set; }
    public string LayerName { get; set; } = "main";
}

public sealed class SceneEntityViewModel : ViewModelBase
{
    public string Name { get; set; } = "entity";
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class SceneColliderViewModel : ViewModelBase
{
    public Rect Rect { get; set; }
    public Color StrokeColor { get; set; } = Colors.OrangeRed;
}
