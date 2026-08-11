using Gondwana.Tooling.Studio.Core.Geometry;

namespace Gondwana.Tooling.Studio.ViewModels;

/// <summary>
/// Represents a tile placed on the scene canvas at a specific grid position.
/// </summary>
public sealed class ScenePaintedTileViewModel : ViewModelBase
{
    /// <summary>
    /// Gets or sets the grid column of this painted tile.
    /// </summary>
    public int GridX { get; set; }
    /// <summary>
    /// Gets or sets the grid row of this painted tile.
    /// </summary>
    public int GridY { get; set; }
    /// <summary>
    /// Gets or sets the pixel X position of this tile on the scene canvas.
    /// </summary>
    public double PixelX { get; set; }
    /// <summary>
    /// Gets or sets the pixel Y position of this tile on the scene canvas.
    /// </summary>
    public double PixelY { get; set; }
    /// <summary>
    /// Gets or sets the display width of this tile in pixels.
    /// </summary>
    public double Width { get; set; } = 16;
    /// <summary>
    /// Gets or sets the display height of this tile in pixels.
    /// </summary>
    public double Height { get; set; } = 16;
    /// <summary>
    /// Gets or sets the tile index into the tilesheet palette.
    /// </summary>
    public int TileIndex { get; set; }
    /// <summary>
    /// Gets or sets the name of the scene layer this tile belongs to.
    /// </summary>
    public string LayerName { get; set; } = "main";
}

/// <summary>
/// Represents a placed entity on the scene.
/// </summary>
public sealed class SceneEntityViewModel : ViewModelBase
{
    /// <summary>
    /// Gets or sets the display name of this entity.
    /// </summary>
    public string Name { get; set; } = "entity";
    /// <summary>
    /// Gets or sets the world X position of this entity.
    /// </summary>
    public double X { get; set; }
    /// <summary>
    /// Gets or sets the world Y position of this entity.
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
