using System;
using Avalonia.Controls;
using Gondwana.Studio.ViewModels;

namespace Gondwana.Studio.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.DirectoryPanel.NodeActivated += OnDirectoryNodeActivated;
        }
    }

    private void OnDirectoryNodeActivated(object? sender, DirectoryNodeViewModel node)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (node.IsCategory && node.Category == EngineStatePartsCategory.AssetsFiles)
        {
            // Open (or focus) the AssetFiles editor document
            var assetVm = new AssetFilesViewModel(this);
            vm.OpenDocumentTab("AssetFiles", "Asset Files", assetVm);
        }
    }
}
