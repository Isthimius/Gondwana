using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Gondwana.Tooling.Studio.ViewModels;

/// <summary>
/// View-model for the Directory panel that shows all EngineStateParts categories
/// as a tree, each with zero or more child entries.
/// </summary>
public partial class DirectoryPanelViewModel : ViewModelBase
{
    public ObservableCollection<DirectoryNodeViewModel> RootNodes { get; } = new();

    [ObservableProperty]
    private DirectoryNodeViewModel? _selectedNode;

    public event EventHandler<DirectoryNodeViewModel>? NodeActivated;

    public DirectoryPanelViewModel()
    {
        BuildTree();
    }

    private void BuildTree()
    {
        foreach (EngineStatePartsCategory category in Enum.GetValues<EngineStatePartsCategory>())
        {
            var node = new DirectoryNodeViewModel
            {
                DisplayName = category.ToString(),
                Category = category,
                IsCategory = true,
                IsExpanded = true
            };
            RootNodes.Add(node);
        }
    }

    /// <summary>
    /// Adds an entry child node to the given category.
    /// </summary>
    public DirectoryNodeViewModel AddEntry(EngineStatePartsCategory category, string displayName, object? tag = null)
    {
        var parent = FindCategoryNode(category);
        if (parent is null)
            throw new InvalidOperationException($"Category node '{category}' not found.");

        var child = new DirectoryNodeViewModel
        {
            DisplayName = displayName,
            Category = category,
            IsCategory = false,
            Tag = tag
        };
        parent.Children.Add(child);
        parent.IsExpanded = true;
        return child;
    }

    /// <summary>
    /// Removes a previously added entry node.
    /// </summary>
    public void RemoveEntry(DirectoryNodeViewModel node)
    {
        var parent = FindCategoryNode(node.Category);
        parent?.Children.Remove(node);
    }

    private DirectoryNodeViewModel? FindCategoryNode(EngineStatePartsCategory category)
    {
        foreach (var node in RootNodes)
        {
            if (node.IsCategory && node.Category == category)
                return node;
        }
        return null;
    }

    [RelayCommand]
    private void ActivateNode(DirectoryNodeViewModel? node)
    {
        if (node is null)
            return;

        SelectedNode = node;
        NodeActivated?.Invoke(this, node);
    }
}
