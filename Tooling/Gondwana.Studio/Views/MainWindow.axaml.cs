using System;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using global::Avalonia.Interactivity;
using Gondwana.Tooling.Studio.Avalonia.Services;
using Gondwana.Tooling.Studio.Avalonia.ViewModels;
using Gondwana.Tooling.Studio.Avalonia.Extensibility;
using Gondwana.Tooling.StudioAssets;
using Gondwana.Tooling.Studio.Core;
using System.IO;
using Gondwana.Tooling.Studio.ViewModels;

namespace Gondwana.Tooling.Studio.Avalonia.Views;

/// <summary>
/// MainWindow.
/// </summary>
public partial class MainWindow : Window
{
    private AvaloniaDialogService? _dialogService;

    /// <summary>
    /// MainWindow.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// OnDataContextChanged.
    /// </summary>
    /// <param name="e">e.</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            _dialogService = new AvaloniaDialogService(this);
            vm.DirectoryPanel.NodeActivated += OnDirectoryNodeActivated;
            AttachPlugins(vm);
        }
    }

    private void OnDirectoryNodeActivated(object? sender, DirectoryNodeViewModel node)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (node.IsCategory && node.Category == EngineStatePartsCategory.AssetsFiles)
        {
            var assetVm = new AssetFilesViewModel(_dialogService!);
            vm.OpenDocumentTab("AssetFiles", "Asset Files", assetVm);
            return;
        }

        if (node.Tag is string path)
        {
            OpenByPath(vm, path);
        }
    }

    private async void OnOpenProjectClicked(object? sender, RoutedEventArgs e)
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

    private void OnCloseProjectClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.CloseProject();
    }

    private void OnNewTilesheetClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var editor = new TilesheetEditorViewModel(_dialogService!);
        vm.OpenDocumentTab($"Tilesheet:{Guid.NewGuid()}", "Tilesheet Editor", editor);
    }

    private void OnNewAnimationClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var editor = new AnimationEditorViewModel(_dialogService!);
        vm.OpenDocumentTab($"Animation:{Guid.NewGuid()}", "Animation Editor", editor);
    }

    private void OnNewSceneClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var editor = new SceneEditorViewModel(_dialogService!);
        vm.OpenDocumentTab($"Scene:{Guid.NewGuid()}", "Scene Editor", editor);
    }

    private async void OnOpenTilesheetClicked(object? sender, RoutedEventArgs e)
    {
        await OpenTypedFileAsync("*.gondwana-tilesheet");
    }

    private async void OnOpenAnimationClicked(object? sender, RoutedEventArgs e)
    {
        await OpenTypedFileAsync("*.gondwana-animation");
    }

    private async void OnOpenSceneClicked(object? sender, RoutedEventArgs e)
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
            var editor = new TilesheetEditorViewModel(_dialogService!);
            editor.LoadMetadata(path);
            vm.OpenDocumentTab($"Tilesheet:{path}", Path.GetFileName(path), editor);
        }
        else if (path.EndsWith(".gondwana-animation", StringComparison.OrdinalIgnoreCase))
        {
            var editor = new AnimationEditorViewModel(_dialogService!);
            editor.LoadAnimation(path);
            vm.OpenDocumentTab($"Animation:{path}", Path.GetFileName(path), editor);
        }
        else if (path.EndsWith(".gondwana-scene", StringComparison.OrdinalIgnoreCase))
        {
            var editor = new SceneEditorViewModel(_dialogService!);
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
