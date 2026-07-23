namespace Gondwana.Studio.ViewModels;

/// <summary>
/// Represents a single tile cell within a tilesheet editor grid.
/// </summary>
public sealed class TileCellViewModel : ViewModelBase
{
    /// <summary>
    /// Gets the zero-based sequential index of this tile.
    /// </summary>
    public int Index { get; init; }
    /// <summary>
    /// Gets the column position of this tile in the grid.
    /// </summary>
    public int X { get; init; }
    /// <summary>
    /// Gets the row position of this tile in the grid.
    /// </summary>
    public int Y { get; init; }
    /// <summary>
    /// Gets the left pixel offset of this tile within the tilesheet image.
    /// </summary>
    public double Left { get; init; }
    /// <summary>
    /// Gets the top pixel offset of this tile within the tilesheet image.
    /// </summary>
    public double Top { get; init; }
    /// <summary>
    /// Gets the pixel width of this tile.
    /// </summary>
    public double Width { get; init; }
    /// <summary>
    /// Gets the pixel height of this tile.
    /// </summary>
    public double Height { get; init; }

    private string _name = string.Empty;

    /// <summary>
    /// Gets or sets the tile name.
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
}
