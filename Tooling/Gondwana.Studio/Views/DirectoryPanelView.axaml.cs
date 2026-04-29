using Avalonia.Controls;
using Avalonia.Input;
using Gondwana.Studio.ViewModels;

namespace Gondwana.Studio.Views;

public partial class DirectoryPanelView : UserControl
{
    public DirectoryPanelView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DirectoryTree.DoubleTapped += OnTreeDoubleTapped;
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
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
