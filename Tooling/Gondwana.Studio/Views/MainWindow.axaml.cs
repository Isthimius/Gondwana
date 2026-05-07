using System;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Gondwana.Studio.ViewModels;
using Gondwana.Studio.Extensibility;
using System.IO;

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
            AttachPlugins(vm);
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
            return;
        }

        if (node.Tag is string path)
        {
            OpenByPath(vm, path);
        }
    }

    private async void OnOpenProjectClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Project Folder",
            AllowMultiple = false
        });

        if (folders.Count == 0)
            return;

        var projectPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(projectPath))
            return;

        vm.SetProject(projectPath);
        RegisterProjectFiles(vm, projectPath);
    }

    private void OnCloseProjectClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.CloseProject();
    }

    private void OnNewTilesheetClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var editor = new TilesheetEditorViewModel(this);
        vm.OpenDocumentTab($"Tilesheet:{Guid.NewGuid()}", "Tilesheet Editor", editor);
    }

    private void OnNewAnimationClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var editor = new AnimationEditorViewModel(this);
        vm.OpenDocumentTab($"Animation:{Guid.NewGuid()}", "Animation Editor", editor);
    }

    private void OnNewSceneClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var editor = new SceneEditorViewModel(this);
        vm.OpenDocumentTab($"Scene:{Guid.NewGuid()}", "Scene Editor", editor);
    }

    private async void OnOpenTilesheetClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await OpenTypedFileAsync("*.gondwana-tilesheet");
    }

    private async void OnOpenAnimationClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await OpenTypedFileAsync("*.gondwana-animation");
    }

    private async void OnOpenSceneClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await OpenTypedFileAsync("*.gondwana-scene");
    }

    private async Task OpenTypedFileAsync(string pattern)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Gondwana Asset") { Patterns = [pattern] }]
        });

        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            OpenByPath(vm, path);
    }

    private void OpenByPath(MainWindowViewModel vm, string path)
    {
        if (path.EndsWith(".gondwana-tilesheet", StringComparison.OrdinalIgnoreCase))
        {
            var editor = new TilesheetEditorViewModel(this);
            editor.LoadMetadata(path);
            vm.OpenDocumentTab($"Tilesheet:{path}", Path.GetFileName(path), editor);
        }
        else if (path.EndsWith(".gondwana-animation", StringComparison.OrdinalIgnoreCase))
        {
            var editor = new AnimationEditorViewModel(this);
            editor.LoadAnimation(path);
            vm.OpenDocumentTab($"Animation:{path}", Path.GetFileName(path), editor);
        }
        else if (path.EndsWith(".gondwana-scene", StringComparison.OrdinalIgnoreCase))
        {
            var editor = new SceneEditorViewModel(this);
            editor.LoadScene(path);
            vm.OpenDocumentTab($"Scene:{path}", Path.GetFileName(path), editor);
        }
    }

    private void RegisterProjectFiles(MainWindowViewModel vm, string projectPath)
    {
        foreach (var node in vm.DirectoryPanel.RootNodes)
            node.Children.Clear();

        foreach (var file in Directory.EnumerateFiles(projectPath, "*.gondwana-tilesheet", SearchOption.AllDirectories))
            vm.DirectoryPanel.AddEntry(EngineStatePartsCategory.Tilesheets, Path.GetFileName(file), file);

        foreach (var file in Directory.EnumerateFiles(projectPath, "*.gondwana-animation", SearchOption.AllDirectories))
            vm.DirectoryPanel.AddEntry(EngineStatePartsCategory.Cycles, Path.GetFileName(file), file);

        foreach (var file in Directory.EnumerateFiles(projectPath, "*.gondwana-scene", SearchOption.AllDirectories))
            vm.DirectoryPanel.AddEntry(EngineStatePartsCategory.Scenes, Path.GetFileName(file), file);
    }

    private void AttachPlugins(MainWindowViewModel vm)
    {
        PluginsMenu.Items.Clear();
        foreach (var item in vm.PluginHost.GetPluginMenuItems())
            PluginsMenu.Items.Add(item);

        foreach (var (pluginName, panel) in vm.PluginHost.GetPluginPanels())
            vm.OpenDocumentTab($"Plugin:{pluginName}", pluginName, panel);
    }
}
