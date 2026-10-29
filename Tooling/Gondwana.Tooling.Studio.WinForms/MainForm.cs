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
public sealed partial class MainForm : Form, Gondwana.Tooling.Studio.Core.Extensibility.IStudioPluginHostServices
{
    private UndoRedoMenu _undoRedo = null!;
    // Packed saves disable the document while serialized bytes await archive readers.
    // Host commands must honor the same editing boundary as the disabled editor.
    private Gondwana.Tooling.Editing.EditHistory? ActiveHistory =>
        ActiveDocument is { Enabled: true } active ? active.Document.History : null;

    /// <inheritdoc/>
    string Gondwana.Tooling.Studio.Core.Extensibility.IStudioPluginHostServices.CurrentWorkingDirectory => Browser.WorkingDirectory;
    /// <inheritdoc/>
    void Gondwana.Tooling.Studio.Core.Extensibility.IStudioPluginHostServices.Log(string message) => _output.Log(message);
    /// <inheritdoc/>
    void Gondwana.Tooling.Studio.Core.Extensibility.IStudioPluginHostServices.RefreshWorkingDirectory() => Browser.RefreshDirectory();
    /// <inheritdoc/>
    void Gondwana.Tooling.Studio.Core.Extensibility.IStudioPluginHostServices.OpenDocument(string path) => OpenDocument(path);
    internal const string OpenFilter = "Gondwana authoring files|*.gaf;*.zip;*.gts;*.gani;*.gsnd;*.gscn;*.gspr|All files|*.*";
    private readonly OutputViewModel _output = new();
    internal static readonly (string Format, string Label)[] NewFileFormats =
    [
        ("gaf", "Asset file / GAF…"), ("gts", "Tilesheet / GTS"),
        ("gani", "Animation / GANI"), ("gsnd", "Sound / GSND"),
        ("gscn", "Scene / GSCN"), ("gspr", "Sprite / GSPR")
    ];
    private readonly StudioPluginHost _plugins;
    private readonly ThemeBase _theme = ToolingAppearance.CreateDockTheme();
    private readonly AssetPackageCatalog _packages = new();
    private readonly Dictionary<AssetLocation, StudioDockDocument> _paths = new(DocumentLocationComparer.Instance);
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

    /// <summary>Creates the Studio workspace, discovers plugins, and selects the saved or fallback working directory.</summary>
    /// <remarks>Registers workspace restoration for the Shown event and starts layout persistence. Construct on the UI thread.</remarks>
    public MainForm() : this(loadPlugins: true) { }

    internal MainForm(bool loadPlugins)
    {
        Icon = Gondwana.Tooling.WinForms.ToolingBranding.CreateIcon();
        Text = "Gondwana Studio";
        Size = new Size(1450, 950);
        MinimumSize = new Size(900, 650);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = ToolingAppearance.Palette.Surface;
        ForeColor = ToolingAppearance.Palette.Foreground;
        Workspace = new DockPanel
        {
            Dock = DockStyle.Fill,
            Theme = _theme,
            DocumentStyle = DocumentStyle.DockingWindow,
            ShowDocumentIcon = true,
            DockLeftPortion = 250,
            DockBottomPortion = 130
        };
        _layout = new DockLayoutPersistence(Workspace, "shell");
        _plugins = new StudioPluginHost(_output.Log);
        Browser = new DirectoryPanel(_output.Log);
        NavigationSizing.SynchronizeOnActivation(this);
        Browser.KnownArchivePassword = path => _archivePasswords.GetValueOrDefault(Path.GetFullPath(path));
        Browser.FileActivated += path => TryOpen(path);
        Browser.PackedFileActivated += packed =>
        {
            try { OpenDocument(new AssetLocation(packed.AssetType, packed.ArchivePath, packed.EntryName)); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportError(ex); }
        };
        Browser.ChooseDirectoryRequested += ChooseDirectory;
        Workspace.ActiveDocumentChanged += (_, _) => { SyncActiveDocumentSelection(); UpdateToolbarState(); };
        AddTool("shell.working-directory", "Working directory", Browser, DockState.DockLeft);
        AddTool("shell.output", "Output", new OutputPanel(_output), DockState.DockBottom);
        InitializeDependencies();
        Workspace.ActiveDocumentChanged += DependencySelectionChanged;
        AskSave = document => ThemedMessageBox.Show(this, $"Save changes to {document.Caption}?",
            "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        SavePath = ChooseSavePath;
        AllowInvalidSave = _ => ThemedMessageBox.Show(this, "This definition has validation errors. See its Validation pane. Save it anyway?",
            "Validation failed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        ReportError = ex => { _output.Log(ex.Message); ThemedMessageBox.Show(this, ex.Message, "Gondwana Studio", MessageBoxButtons.OK, MessageBoxIcon.Error); };
        AssetPassword = path => InputDialog.Show($"Password for {Path.GetFileName(path)}:", "Asset password", owner: this, password: true);
        _packages.PasswordProvider = path =>
        {
            path = Path.GetFullPath(path);
            if (_session.IsRestoring && _failedRestoreArchives.Contains(path)) return null;
            // Revalidate cached credentials after package invalidation. PasswordAccepted
            // restores them on success; a changed password must remain retryable.
            string? password;
            if (_archivePasswords.Remove(path, out var known)) password = known;
            // Keep unverified answers out of the session cache so Reload can retry.
            else password = AssetPassword(path);
            if (_session.IsRestoring) _failedRestoreArchives.Add(path);
            return password;
        };
        _packages.PackageAccessed += path => (_loadingDependencies ?? ActiveDocument?.Document.DependencyArchives)?.Add(path);
        _packages.PasswordAccepted += (path, password) =>
        {
            _archivePasswords[path] = password;
            if (_session.IsRestoring) _failedRestoreArchives.Remove(path);
        };
        MainMenuStrip = BuildMenu();
        StudioToolbar = BuildToolbar();
        Controls.Add(Workspace);
        Controls.Add(StudioToolbar);
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
        ToolingAppearance.Apply(this);
        // Restore the ToolStrip's native hover tooltips after appearance is applied.
        StudioToolbar.ShowItemToolTips = true;
        _output.Log("Gondwana Studio ready.");
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        Disposed += (_, _) => _undoRedo?.Dispose();
        _undoRedo = new(() => ActiveHistory, () => ActiveDocument?.Document.CommitEdits() == true);
        Workspace.ActiveDocumentChanged += (_, _) => _undoRedo.Refresh();
        var file = new ToolStripMenuItem("&File");
        var create = new ToolStripMenuItem("&New");
        foreach (var (format, label) in NewFileFormats)
            Add(create, label, Keys.None, () => TryNew(format));
        file.DropDownItems.Add(create);
        Add(file, "&Open…", Keys.Control | Keys.O, OpenFiles);
        Add(file, "Open working &directory…", Keys.None, ChooseDirectory);
        Add(file, "Move / rename content…", Keys.None, MoveContent);
        Add(file, "Delete content…", Keys.None, DeleteContent);
        file.DropDownItems.Add(new ToolStripSeparator());
        Add(file, "&Save", Keys.Control | Keys.S, async () => { if (ActiveDocument is { } doc) await SaveDocument(doc); });
        _saveAllMenuItem = Add(file, "Save A&ll", Keys.Control | Keys.Alt | Keys.S, async () => await SaveAllDocuments());
        Add(file, "Save &As…", Keys.Control | Keys.Shift | Keys.S, async () => { if (ActiveDocument is { } doc) await SaveDocument(doc, saveAs: true); });
        Add(file, "Reload document from disk", Keys.None, () => { if (ActiveDocument is { } doc) ReloadDocument(doc); });
        Add(file, "Reload dependency previews", Keys.None, () => { if (ActiveDocument is { } doc) ReloadPreviews(doc); });
        Add(file, "Find References", Keys.None, () => ShowDependencies(dependents: true));
        Add(file, "Show Dependencies", Keys.None, () => ShowDependencies(dependents: false));
        Add(file, "Show Dependents", Keys.None, () => ShowDependencies(dependents: true));
        Add(file, "&Close", Keys.Control | Keys.W, () => ActiveDocument?.Close());
        Add(file, "E&xit", Keys.Alt | Keys.F4, Close);
        _session.AddMenu(file);
        menu.Items.AddRange([file, _view, new ToolStripMenuItem("&Plugins") { Name = "PluginsMenu" }]);
        menu.Items.Add(_undoRedo.Menu);
        return menu;
    }

    internal void RestoreSession()
    {
        try
        {
            _session.Restore(["gaf", "gts", "gani", "gsnd", "gscn", "gspr"],
                path => { if (!string.Equals(Browser.WorkingDirectory, path, StringComparison.OrdinalIgnoreCase)) SetWorkingDirectory(path); },
                document => OpenDocument(SessionLocation(document)),
                path => _documents.FirstOrDefault(document => string.Equals(document.Document.Path(), path, StringComparison.OrdinalIgnoreCase))?.Activate(),
                _output.Log, _output.Log, document => _paths.GetValueOrDefault(SessionLocation(document))?.Activate());
        }
        finally
        {
            foreach (string path in _failedRestoreArchives) _packages.Invalidate(path);
            _failedRestoreArchives.Clear();
        }
    }

    private static AssetLocation SessionLocation(WorkspaceSessionDocument document)
    {
        if (document.EntryName is null) return new AssetLocation(AssetTypes.Misc, document.Path);
        var type = (AssetTypes)(document.AssetType ?? throw new InvalidDataException("Missing packed asset type."));
        if (StudioDocument.FormatFor(type) != document.Format) throw new InvalidDataException("Packed document type does not match its editor.");
        return new AssetLocation(type, document.Path, document.EntryName);
    }

    private static WorkspaceSessionDocument? SessionIdentity(StudioDocument? document) => document?.Location is { } location
        ? new(location.Path, document.Kind == "asset" ? "gaf" : document.Extension,
            location.AssetEntryName is null ? null : (int)location.AssetType, location.AssetEntryName) : null;

    private void SaveSession() => _session.SaveDocuments(Browser.WorkingDirectory,
        WorkspaceSessionController.DocumentOrder(Workspace, _documents).Select(document => SessionIdentity(document.Document)),
        SessionIdentity(ActiveDocument?.Document));

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
            InvalidateDependencies();
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
            InvalidateDependencies();
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
        _view.DropDownItems.Add(ToolingAppearance.CreateThemeMenu(this));
        foreach (var tool in _tools)
            Add(_view, tool.Text, Keys.None, () => tool.Show(Workspace)).Checked = !tool.IsHidden;
        _view.DropDownItems.Add(NavigationSizing.CreateMenu(this));
        Add(_view, "Reset application layout", Keys.None, _layout.Reset);
        if (ActiveDocument is not { } active) return;
        _view.DropDownItems.Add(new ToolStripSeparator());
        foreach (var name in active.Document.PaneNames)
            Add(_view, name, Keys.None, () => active.Document.ShowPane(name)).Checked = active.Document.IsPaneVisible(name);
        Add(_view, $"Show all {active.Document.Kind} panes", Keys.None, active.Document.ShowAllPanes);
        Add(_view, "Reset active editor layout", Keys.None, active.Document.ResetLayout);
    }

    private void SyncActiveDocumentSelection() =>
        Browser.SelectActiveDocument(ActiveDocument?.Document.Location);

    internal void SetWorkingDirectory(string path)
    {
        InvalidateDependencies();
        path = Path.GetFullPath(path);
        if (_workingDirectory is not null) _plugins.NotifyProjectClosed();
        Browser.SetDirectory(path);
        SyncActiveDocumentSelection();
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

    internal StudioDockDocument OpenDocument(string path, string? password = null) =>
        OpenDocument(new AssetLocation(AssetTypes.Misc, Path.GetFullPath(path)), password);

    internal StudioDockDocument OpenDocument(AssetLocation location, string? password = null)
    {
        if (_paths.TryGetValue(location, out var existing)) { existing.Activate(); return existing; }
        return ShowDocument(CreateModel(location, password));
    }
    private async void TryNew(string format)
    {
        try
        {
            if (format != "gaf") { NewDocument(format); return; }
            using var dialog = new SaveFileDialog { Filter = "Asset files|*.gaf;*.zip", DefaultExt = "gaf", InitialDirectory = Browser.WorkingDirectory };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (File.Exists(dialog.FileName)) { TryOpen(dialog.FileName); return; }
            bool encrypt = ThemedMessageBox.Show(this, "Enable password protection?", "New asset file", MessageBoxButtons.YesNo) == DialogResult.Yes;
            string? password = encrypt ? InputDialog.Show("Password:", "New asset file", owner: this, password: true) : null;
            if (encrypt && string.IsNullOrWhiteSpace(password)) return;
            var document = NewDocument(format, dialog.FileName, password, encrypt);
            await SaveDocument(document);
        }
        catch (Exception ex) { ReportError(ex); }
    }

    internal StudioDockDocument NewDocument(string format, string? assetPath = null, string? password = null, bool encrypt = false)
    {
        if (assetPath is not null && _paths.TryGetValue(new AssetLocation(AssetTypes.Misc, Path.GetFullPath(assetPath)), out var existing))
        { existing.Activate(); return existing; }
        return ShowDocument(StudioDocument.Create(format, Browser.WorkingDirectory, assetPath, _packages, password, encrypt));
    }

    private StudioDockDocument ShowDocument(StudioDocument model)
    {
        var document = new StudioDockDocument(model, ConfirmClose);
        if (model.Editor is Gondwana.Tooling.Scenes.WinForms.SceneEditorControl sceneEditor)
            sceneEditor.SaveRequestedAsync = () => SaveDocument(document);
        _documents.Add(document);
        model.Changed += ToolbarDocumentChanged;
        model.Changed += DependencySelectionChanged;
        var referencesMenu = new ContextMenuStrip();
        foreach (var title in new[] { "Find References", "Show Dependencies", "Show Dependents" })
            referencesMenu.Items.Add(title, null, (_, _) => ShowDependencies(
                model.Location is { } endpoint ? new ResourceAddress(endpoint, model.SelectedMember) : null,
                dependents: title != "Show Dependencies"));
        document.TabPageContextMenuStrip = referencesMenu;
        if (model.Location is { } location) _paths.Add(location, document);
        if (model.Editor is AssetEditorControl assets)
        {
            assets.SaveRequested = async saveAs => await SaveDocument(document, saveAs);
            assets.IsDocumentOpen = path => _paths.Keys.Any(location => string.Equals(location.Path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
            assets.RelocationScope = () => Browser.WorkingDirectory;
            assets.ValidateRelocation = plan => ValidateRelocation(plan, assets.FilePath);
            assets.ValidateDeletion = plan => ValidateDeletion(plan, assets.FilePath);
            assets.WorkspaceChanged = () =>
            {
                InvalidateDependencies();
                if (!assets.IsDirty && File.Exists(assets.FilePath))
                {
                    using var current = StudioArchiveSnapshot.Open(assets.FilePath, assets.AssetsFile.Password);
                    // Existing relocation/deletion transactions already verify the editor against disk.
                    model.ArchiveHash = current.Hash;
                }
                _packages.Clear();
                Browser.RefreshDirectory();
            };
        }
        document.Disposed += (_, _) =>
        {
            model.Changed -= ToolbarDocumentChanged;
            model.Changed -= DependencySelectionChanged;
            referencesMenu.Dispose();
            _documents.Remove(document);
            _saves.Remove(document);
            foreach (var key in _paths.Where(pair => pair.Value == document).Select(pair => pair.Key).ToArray()) _paths.Remove(key);
            UpdateToolbarState();
        };
        document.Show(Workspace, DockState.Document);
        document.Activate();
        SyncActiveDocumentSelection();
        UpdateToolbarState();
        if (model.Kind == "asset") Browser.RefreshDirectory(); // authenticated archives can now be browsed without another prompt
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
            bool packedSave = !saveAs && destination is null && model.PackedLocation() is not null;
            destination ??= packedSave ? model.PackedLocation()!.Path : saveAs || model.Path() is null ? SavePath(model) : model.Path();
            if (destination is null) return false;
            destination = Path.GetFullPath(destination);
            if (!packedSave && StudioDocument.FormatFor(destination) != StudioDocument.FormatFor("file." + model.Extension))
                throw new InvalidOperationException("Choose a filename with the document's format extension.");
            var target = packedSave ? model.PackedLocation()! : new AssetLocation(StudioDocument.TypeFor(model.Extension), destination);
            void CheckOwner()
            {
                if (_paths.TryGetValue(target, out var owner) && owner != document)
                    throw new InvalidOperationException("That destination is already open in another document. Close it before overwriting it.");
            }
            CheckOwner();
            var errors = model.Validate();
            if (errors.Count > 0 && !AllowInvalidSave(errors)) return false;
            var bytes = packedSave ? model.SerializePacked!() : null;
            void Save()
            {
                if (document.IsDisposed || IsDisposed) throw new OperationCanceledException();
                CheckOwner();
                if (packedSave || model.Kind == "asset") SaveArchive(document, destination, bytes);
                else
                {
                    model.Save(destination, errors.Count > 0);
                    model.ArchiveHash = null;
                    model.OriginalEntry = null;
                    model.ContentStale = false;
                    model.ArchivePassword = null;
                }
                foreach (var key in _paths.Where(pair => pair.Value == document).Select(pair => pair.Key).ToArray()) _paths.Remove(key);
                _paths.Add(model.Location!, document);
                document.UpdateCaption();
                SyncActiveDocumentSelection();
                _packages.Invalidate(destination);
                _output.Log($"Saved {model.Tooltip}");
                InvalidateDependencies();
                if (_followDependencySelection) _dependencyTarget = ActiveAddress();
            }
            if (packedSave || model.Kind == "asset")
            {
                InvalidateDependencies(); // Cancel readers before asynchronously draining their archive handles.
                document.Enabled = false;
                _undoRedo.Refresh();
                try { await Browser.RunWithoutArchiveReadsAsync(Save); }
                finally
                {
                    if (!document.IsDisposed) document.Enabled = true;
                    if (!IsDisposed && !_disposed) _undoRedo.Refresh();
                }
            }
            else { Save(); Browser.RefreshDirectory(); }
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { ReportError(ex); return false; }
        finally { if (!IsDisposed && !_disposed) UpdateToolbarState(); }
    }
    private string? ChooseSavePath(StudioDocument model)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = model.Kind == "asset" ? "Asset files|*.gaf;*.zip" : $"Gondwana {model.Kind}|*.{model.Extension}",
            DefaultExt = model.Extension,
            AddExtension = true,
            InitialDirectory = model.Location is { } location ? Path.GetDirectoryName(location.Path) : Browser.WorkingDirectory,
            FileName = model.PackedLocation()?.AssetEntryName is { } entry ? Path.GetFileName(entry) : model.Path() is { } name ? Path.GetFileName(name) : $"Untitled.{model.Extension}"
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

    /// <summary>Requests save or discard approval for open documents and records the session only after approval.</summary>
    /// <param name="e">Closing event cancelled if approval is denied or still pending.</param>
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

    private void ApplyTheme(Control control) => ToolingAppearance.Apply(control);

    /// <summary>Closes plugin project state and disposes layout persistence, documents, tools, package resources, and the theme.</summary>
    /// <param name="disposing">Whether to release managed shell resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            InvalidateDependencies();
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
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) =>
        UndoRedoMenu.TryShortcut(this, keyData, ActiveHistory, () => ActiveDocument?.Document.CommitEdits() == true) || base.ProcessCmdKey(ref msg, keyData);

}
