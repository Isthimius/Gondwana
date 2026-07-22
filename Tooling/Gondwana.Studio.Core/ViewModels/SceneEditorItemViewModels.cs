using Gondwana.Studio.Core.Geometry;

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
    private RectD _rect;

    /// <summary>
    /// Gets or sets the collider bounds.
    /// </summary>
    public RectD Rect
    {
        get => _rect;
        set
        {
            _rect = value;
            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Y));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
        }
    }

    /// <summary>Gets X.</summary>
    public double X => _rect.X;
    /// <summary>Gets Y.</summary>
    public double Y => _rect.Y;
    /// <summary>Gets Width.</summary>
    public double Width => _rect.Width;
    /// <summary>Gets Height.</summary>
    public double Height => _rect.Height;
}
