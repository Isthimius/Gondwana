using Gondwana.Tooling.WinForms;
using Gondwana.Tooling.Assets.WinForms;
using Gondwana.Tooling.Studio.ViewModels;
using Gondwana.Tooling.Studio.WinForms.Documents;
using Gondwana.Tooling.Studio.WinForms.Extensibility;
using Gondwana.Tooling.Studio.WinForms.Panels;
using Gondwana.Tooling.Studio.WinForms.Services;
using Gondwana.Tooling.Tilesheets.Sources;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace Gondwana.Tooling.Studio.WinForms;

/// <summary>Studio owns outer documents and tools; authoring controls own their inner workspaces.</summary>
public sealed class MainForm : Form
{
    internal const string OpenFilter = "Gondwana authoring files|*.gaf;*.zip;*.gts;*.gani;*.gsnd;*.gscn;*.gspr|All files|*.*";
    private readonly OutputViewModel _output = new();
    private readonly StudioPluginHost _plugins;
    private readonly VS2015DarkTheme _theme = new();
    private readonly AssetPackageCatalog _packages = new();
    private readonly Dictionary<string, StudioDockDocument> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<StudioDockDocument> _documents = [];
    private readonly List<DockContent> _tools = [];
    private readonly ToolStripMenuItem _view = new("&View");
    private readonly DockLayoutPersistence _layout;
    private bool _disposed;
    internal DockPanel Workspace { get; }
    internal DirectoryPanel Browser { get; }
    internal IReadOnlyList<StudioDockDocument> Documents => _documents;
    internal StudioDockDocument? ActiveDocument => Workspace.ActiveDocument as StudioDockDocument;
    internal Func<StudioDocument, DialogResult> AskSave { get; set; }
    internal Func<StudioDocument, string?> SavePath { get; set; }
    internal Func<IReadOnlyList<string>, bool> AllowInvalidSave { get; set; }
    internal Action<Exception> ReportError { get; set; }
    internal Func<string, string?> AssetPassword { get; set; }

    public MainForm() : this(loadPlugins: true) { }

    internal MainForm(bool loadPlugins)
    {
        Text = "Gondwana Studio";
        Size = new Size(1450, 950);
        MinimumSize = new Size(900, 650);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(37, 37, 38);
        ForeColor = Color.Gainsboro;
        Workspace = new DockPanel
        {
            Dock = DockStyle.Fill, Theme = _theme, DocumentStyle = DocumentStyle.DockingWindow,
            DockLeftPortion = 250, DockBottomPortion = 130
        };
        _layout = new DockLayoutPersistence(Workspace, "shell");
        _plugins = new StudioPluginHost(_output.Log);
        Browser = new DirectoryPanel(_output.Log);
        Browser.FileActivated += path => TryOpen(path);
        Browser.ChooseDirectoryRequested += ChooseDirectory;
        AddTool("shell.working-directory", "Working directory", Browser, DockState.DockLeft);
        AddTool("shell.output", "Output", new OutputPanel(_output), DockState.DockBottom);
        AskSave = document => MessageBox.Show(this, $"Save changes to {Path.GetFileName(document.Path()) ?? "Untitled." + document.Extension}?",
            "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        SavePath = ChooseSavePath;
        AllowInvalidSave = _ => MessageBox.Show(this, "This definition has validation errors. See its Validation pane. Save it anyway?",
            "Validation failed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        ReportError = ex => { _output.Log(ex.Message); MessageBox.Show(this, ex.Message, "Gondwana Studio", MessageBoxButtons.OK, MessageBoxIcon.Error); };
        AssetPassword = path => InputDialog.Show($"Password for {Path.GetFileName(path)}:", "Asset password", owner: this, password: true);
        _packages.PasswordProvider = path => AssetPassword(path);
        MainMenuStrip = BuildMenu();
        Controls.Add(Workspace);
        Controls.Add(MainMenuStrip);
        _view.DropDownOpening += (_, _) => RebuildViewMenu();
        if (loadPlugins)
        {
            _plugins.DiscoverAndLoad();
            AttachPlugins();
        }
        _layout.Start();
        SetWorkingDirectory(Environment.CurrentDirectory);
        _output.Log("Gondwana Studio ready.");
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip { Renderer = new DarkMenuRenderer(), BackColor = BackColor, ForeColor = ForeColor };
        var file = new ToolStripMenuItem("&File");
        var create = new ToolStripMenuItem("&New");
        foreach (var (format, label) in new[] { ("gaf", "Asset file / GAF…"), ("gts", "Tilesheet / GTS"),
            ("gani", "Animation / GANI"), ("gsnd", "Sound / GSND"), ("gscn", "Scene / GSCN"), ("gspr", "Sprite / GSPR") })
            Add(create, label, Keys.None, () => TryNew(format));
        file.DropDownItems.Add(create);
        Add(file, "&Open…", Keys.Control | Keys.O, OpenFiles);
        Add(file, "Open working &directory…", Keys.None, ChooseDirectory);
        file.DropDownItems.Add(new ToolStripSeparator());
        Add(file, "&Save", Keys.Control | Keys.S, () => { if (ActiveDocument is { } doc) SaveDocument(doc); });
        Add(file, "Save &As…", Keys.Control | Keys.Shift | Keys.S, () => { if (ActiveDocument is { } doc) SaveDocument(doc, saveAs: true); });
        Add(file, "&Close", Keys.Control | Keys.W, () => ActiveDocument?.Close());
        Add(file, "E&xit", Keys.Alt | Keys.F4, Close);
        menu.Items.AddRange([file, _view, new ToolStripMenuItem("&Plugins") { Name = "PluginsMenu" }]);
        return menu;
    }

    private static ToolStripMenuItem Add(ToolStripMenuItem menu, string label, Keys shortcut, Action action)
    {
        var item = new ToolStripMenuItem(label) { ShortcutKeys = shortcut };
        item.Click += (_, _) => action();
        menu.DropDownItems.Add(item);
        return item;
    }

    internal void RebuildViewMenu()
    {
        while (_view.DropDownItems.Count > 0) _view.DropDownItems[0].Dispose();
        foreach (var tool in _tools)
            Add(_view, tool.Text, Keys.None, () => tool.Show(Workspace)).Checked = !tool.IsHidden;
        Add(_view, "Reset application layout", Keys.None, _layout.Reset);
        if (ActiveDocument is not { } active) return;
        _view.DropDownItems.Add(new ToolStripSeparator());
        foreach (var name in active.Document.PaneNames)
            Add(_view, name, Keys.None, () => active.Document.ShowPane(name)).Checked = active.Document.IsPaneVisible(name);
        Add(_view, $"Show all {active.Document.Kind} panes", Keys.None, active.Document.ShowAllPanes);
        Add(_view, "Reset active editor layout", Keys.None, active.Document.ResetLayout);
    }

    internal void SetWorkingDirectory(string path)
    {
        path = Path.GetFullPath(path);
        if (_workingDirectory is not null) _plugins.NotifyProjectClosed();
        Browser.SetDirectory(path);
        _workingDirectory = path;
        _plugins.NotifyProjectOpened(path);
        _output.Log($"Working directory: {path}");
    }
    private string? _workingDirectory;

    private void ChooseDirectory()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose working directory", UseDescriptionForTitle = true, SelectedPath = Browser.WorkingDirectory };
        if (dialog.ShowDialog(this) == DialogResult.OK) SetWorkingDirectory(dialog.SelectedPath);
    }

    private void OpenFiles()
    {
        using var dialog = new OpenFileDialog { Filter = OpenFilter, Multiselect = true, InitialDirectory = Browser.WorkingDirectory };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            foreach (var path in dialog.FileNames) TryOpen(path);
    }

    private void TryOpen(string path)
    {
        try { OpenDocument(path); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ReportError(ex); }
    }

    internal StudioDockDocument OpenDocument(string path, string? password = null)
    {
        path = Path.GetFullPath(path);
        if (_paths.TryGetValue(path, out var existing)) { existing.Activate(); return existing; }
        if (!File.Exists(path)) throw new FileNotFoundException("Authoring file does not exist.", path);
        var format = StudioDocument.FormatFor(path) ?? throw new NotSupportedException("Unsupported authoring file: " + path);
        StudioDocument model;
        try { model = StudioDocument.Create(format, Browser.WorkingDirectory, path, _packages, password); }
        catch (Exception) when (format == "gaf" && password is null)
        {
            password = AssetPassword(path);
            if (password is null) throw new OperationCanceledException("Asset file opening cancelled.");
            model = StudioDocument.Create(format, Browser.WorkingDirectory, path, _packages, password);
        }
        return ShowDocument(model);
    }

    private void TryNew(string format)
    {
        try
        {
            if (format != "gaf") { NewDocument(format); return; }
            using var dialog = new SaveFileDialog { Filter = "Asset files|*.gaf;*.zip", DefaultExt = "gaf", InitialDirectory = Browser.WorkingDirectory };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (File.Exists(dialog.FileName)) { TryOpen(dialog.FileName); return; }
            bool encrypt = MessageBox.Show(this, "Enable password protection?", "New asset file", MessageBoxButtons.YesNo) == DialogResult.Yes;
            string? password = encrypt ? InputDialog.Show("Password:", "New asset file", owner: this, password: true) : null;
            if (encrypt && string.IsNullOrWhiteSpace(password)) return;
            var document = NewDocument(format, dialog.FileName, password, encrypt);
            SaveDocument(document);
        }
        catch (Exception ex) { ReportError(ex); }
    }

    internal StudioDockDocument NewDocument(string format, string? assetPath = null, string? password = null, bool encrypt = false)
    {
        if (assetPath is not null && _paths.TryGetValue(Path.GetFullPath(assetPath), out var existing))
        { existing.Activate(); return existing; }
        return ShowDocument(StudioDocument.Create(format, Browser.WorkingDirectory, assetPath, _packages, password, encrypt));
    }

    private StudioDockDocument ShowDocument(StudioDocument model)
    {
        var document = new StudioDockDocument(model, ConfirmClose);
        _documents.Add(document);
        if (model.Path() is { } path) _paths.Add(Path.GetFullPath(path), document);
        if (model.Editor is AssetEditorControl assets)
        {
            assets.SaveRequested = saveAs => SaveDocument(document, saveAs);
            assets.IsDocumentOpen = path => _paths.ContainsKey(Path.GetFullPath(path));
            assets.WorkspaceChanged = Browser.RefreshDirectory;
        }
        document.Disposed += (_, _) =>
        {
            _documents.Remove(document);
            foreach (var key in _paths.Where(pair => pair.Value == document).Select(pair => pair.Key).ToArray()) _paths.Remove(key);
        };
        document.Show(Workspace, DockState.Document);
        document.Activate();
        _output.Log($"Opened {model.Path() ?? document.Text}");
        return document;
    }

    internal bool SaveDocument(StudioDockDocument document, bool saveAs = false, string? destination = null)
    {
        try
        {
            var model = document.Document;
            if (!model.CommitEdits()) return false;
            destination ??= saveAs || model.Path() is null ? SavePath(model) : model.Path();
            if (destination is null) return false;
            destination = Path.GetFullPath(destination);
            if (StudioDocument.FormatFor(destination) != StudioDocument.FormatFor("file." + model.Extension))
                throw new InvalidOperationException("Choose a filename with the document's format extension.");
            if (_paths.TryGetValue(destination, out var existing) && existing != document)
                throw new InvalidOperationException("That destination is already open in another document. Close it before overwriting it.");
            var errors = model.Validate();
            if (errors.Count > 0 && !AllowInvalidSave(errors)) return false;
            model.Save(destination, errors.Count > 0);
            foreach (var key in _paths.Where(pair => pair.Value == document).Select(pair => pair.Key).ToArray()) _paths.Remove(key);
            _paths.Add(Path.GetFullPath(model.Path()!), document);
            document.UpdateCaption();
            _packages.Invalidate(destination);
            Browser.RefreshDirectory();
            _output.Log($"Saved {destination}");
            return true;
        }
        catch (Exception ex) { ReportError(ex); return false; }
    }

    private string? ChooseSavePath(StudioDocument model)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = model.Kind == "asset" ? "Asset files|*.gaf;*.zip" : $"Gondwana {model.Kind}|*.{model.Extension}",
            DefaultExt = model.Extension, AddExtension = true,
            InitialDirectory = model.Path() is { } path ? Path.GetDirectoryName(path) : Browser.WorkingDirectory,
            FileName = model.Path() is { } name ? Path.GetFileName(name) : $"Untitled.{model.Extension}"
        };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }

    internal bool ConfirmClose(StudioDockDocument document)
    {
        if (!document.Document.CommitEdits()) return false;
        if (!document.Document.Dirty()) return true;
        return AskSave(document.Document) switch
        {
            DialogResult.No => true,
            DialogResult.Yes => SaveDocument(document),
            _ => false
        };
    }

    internal bool ApproveShutdown()
    {
        foreach (var document in _documents.ToArray())
            if (!ConfirmClose(document)) return false;
        foreach (var document in _documents) document.CloseApproved = true;
        return true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!e.Cancel) e.Cancel = !ApproveShutdown();
    }

    private DockContent AddTool(string id, string title, Control control, DockState state)
    {
        var tool = new PersistentDockContent(id) { Text = title, HideOnClose = true };
        control.Dock = DockStyle.Fill;
        ApplyTheme(control);
        tool.Controls.Add(control);
        _tools.Add(tool);
        _layout.Register(tool, () => tool.Show(Workspace, state));
        tool.Show(Workspace, state);
        return tool;
    }

    private void AttachPlugins()
    {
        var menu = (ToolStripMenuItem)MainMenuStrip!.Items["PluginsMenu"]!;
        foreach (var item in _plugins.GetPluginMenuItems()) menu.DropDownItems.Add(item);
        foreach (var (id, name, control) in _plugins.GetPersistentPluginPanels())
            AddTool(id, name, control, DockState.DockRight);
    }

    private void ApplyTheme(Control control)
    {
        control.BackColor = Color.FromArgb(30, 30, 30);
        control.ForeColor = Color.Gainsboro;
        if (control is ToolStrip strip) _theme.ApplyTo(strip);
        foreach (Control child in control.Controls) ApplyTheme(child);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _layout.Dispose();
            if (_workingDirectory is not null) _plugins.NotifyProjectClosed();
            foreach (var document in _documents.ToArray()) document.Dispose();
            foreach (var tool in _tools) tool.Dispose();
            _tools.Clear();
            _packages.Dispose();
        }
        base.Dispose(disposing);
        if (disposing) _theme.Dispose();
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

