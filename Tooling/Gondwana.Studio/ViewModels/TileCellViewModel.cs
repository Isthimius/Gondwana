namespace Gondwana.Studio.ViewModels;

public sealed class TileCellViewModel : ViewModelBase
{
    public int Index { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public double Left { get; init; }
    public double Top { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
}
