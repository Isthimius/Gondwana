using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Gondwana.Studio.ViewModels;

/// <summary>
/// Represents a single node in the Directory panel tree.
/// Each EngineStateParts category is a top-level node, and each
/// loaded resource under that category is a child node.
/// </summary>
public partial class DirectoryNodeViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>The backing data object this node represents (e.g. an AssetsFile path).</summary>
    public object? Tag { get; init; }

    /// <summary>The EngineStateParts category this node belongs to.</summary>
    public EngineStatePartsCategory Category { get; init; }

    /// <summary>Whether this is a root/category node (true) or an entry node (false).</summary>
    public bool IsCategory { get; init; }

    public ObservableCollection<DirectoryNodeViewModel> Children { get; } = new();
}
