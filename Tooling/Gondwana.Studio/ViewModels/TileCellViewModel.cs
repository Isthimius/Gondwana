namespace Gondwana.Studio.ViewModels;

/// <summary>
/// TileCellViewModel.
/// </summary>
public sealed class TileCellViewModel : ViewModelBase
{
    /// <summary>
    /// Gets or sets init.
    /// </summary>
    public int Index { get; init; }
    /// <summary>
    /// Gets or sets init.
    /// </summary>
    public int X { get; init; }
    /// <summary>
    /// Gets or sets init.
    /// </summary>
    public int Y { get; init; }
    /// <summary>
    /// Gets or sets init.
    /// </summary>
    public double Left { get; init; }
    /// <summary>
    /// Gets or sets init.
    /// </summary>
    public double Top { get; init; }
    /// <summary>
    /// Gets or sets init.
    /// </summary>
    public double Width { get; init; }
    /// <summary>
    /// Gets or sets init.
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
