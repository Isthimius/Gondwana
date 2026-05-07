using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Gondwana.Studio.ViewModels;

namespace Gondwana.Studio.Views;

public partial class TilesheetEditorView : UserControl
{
    public TilesheetEditorView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnTileCellClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TilesheetEditorViewModel vm)
            return;

        if (sender is Button { Tag: TileCellViewModel tile })
            vm.SelectedTile = tile;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not TilesheetEditorViewModel vm)
            return;

        if (!e.Data.Contains(DataFormats.Files))
            return;

        var files = e.Data.GetFiles();
        var path = (files?.FirstOrDefault() as IStorageFile)?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            vm.LoadImage(path);
    }
}
