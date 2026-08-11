using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia;
using Gondwana.Tooling.Studio.Avalonia.ViewModels;
using Gondwana.Tooling.Studio.ViewModels;

namespace Gondwana.Tooling.Studio.Avalonia.Views;

public partial class DirectoryPanelView : UserControl
{
    public DirectoryPanelView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DirectoryTree.DoubleTapped += OnTreeDoubleTapped;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DirectoryTree.DoubleTapped -= OnTreeDoubleTapped;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is DirectoryPanelViewModel vm && vm.SelectedNode is { } node)
            vm.ActivateNodeCommand.Execute(node);
    }
}
