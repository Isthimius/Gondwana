using System.IO;
using Gondwana.Studio.ViewModels;
using Gondwana.Studio.WinForms.Extensibility;
using Gondwana.Studio.WinForms.Panels;
using Gondwana.Studio.WinForms.Services;

namespace Gondwana.Studio.WinForms;

/// <summary>
/// Main application window with dark-themed tabbed layout.
/// The layout uses SplitContainer + TabControl to approximate docking.
/// To upgrade to full docking, add WeifenLuo.WinFormsUI.DockPanel 3.1.0 and replace
/// the layout with DockPanel and make each panel a DockContent with VS2015DarkTheme.
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

    private readonly DirectoryPanel _directoryPanel;
    private readonly OutputPanel _outputPanel;
    private readonly TabControl _documentTabs;

    // Track open document tabs by key to avoid duplicates
    private readonly Dictionary<string, TabPage> _openTabs = [];

    /// <summary>
    /// MainForm.
    /// </summary>
    public MainForm()
    {
        _directoryVm = new DirectoryPanelViewModel();
        _outputVm = new OutputViewModel();
        _dialogService = new WinFormsDialogService(this);
        _pluginHost = new StudioPluginHost(msg => _outputVm.Log(msg));

        Text = "Gondwana Studio";
        Width = 1280;
        Height = 800;
        MinimumSize = new System.Drawing.Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = DarkBackground;
        ForeColor = DarkForeground;

        var menuStrip = BuildMenuStrip();

        // Outer split: left = Directory, right = content area
        var outerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 220,
            Orientation = Orientation.Vertical,
            BackColor = DarkBackground
        };

        _directoryPanel = new DirectoryPanel(_directoryVm) { Dock = DockStyle.Fill };
        _directoryPanel.NodeActivated += OnNodeActivated;
        ApplyDarkColors(_directoryPanel);
        outerSplit.Panel1.Controls.Add(_directoryPanel);

        // Inner split: top = document tabs, bottom = output
        var innerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            BackColor = DarkBackground
        };
        innerSplit.SplitterDistance = Math.Max(10, innerSplit.Height - 160);

        _documentTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new System.Drawing.Size(120, 24),
            Multiline = true,
            BackColor = DarkBackground
        };
        _documentTabs.DrawItem += OnDrawTab;
        ApplyDarkColors(_documentTabs);
        innerSplit.Panel1.Controls.Add(_documentTabs);

        var outputHeader = new Label
        {
            Text = " Output",
            Dock = DockStyle.Top,
            Height = 22,
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
            ForeColor = DarkForeground,
            Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
        };
        _outputPanel = new OutputPanel(_outputVm) { Dock = DockStyle.Fill };
        innerSplit.Panel2.Controls.Add(_outputPanel);
        innerSplit.Panel2.Controls.Add(outputHeader);

        outerSplit.Panel2.Controls.Add(innerSplit);

        Controls.Add(outerSplit);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;

        _outputVm.Log("Gondwana Studio WinForms ready.");

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
        openTilesheetItem.Click += async (_, _) => await OpenTypedFileAsync("*.gondwana-tilesheet");
        openAnimationItem.Click += async (_, _) => await OpenTypedFileAsync("*.gondwana-animation");
        openSceneItem.Click += async (_, _) => await OpenTypedFileAsync("*.gondwana-scene");
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

    private async Task OpenTypedFileAsync(string pattern)
    {
        var path = await _dialogService.OpenFileAsync("Open File", [pattern]);
        if (!string.IsNullOrWhiteSpace(path))
            OpenByPath(path);
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
        if (_openTabs.TryGetValue(path, out var existing))
        {
            _documentTabs.SelectedTab = existing;
            return;
        }

        if (path.EndsWith(".gondwana-tilesheet", StringComparison.OrdinalIgnoreCase))
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

        foreach (var file in Directory.EnumerateFiles(projectPath, "*.gondwana-animation", SearchOption.AllDirectories))
            _directoryVm.AddEntry(EngineStatePartsCategory.Cycles, Path.GetFileName(file), file);

        foreach (var file in Directory.EnumerateFiles(projectPath, "*.gondwana-scene", SearchOption.AllDirectories))
            _directoryVm.AddEntry(EngineStatePartsCategory.Scenes, Path.GetFileName(file), file);
    }

    private void OpenDocumentPanel(string key, string title, Control content)
    {
        content.Dock = DockStyle.Fill;
        ApplyDarkColors(content);

        var tab = new TabPage(title) { BackColor = DarkBackground, ForeColor = DarkForeground };
        tab.Controls.Add(content);
        _openTabs[key] = tab;
        _documentTabs.TabPages.Add(tab);
        _documentTabs.SelectedTab = tab;
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

    private static void ApplyDarkColors(Control control)
    {
        control.BackColor = DarkSurface;
        control.ForeColor = DarkForeground;
    }

    private void OnDrawTab(object? sender, DrawItemEventArgs e)
    {
        var tabPage = _documentTabs.TabPages[e.Index];
        var textBrush = new System.Drawing.SolidBrush(DarkForeground);
        var bgBrush = new System.Drawing.SolidBrush(
            e.State == DrawItemState.Selected
                ? DarkBackground
                : System.Drawing.Color.FromArgb(45, 45, 48));

        e.Graphics.FillRectangle(bgBrush, e.Bounds);
        e.Graphics.DrawString(tabPage.Text, e.Font ?? Font, textBrush, e.Bounds, StringFormat.GenericDefault);
        bgBrush.Dispose();
        textBrush.Dispose();
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

