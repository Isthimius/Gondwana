using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Assets.WinForms;
using Gondwana.Tooling.Studio.ViewModels;
using Gondwana.Tooling.Studio.WinForms.Documents;
using Gondwana.Tooling.Studio.WinForms.Extensibility;
using Gondwana.Tooling.Studio.WinForms.Panels;
using Gondwana.Tooling.Studio.WinForms.Services;
using Gondwana.Tooling.Tilesheets.Sources;
using Gondwana.Tooling.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Studio.WinForms;

/// <summary>Studio owns outer documents and tools; authoring controls own their inner workspaces.</summary>
public sealed class MainForm : Form, Gondwana.Tooling.Studio.Core.Extensibility.IStudioPluginHostServices
{
    string Gondwana.Tooling.Studio.Core.Extensibility.IStudioPluginHostServices.CurrentWorkingDirectory => Browser.WorkingDirectory;
    void Gondwana.Tooling.Studio.Core.Extensibility.IStudioPluginHostServices.Log(string message) => _output.Log(message);
    void Gondwana.Tooling.Studio.Core.Extensibility.IStudioPluginHostServices.RefreshWorkingDirectory() => Browser.RefreshDirectory();
    void Gondwana.Tooling.Studio.Core.Extensibility.IStudioPluginHostServices.OpenDocument(string path) => OpenDocument(path);
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
    private readonly WorkspaceSessionController _session = new();
    private readonly ProjectFolderStore _projectFolder = new();
    private bool _disposed;
    private bool _shutdownPending;
    private bool _shutdownApproved;
    private readonly Dictionary<StudioDockDocument, Task<bool>> _saves = new();
    internal DockPanel Workspace { get; }
    internal DirectoryPanel Browser { get; }
    internal IReadOnlyList<StudioDockDocument> Documents => _documents;
    internal StudioDockDocument? ActiveDocument => Workspace.ActiveDocument as StudioDockDocument;
    internal Func<StudioDocument, DialogResult> AskSave { get; set; }
    internal Func<StudioDocument, string?> SavePath { get; set; }
    internal Func<IReadOnlyList<string>, bool> AllowInvalidSave { get; set; }
    internal Action<Exception> ReportError { get; set; }
    internal Func<string, string?> AssetPassword { get; set; }

    /// <summary>
    /// Initializes a new instance of the <c>MainForm</c> class.
    /// </summary>
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
            Dock = DockStyle.Fill,
            Theme = _theme,
            DocumentStyle = DocumentStyle.DockingWindow,
            DockLeftPortion = 250,
            DockBottomPortion = 130
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
            _plugins.AttachHostServices(this);
            AttachPlugins();
        }
        _layout.Start();
        SetWorkingDirectory(_session.InitialWorkingDirectory(_projectFolder.Read()));
        Shown += (_, _) => RestoreSession();
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
        Add(file, "Move / rename content…", Keys.None, MoveContent);
        Add(file, "Delete content…", Keys.None, DeleteContent);
        file.DropDownItems.Add(new ToolStripSeparator());
        Add(file, "&Save", Keys.Control | Keys.S, async () => { if (ActiveDocument is { } doc) await SaveDocument(doc); });
        Add(file, "Save &As…", Keys.Control | Keys.Shift | Keys.S, async () => { if (ActiveDocument is { } doc) await SaveDocument(doc, saveAs: true); });
        Add(file, "&Close", Keys.Control | Keys.W, () => ActiveDocument?.Close());
        Add(file, "E&xit", Keys.Alt | Keys.F4, Close);
        _session.AddMenu(file);
        menu.Items.AddRange([file, _view, new ToolStripMenuItem("&Plugins") { Name = "PluginsMenu" }]);
        return menu;
    }

    internal void RestoreSession() => _session.Restore(["gaf", "gts", "gani", "gsnd", "gscn", "gspr"],
        path => { if (!string.Equals(Browser.WorkingDirectory, path, StringComparison.OrdinalIgnoreCase)) SetWorkingDirectory(path); },
        document => OpenDocument(document.Path),
        path => _documents.FirstOrDefault(document => string.Equals(document.Document.Path(), path, StringComparison.OrdinalIgnoreCase))?.Activate(),
        _output.Log);

    private void SaveSession() => _session.Save(Browser.WorkingDirectory,
        WorkspaceSessionController.DocumentOrder(Workspace, _documents).Select(document => document.Document.Path()),
        ActiveDocument?.Document.Path());

    private void MoveContent()
    {
        using var dialog = new OpenFileDialog { Title = "Move / rename content (save and close documents first)", Filter = "All content|*.*", InitialDirectory = Browser.WorkingDirectory };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var path = Path.GetFullPath(dialog.FileName);
        bool archive = AssetArchiveFormat.IsArchive(path);
        AssetMoveDialog.Show(this, new AssetLocation(AssetImportPlan.InferType(path), path), Browser.WorkingDirectory, plan =>
        {
            ValidateRelocation(plan, null);
            plan.Commit();
            _packages.Clear();
            Browser.RefreshDirectory();
            _output.Log("Move committed within scope: " + plan.Scope);
        }, wholeArchive: archive);
    }

    private void DeleteContent()
    {
        using var dialog = new OpenFileDialog { Title = "Delete content (save and close documents first)", Filter = "All content|*.*", Multiselect = true, InitialDirectory = Browser.WorkingDirectory };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var paths = dialog.FileNames.Select(Path.GetFullPath).ToArray();
        var archives = paths.Where(AssetArchiveFormat.IsArchive).ToArray();
        var targets = paths.Except(archives).Select(path => new AssetLocation(AssetImportPlan.InferType(path), path)).ToArray();
        AssetDeleteDialog.Show(this, targets, archives, Browser.WorkingDirectory, (plan, force) =>
        {
            ValidateDeletion(plan, null);
            plan.Commit(force);
            _packages.Clear();
            Browser.RefreshDirectory();
            _output.Log("Deletion committed within scope: " + plan.Scope);
        });
    }

    /// <summary>Protects open editor models before reference-aware deletion.</summary>
    /// <param name="plan">Reviewed deletion.</param>
    /// <param name="currentArchive">Optional clean archive allowed to refresh itself.</param>
    internal void ValidateDeletion(AssetDeletionPlan plan, string? currentArchive) => ValidateContentOperation(currentArchive);

    /// <summary>Protects authoritative editor state before a filesystem relocation.</summary>
    /// <param name="plan">Reviewed relocation.</param>
    /// <param name="currentArchive">Optional clean shared archive editor allowed to refresh itself.</param>
    internal void ValidateRelocation(AssetRelocationPlan plan, string? currentArchive) => ValidateContentOperation(currentArchive);

    private void ValidateContentOperation(string? currentArchive)
    {
        foreach (var document in _documents)
            if (currentArchive is null || !string.Equals(document.Document.Path(), currentArchive, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Save and close other authoring documents before moving or deleting content. This prevents overwriting models or missing unsaved references.");
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
        if (Directory.Exists(path)) _projectFolder.Write(path);
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

    private async void TryNew(string format)
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
            await SaveDocument(document);
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
        if (model.Editor is Gondwana.Tooling.Scenes.WinForms.SceneEditorControl sceneEditor)
            // Loose-definition saves complete synchronously; only asset saves await readers.
            sceneEditor.SaveRequested = () => SaveDocument(document).GetAwaiter().GetResult();
        _documents.Add(document);
        if (model.Path() is { } path) _paths.Add(Path.GetFullPath(path), document);
        if (model.Editor is AssetEditorControl assets)
        {
            assets.SaveRequested = async saveAs => await SaveDocument(document, saveAs);
            assets.IsDocumentOpen = path => _paths.ContainsKey(Path.GetFullPath(path));
            assets.RelocationScope = () => Browser.WorkingDirectory;
            assets.ValidateRelocation = plan => ValidateRelocation(plan, assets.FilePath);
            assets.ValidateDeletion = plan => ValidateDeletion(plan, assets.FilePath);
            assets.WorkspaceChanged = () => { _packages.Clear(); Browser.RefreshDirectory(); };
        }
        document.Disposed += (_, _) =>
        {
            _documents.Remove(document);
            _saves.Remove(document);
            foreach (var key in _paths.Where(pair => pair.Value == document).Select(pair => pair.Key).ToArray()) _paths.Remove(key);
        };
        document.Show(Workspace, DockState.Document);
        document.Activate();
        _output.Log($"Opened {model.Path() ?? document.Text}");
        return document;
    }

    internal Task<bool> SaveDocument(StudioDockDocument document, bool saveAs = false, string? destination = null)
    {
        if (_saves.TryGetValue(document, out var pending) && !pending.IsCompleted) return pending;
        return _saves[document] = SaveDocumentCore(document, saveAs, destination);
    }

    private async Task<bool> SaveDocumentCore(StudioDockDocument document, bool saveAs, string? destination)
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
            void Save()
            {
                if (document.IsDisposed || IsDisposed) throw new OperationCanceledException();
                // Another document may have opened this destination while we awaited the reader.
                if (_paths.TryGetValue(destination, out var owner) && owner != document)
                    throw new InvalidOperationException("That destination is already open in another document. Close it before overwriting it.");
                model.Save(destination, errors.Count > 0);
                foreach (var key in _paths.Where(pair => pair.Value == document).Select(pair => pair.Key).ToArray()) _paths.Remove(key);
                _paths.Add(Path.GetFullPath(model.Path()!), document);
                document.UpdateCaption();
                _packages.Invalidate(destination);
                _output.Log($"Saved {destination}");
            }
            if (model.Kind == "asset")
            {
                document.Enabled = false;
                try { await Browser.RunWithoutArchiveReadsAsync(Save); }
                finally { if (!document.IsDisposed) document.Enabled = true; }
            }
            else
            {
                Save();
                Browser.RefreshDirectory();
            }
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { ReportError(ex); return false; }
    }

    private string? ChooseSavePath(StudioDocument model)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = model.Kind == "asset" ? "Asset files|*.gaf;*.zip" : $"Gondwana {model.Kind}|*.{model.Extension}",
            DefaultExt = model.Extension,
            AddExtension = true,
            InitialDirectory = model.Path() is { } path ? Path.GetDirectoryName(path) : Browser.WorkingDirectory,
            FileName = model.Path() is { } name ? Path.GetFileName(name) : $"Untitled.{model.Extension}"
        };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }

    internal async Task<bool> ConfirmClose(StudioDockDocument document)
    {
        if (_saves.TryGetValue(document, out var pending) && !pending.IsCompleted)
            return await pending;
        if (!document.Document.CommitEdits()) return false;
        if (!document.Document.Dirty()) return true;
        return AskSave(document.Document) switch
        {
            DialogResult.No => true,
            DialogResult.Yes => await SaveDocument(document),
            _ => false
        };
    }

    internal async Task<bool> ApproveShutdown()
    {
        foreach (var document in _documents.ToArray())
            if (!await ConfirmClose(document)) return false;
        foreach (var document in _documents) document.CloseApproved = true;
        return true;
    }

    /// <inheritdoc/>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel) return;
        if (_shutdownApproved) { SaveSession(); return; }
        if (_shutdownPending) { e.Cancel = true; return; }
        var approval = ApproveShutdown();
        if (approval.IsCompleted)
        {
            e.Cancel = !approval.GetAwaiter().GetResult();
            if (!e.Cancel) SaveSession();
            return;
        }
        e.Cancel = true;
        _shutdownPending = true;
        Enabled = false;
        FinishShutdownAsync(approval);
    }

    private async void FinishShutdownAsync(Task<bool> approval)
    {
        try
        {
            if (await approval && !IsDisposed)
            {
                _shutdownApproved = true;
                Close();
            }
        }
        finally
        {
            _shutdownPending = false;
            if (!IsDisposed) Enabled = true;
        }
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

    /// <inheritdoc/>
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

    /// <summary>
    /// Initializes a new instance of the <c>DarkMenuRenderer</c> class.
    /// </summary>
    public DarkMenuRenderer() : base(new DarkColorTable())
    {
    }

    /// <inheritdoc/>
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var g = e.Graphics;
        var bounds = new System.Drawing.Rectangle(0, 0, e.Item.Width, e.Item.Height);
        using var brush = new System.Drawing.SolidBrush(e.Item.Selected ? DarkHighlight : DarkBg);
        g.FillRectangle(brush, bounds);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public override System.Drawing.Color MenuStripGradientBegin => DarkBg;
    /// <inheritdoc/>
    public override System.Drawing.Color MenuStripGradientEnd => DarkBg;
    /// <inheritdoc/>
    public override System.Drawing.Color ToolStripDropDownBackground => DarkBg;
    /// <inheritdoc/>
    public override System.Drawing.Color ImageMarginGradientBegin => DarkBg;
    /// <inheritdoc/>
    public override System.Drawing.Color ImageMarginGradientMiddle => DarkBg;
    /// <inheritdoc/>
    public override System.Drawing.Color ImageMarginGradientEnd => DarkBg;
    /// <inheritdoc/>
    public override System.Drawing.Color MenuBorder => System.Drawing.Color.FromArgb(60, 60, 60);
    /// <inheritdoc/>
    public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.FromArgb(60, 60, 60);
}
