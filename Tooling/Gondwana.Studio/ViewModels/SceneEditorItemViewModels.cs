using Avalonia;
using Avalonia.Media;

namespace Gondwana.Studio.ViewModels;

/// <summary>
/// ScenePaintedTileViewModel.
/// </summary>
public sealed class ScenePaintedTileViewModel : ViewModelBase
{
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public int GridX { get; set; }
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public int GridY { get; set; }
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public double PixelX { get; set; }
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public double PixelY { get; set; }
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public double Width { get; set; } = 16;
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public double Height { get; set; } = 16;
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public int TileIndex { get; set; }
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public string LayerName { get; set; } = "main";
}

/// <summary>
/// SceneEntityViewModel.
/// </summary>
public sealed class SceneEntityViewModel : ViewModelBase
{
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public string Name { get; set; } = "entity";
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public double X { get; set; }
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public double Y { get; set; }
}

/// <summary>
/// SceneColliderViewModel.
/// </summary>
public sealed class SceneColliderViewModel : ViewModelBase
{
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public Rect Rect { get; set; }
    /// <summary>
    /// Gets or sets OrangeRed.
    /// </summary>
    public Color StrokeColor { get; set; } = Colors.OrangeRed;

    /// <summary>
    /// Gets X.
    /// </summary>
    public double X => Rect.X;
    /// <summary>
    /// Gets Y.
    /// </summary>
    public double Y => Rect.Y;
    /// <summary>
    /// Gets Width.
    /// </summary>
    public double Width => Rect.Width;
    /// <summary>
    /// Gets Height.
    /// </summary>
    public double Height => Rect.Height;
}
