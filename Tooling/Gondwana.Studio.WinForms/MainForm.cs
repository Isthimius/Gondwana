using System.IO;
using Gondwana.Tooling.Studio.ViewModels;
using Gondwana.Tooling.Studio.WinForms.Extensibility;
using Gondwana.Tooling.Studio.WinForms.Panels;
using Gondwana.Tooling.Studio.WinForms.Services;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace Gondwana.Tooling.Studio.WinForms;

/// <summary>
/// Main application window with DockPanelSuite layout and VS dark theme.
/// </summary>
public sealed class MainForm : Form
{
    private static readonly System.Drawing.Color DarkBackground = System.Drawing.Color.FromArgb(37, 37, 38);
    private static readonly System.Drawing.Color DarkSurface = System.Drawing.Color.FromArgb(30, 30, 30);
    private static readonly System.Drawing.Color DarkForeground = System.Drawing.Color.FromArgb(220, 220, 220);

    private readonly WinFormsDialogService _dialogService;
    private readonly StudioPluginHost _pluginHost;
    private readonly DirectoryPanelViewModel _directoryVm;
    private readonly OutputViewModel _outputVm;
    private readonly DockPanel _dockPanel;
    private readonly VS2015DarkTheme _dockTheme;

    private readonly DirectoryPanel _directoryPanel;
    private readonly OutputPanel _outputPanel;

    // Track open documents by key to avoid duplicates
    private readonly Dictionary<string, DockContent> _openDocuments = [];

    /// <summary>
    /// MainForm.
    /// </summary>
    public MainForm()
    {
        _directoryVm = new DirectoryPanelViewModel();
        _outputVm = new OutputViewModel();
        _dialogService = new WinFormsDialogService(this);
        _pluginHost = new StudioPluginHost(msg => _outputVm.Log(msg));
        _dockTheme = new VS2015DarkTheme();

        Text = "Gondwana Studio";
        Width = 1280;
        Height = 800;
        MinimumSize = new System.Drawing.Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = DarkBackground;
        ForeColor = DarkForeground;

        var menuStrip = BuildMenuStrip();

        _directoryPanel = new DirectoryPanel(_directoryVm) { Dock = DockStyle.Fill };
        _directoryPanel.NodeActivated += OnNodeActivated;
        ApplyStudioTheme(_directoryPanel);
        _outputPanel = new OutputPanel(_outputVm) { Dock = DockStyle.Fill };
        ApplyStudioTheme(_outputPanel);

        _dockPanel = new DockPanel
        {
            Dock = DockStyle.Fill,
            BackColor = DarkBackground,
            Theme = _dockTheme
        };

        Controls.Add(_dockPanel);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;

        ShowToolWindows();

        _outputVm.Log("Gondwana Studio WinForms ready.");

        _pluginHost.DiscoverAndLoad();
        AttachPlugins();
    }

    private MenuStrip BuildMenuStrip()
    {
        var menuStrip = new MenuStrip { BackColor = DarkBackground, ForeColor = DarkForeground, Renderer = new DarkMenuRenderer() };

        var fileMenu = new ToolStripMenuItem("&File");
        var openProjectItem = new ToolStripMenuItem("&Open Project…");
        var closeProjectItem = new ToolStripMenuItem("&Close Project");
        var newMenu = new ToolStripMenuItem("&New");
        var newTilesheetItem = new ToolStripMenuItem("Tilesheet Editor");
        var newAnimationItem = new ToolStripMenuItem("Animation Editor");
        var newSceneItem = new ToolStripMenuItem("Scene Editor");
        newMenu.DropDownItems.AddRange([newTilesheetItem, newAnimationItem, newSceneItem]);
        var openMenu = new ToolStripMenuItem("&Open");
        var openTilesheetItem = new ToolStripMenuItem("Tilesheet…");
        var openAnimationItem = new ToolStripMenuItem("Animation…");
        var openSceneItem = new ToolStripMenuItem("Scene…");
        openMenu.DropDownItems.AddRange([openTilesheetItem, openAnimationItem, openSceneItem]);
        var exitItem = new ToolStripMenuItem("E&xit");

        openProjectItem.Click += OnOpenProjectClicked;
        closeProjectItem.Click += OnCloseProjectClicked;
        newTilesheetItem.Click += OnNewTilesheetClicked;
        newAnimationItem.Click += OnNewAnimationClicked;
        newSceneItem.Click += OnNewSceneClicked;
        openTilesheetItem.Click += async (_, _) => await OpenTypedFileFromMenuAsync("*.gts", "*.gondwana-tilesheet");
        openAnimationItem.Click += async (_, _) => await OpenTypedFileFromMenuAsync("*.gondwana-animation");
        openSceneItem.Click += async (_, _) => await OpenTypedFileFromMenuAsync("*.gondwana-scene");
        exitItem.Click += (_, _) => Close();

        fileMenu.DropDownItems.AddRange([
            openProjectItem, closeProjectItem,
            new ToolStripSeparator(),
            newMenu, openMenu,
            new ToolStripSeparator(),
            exitItem
        ]);

        var pluginsMenu = new ToolStripMenuItem("&Plugins") { Name = "PluginsMenu" };

        menuStrip.Items.AddRange([fileMenu, pluginsMenu]);
        return menuStrip;
    }

    // ------------------------------------------------------------------ Event handlers

    private void OnOpenProjectClicked(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "Select Project Folder", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        RegisterProjectFiles(dialog.SelectedPath);
        _outputVm.Log($"Opened project: {dialog.SelectedPath}");
    }

    private void OnCloseProjectClicked(object? sender, EventArgs e)
    {
        foreach (var node in _directoryVm.RootNodes)
            node.Children.Clear();
        _outputVm.Log("Project closed.");
    }

    private void OnNewTilesheetClicked(object? sender, EventArgs e) =>
        OpenDocumentPanel($"Tilesheet:{Guid.NewGuid()}", "Tilesheet Editor",
            new TilesheetEditorPanel(new TilesheetEditorViewModelBase(_dialogService)));

    private void OnNewAnimationClicked(object? sender, EventArgs e) =>
        OpenDocumentPanel($"Animation:{Guid.NewGuid()}", "Animation Editor",
            new AnimationEditorPanel(new AnimationEditorViewModel(_dialogService)));

    private void OnNewSceneClicked(object? sender, EventArgs e) =>
        OpenDocumentPanel($"Scene:{Guid.NewGuid()}", "Scene Editor",
            new SceneEditorPanel(new SceneEditorViewModel(_dialogService)));

    private async Task OpenTypedFileAsync(params string[] patterns)
    {
        var path = await _dialogService.OpenFileAsync("Open File", patterns);
        if (!string.IsNullOrWhiteSpace(path))
            OpenByPath(path);
    }

    private async Task OpenTypedFileFromMenuAsync(params string[] patterns)
    {
        try
        {
            await OpenTypedFileAsync(patterns);
        }
        catch (Exception ex)
        {
            _outputVm.Log($"Failed to open file: {ex.Message}");
        }
    }

    private void OnNodeActivated(object? sender, DirectoryNodeViewModel node)
    {
        if (node.IsCategory && node.Category == EngineStatePartsCategory.AssetsFiles)
        {
            OpenDocumentPanel($"AssetFiles:{Guid.NewGuid()}", "Asset Files",
                new AssetFilesPanel(new AssetFilesViewModel(_dialogService)));
            return;
        }

        if (node.Tag is string path)
            OpenByPath(path);
    }

    // ------------------------------------------------------------------ Helpers

    private void OpenByPath(string path)
    {
        if (_openDocuments.TryGetValue(path, out var existing))
        {
            existing.Activate();
            return;
        }

        if (path.EndsWith(".gts", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".gondwana-tilesheet", StringComparison.OrdinalIgnoreCase))
        {
            var vm = new TilesheetEditorViewModelBase(_dialogService);
            vm.LoadMetadata(path);
            OpenDocumentPanel(path, Path.GetFileName(path), new TilesheetEditorPanel(vm));
        }
        else if (path.EndsWith(".gondwana-animation", StringComparison.OrdinalIgnoreCase))
        {
            var vm = new AnimationEditorViewModel(_dialogService);
            vm.LoadAnimation(path);
            OpenDocumentPanel(path, Path.GetFileName(path), new AnimationEditorPanel(vm));
        }
        else if (path.EndsWith(".gondwana-scene", StringComparison.OrdinalIgnoreCase))
        {
            var vm = new SceneEditorViewModel(_dialogService);
            vm.LoadScene(path);
            OpenDocumentPanel(path, Path.GetFileName(path), new SceneEditorPanel(vm));
        }
        else if (path.EndsWith(".gaf", StringComparison.OrdinalIgnoreCase)
                 || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var vm = new AssetFilesViewModel(_dialogService);
            OpenDocumentPanel(path, Path.GetFileName(path), new AssetFilesPanel(vm));
        }
    }

    private void RegisterProjectFiles(string projectPath)
    {
        foreach (var node in _directoryVm.RootNodes)
            node.Children.Clear();

        foreach (var file in Directory.EnumerateFiles(projectPath, "*.gondwana-tilesheet", SearchOption.AllDirectories))
            _directoryVm.AddEntry(EngineStatePartsCategory.Tilesheets, Path.GetFileName(file), file);
        foreach (var file in Directory.EnumerateFiles(projectPath, "*.gts", SearchOption.AllDirectories))
            _directoryVm.AddEntry(EngineStatePartsCategory.Tilesheets, Path.GetFileName(file), file);

        foreach (var file in Directory.EnumerateFiles(projectPath, "*.gondwana-animation", SearchOption.AllDirectories))
            _directoryVm.AddEntry(EngineStatePartsCategory.Cycles, Path.GetFileName(file), file);

        foreach (var file in Directory.EnumerateFiles(projectPath, "*.gondwana-scene", SearchOption.AllDirectories))
            _directoryVm.AddEntry(EngineStatePartsCategory.Scenes, Path.GetFileName(file), file);
    }

    private void OpenDocumentPanel(string key, string title, Control content)
    {
        if (_openDocuments.TryGetValue(key, out var existing))
        {
            existing.Activate();
            return;
        }

        ApplyStudioTheme(content);

        var doc = new StudioDockContent(title, content, closeable: true, onClosed: () => _openDocuments.Remove(key))
        {
            DockAreas = DockAreas.Document | DockAreas.Float
        };
        _openDocuments[key] = doc;
        doc.Show(_dockPanel, DockState.Document);
        doc.Activate();
    }

    private void AttachPlugins()
    {
        if (MainMenuStrip?.Items["PluginsMenu"] is not ToolStripMenuItem pluginsMenu)
            return;

        pluginsMenu.DropDownItems.Clear();
        foreach (var item in _pluginHost.GetPluginMenuItems())
            pluginsMenu.DropDownItems.Add(item);

        foreach (var (pluginName, control) in _pluginHost.GetPluginPanels())
            OpenDocumentPanel($"Plugin:{pluginName}", pluginName, control);
    }

    private void ShowToolWindows()
    {
        var directoryWindow = new StudioDockContent("Directory", _directoryPanel, closeable: false)
        {
            DockAreas = DockAreas.DockLeft | DockAreas.Float
        };
        directoryWindow.Show(_dockPanel, DockState.DockLeft);

        var outputWindow = new StudioDockContent("Output", _outputPanel, closeable: false)
        {
            DockAreas = DockAreas.DockBottom | DockAreas.Float
        };
        outputWindow.Show(_dockPanel, DockState.DockBottom);
    }

    private void ApplyStudioTheme(Control control)
    {
        ApplyDarkColors(control);
        ApplyDockTheme(control);
    }

    private void ApplyDockTheme(Control control)
    {
        if (control is ToolStrip toolStrip)
            _dockTheme.ApplyTo(toolStrip);

        if (control.ContextMenuStrip is { } contextMenuStrip)
            _dockTheme.ApplyTo(contextMenuStrip);

        foreach (Control child in control.Controls)
            ApplyDockTheme(child);
    }

    private static void ApplyDarkColors(Control control)
    {
        if (control.BackColor == default || control.BackColor == SystemColors.Control)
            control.BackColor = DarkSurface;

        if (control.ForeColor == default || control.ForeColor == SystemColors.ControlText)
            control.ForeColor = DarkForeground;

        foreach (Control child in control.Controls)
            ApplyDarkColors(child);
    }
}

file sealed class StudioDockContent : DockContent
{
    private readonly Action? _onClosed;

    public StudioDockContent(string title, Control content, bool closeable, Action? onClosed = null)
    {
        Text = title;
        CloseButton = closeable;
        CloseButtonVisible = closeable;
        _onClosed = onClosed;

        content.Dock = DockStyle.Fill;
        Controls.Add(content);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _onClosed?.Invoke();
        base.OnFormClosed(e);
    }
}

/// <summary>
/// Custom ToolStrip/MenuStrip renderer that applies dark colors.
/// </summary>
file sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly System.Drawing.Color DarkBg = System.Drawing.Color.FromArgb(37, 37, 38);
    private static readonly System.Drawing.Color DarkHighlight = System.Drawing.Color.FromArgb(62, 62, 64);

    public DarkMenuRenderer() : base(new DarkColorTable())
    {
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var g = e.Graphics;
        var bounds = new System.Drawing.Rectangle(0, 0, e.Item.Width, e.Item.Height);
        using var brush = new System.Drawing.SolidBrush(e.Item.Selected ? DarkHighlight : DarkBg);
        g.FillRectangle(brush, bounds);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = System.Drawing.Color.FromArgb(220, 220, 220);
        base.OnRenderItemText(e);
    }
}

/// <summary>
/// Professional color table for dark menu styling.
/// </summary>
file sealed class DarkColorTable : ProfessionalColorTable
{
    private static readonly System.Drawing.Color DarkBg = System.Drawing.Color.FromArgb(37, 37, 38);

    public override System.Drawing.Color MenuStripGradientBegin => DarkBg;
    public override System.Drawing.Color MenuStripGradientEnd => DarkBg;
    public override System.Drawing.Color ToolStripDropDownBackground => DarkBg;
    public override System.Drawing.Color ImageMarginGradientBegin => DarkBg;
    public override System.Drawing.Color ImageMarginGradientMiddle => DarkBg;
    public override System.Drawing.Color ImageMarginGradientEnd => DarkBg;
    public override System.Drawing.Color MenuBorder => System.Drawing.Color.FromArgb(60, 60, 60);
    public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.FromArgb(60, 60, 60);
}
