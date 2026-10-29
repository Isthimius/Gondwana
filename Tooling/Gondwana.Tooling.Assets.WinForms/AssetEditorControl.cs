using System.ComponentModel;
using Gondwana.Assets;
using Gondwana.Tooling.Editing;
using Gondwana.Tooling.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Assets.WinForms;

/// <summary>
/// Hostable WinForms editor for one Gondwana asset package. The standalone GAF
/// application wraps this control in DockContent; Gondwana Studio can host the
/// same surface directly.
/// </summary>
public sealed class AssetEditorControl : UserControl
{
    /// <summary>Gets the pane titles accepted by the pane visibility methods.</summary>
    public static IReadOnlyList<string> PaneNames { get; } =
        Array.AsReadOnly(["Assets", "Status"]);

    private const string AssetFileFilter = "Asset Files (*.gaf;*.zip)|*.gaf;*.zip|All Files (*.*)|*.*";

    private AssetsFile _assetsFile;
    private AssetsFile? _ownedSavedCopy;

    /// <summary>Dirty state for edits made through this editor.</summary>
    public bool IsDirty => History.IsDirty;
    private readonly AssetEditSession _edits;
    /// <summary>Document-local entry history.</summary>
    public EditHistory History => _edits.History;
    /// <summary>Occurs synchronously when edits mark the archive dirty or an accepted save clears the dirty state.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets or sets host routing for Save and Save As; the argument is false for Save and true for Save As. Null uses the standalone save flow.</summary>
    public Action<bool>? SaveRequested { get; set; }

    /// <summary>Notifies a host that the underlying package or a saved copy changed.</summary>
    public Action WorkspaceChanged { get; set; } = () => { };

    /// <summary>Allows a host to prevent Save As from colliding with another open document.</summary>
    public Func<string, bool> IsDocumentOpen { get; set; } = _ => false;
    /// <summary>Suggested directory or archive scope for semantic moves and deletions; the user reviews it.</summary>
    public Func<string>? RelocationScope { get; set; }
    /// <summary>Allows a host to require other in-scope documents to be saved and closed.</summary>
    public Action<AssetRelocationPlan>? ValidateRelocation { get; set; }
    /// <summary>Allows the host to protect other open authoring documents before deletion.</summary>
    public Action<AssetDeletionPlan>? ValidateDeletion { get; set; }
    private readonly BindingList<AssetRecord> _records = new();

    private readonly DataGridView _grid;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripComboBox _typeComboBox;
    private readonly ToolStripTextBox _searchTextBox;

    private readonly ToolStripButton _saveButton;
    private readonly ToolStripButton _saveAsButton;
    private readonly ToolStripButton _refreshButton;
    private readonly ToolStripButton _addButton;
    private readonly ToolStripButton _replaceButton;
    private readonly ToolStripButton _renameButton;
    private readonly ToolStripButton _exportButton;
    private readonly ToolStripButton _deleteButton;
    private EditorDockWorkspace _workspace = null!;

    /// <summary>Gets the mutable archive currently displayed; callers must not dispose it while the editor is using it.</summary>
    public AssetsFile AssetsFile => _assetsFile;
    /// <summary>Gets the full source path of the currently displayed archive.</summary>
    public string FilePath => Path.GetFullPath(_assetsFile.SourcePath!);

    /// <summary>Creates an editor for a loaded archive without taking ownership of that initial archive.</summary>
    /// <param name="assetsFile">The mutable archive to edit, with a source path assigned.</param>
    /// <exception cref="ArgumentNullException"><paramref name="assetsFile"/> is null.</exception>
    public AssetEditorControl(AssetsFile assetsFile)
    {
        _assetsFile = assetsFile ?? throw new ArgumentNullException(nameof(assetsFile));
        _edits = new(assetsFile, initiallySaved: File.Exists(assetsFile.SourcePath));
        var shortcuts = UndoRedoMenu.Route(this, () => Enabled ? History : null, () => Enabled);
        Disposed += (_, _) => shortcuts.Dispose();
        Size = new Size(1000, 650);
        Dock = DockStyle.Fill;

        var fileTools = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

        _saveButton = AddButton(fileTools, "Save", (_, _) => Save());
        _saveAsButton = AddButton(fileTools, "Save As…", (_, _) => SaveAs());
        fileTools.Items.Add(new ToolStripSeparator());
        _refreshButton = AddButton(fileTools, "Refresh", (_, _) => RefreshEntries());
        fileTools.Items.Add(new ToolStripSeparator());
        _addButton = AddButton(fileTools, "Add / Import", (_, _) => AddAsset());
        _replaceButton = AddButton(fileTools, "Replace", (_, _) => ReplaceSelectedAsset());
        _renameButton = AddButton(fileTools, "Rename / Move…", (_, _) => RenameSelectedAsset());
        _exportButton = AddButton(fileTools, "Export", (_, _) => ExportSelectedAsset());
        _deleteButton = AddButton(fileTools, "Delete", (_, _) => DeleteSelectedAsset());

        var filterTools = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

        filterTools.Items.Add(new ToolStripLabel("Type:"));
        _typeComboBox = new ToolStripComboBox
        {
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _typeComboBox.Items.Add("All Types");
        foreach (var value in Enum.GetValues<AssetTypes>())
            _typeComboBox.Items.Add(value);
        _typeComboBox.SelectedIndex = 0;
        _typeComboBox.SelectedIndexChanged += (_, _) => RefreshEntries();
        filterTools.Items.Add(_typeComboBox);

        filterTools.Items.Add(new ToolStripSeparator());
        filterTools.Items.Add(new ToolStripLabel("Search:"));
        _searchTextBox = new ToolStripTextBox
        {
            Width = 250,
            AutoSize = false
        };
        _searchTextBox.TextChanged += (_, _) => RefreshEntries();
        filterTools.Items.Add(_searchTextBox);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            DataSource = _records
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Type",
            DataPropertyName = nameof(AssetRecord.AssetType),
            Width = 120
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Name",
            DataPropertyName = nameof(AssetRecord.AssetName),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Size",
            DataPropertyName = nameof(AssetRecord.DisplaySize),
            Width = 120
        });

        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
                ExportSelectedAsset();
        };
        _grid.SelectionChanged += (_, _) => UpdateUiState();

        var statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel();
        statusStrip.Items.Add(_statusLabel);

        _workspace = new EditorDockWorkspace("gaf");
        Controls.Add(_workspace);
        var dock = _workspace.DockPanel;
        var entries = _workspace.AddPane("assets", "Assets", _grid, filterTools, fileTools);
        var status = _workspace.AddPane("status", "Status", statusStrip);
        _workspace.Place(entries, () => entries.Show(dock, DockState.Document));
        _workspace.Place(status, () => status.Show(entries.Pane, DockAlignment.Bottom, 55d / 650));
        _workspace.InitializeLayout();

        ToolingAppearance.Apply(this);
        _typeComboBox.BackColor = ToolingAppearance.Palette.Surface;
        _typeComboBox.ForeColor = ToolingAppearance.Palette.Foreground;
        _searchTextBox.BackColor = ToolingAppearance.Palette.Surface;
        _searchTextBox.ForeColor = ToolingAppearance.Palette.Foreground;

        History.Changed += HistoryChanged;
        RefreshEntries();
    }

    private void HistoryChanged(object? sender, EventArgs e)
    {
        RefreshEntries();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool ConfirmNonUndoable(long bytes) => ThemedMessageBox.Show(this,
        $"This edit needs {bytes / (1024d * 1024):F1} MiB of history, exceeding the {History.ByteLimit / (1024d * 1024):F1} MiB budget. " +
        "Continue without Undo? All existing Undo and Redo for this archive will be cleared after success. Cancel leaves the archive and history unchanged.",
        "Edit exceeds Undo budget", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    private static ToolStripButton AddButton(ToolStrip strip, string text, EventHandler onClick)
    {
        var button = new ToolStripButton(text);
        button.Click += onClick;
        strip.Items.Add(button);
        return button;
    }

    /// <summary>
    /// Shows the named editor pane.
    /// </summary>
    /// <param name="paneName">The name of the editor pane.</param>
    /// <returns><see langword="true"/> if the named pane was found; otherwise, <see langword="false"/>.</returns>
    public bool ShowPane(string paneName) =>
        _workspace.ShowPane(paneName);

    /// <summary>
    /// Determines whether the named editor pane is visible.
    /// </summary>
    /// <param name="paneName">The name of the editor pane.</param>
    /// <returns><see langword="true"/> if the pane is visible; otherwise, <see langword="false"/>.</returns>
    public bool IsPaneVisible(string paneName) =>
        _workspace.IsPaneVisible(paneName);

    /// <summary>Restore the default pane arrangement for this editor type.</summary>
    public void ResetLayout() => _workspace.ResetLayout();

    /// <summary>
    /// Shows every editor pane.
    /// </summary>
    public void ShowAllPanes() =>
        _workspace.ShowAllPanes();

    /// <summary>Routes Save to the host, or saves to the current path and displays standalone save failures in a dialog.</summary>
    public void Save()
    {
        if (SaveRequested is { } save) { save(false); return; }
        try
        {
            SaveTo(FilePath);
        }
        catch (Exception ex)
        {
            ShowError("Failed to save asset file.", ex);
        }
    }

    /// <summary>Routes Save As to the host, or prompts for a copy destination and encryption settings without adopting the copy.</summary>
    /// <remarks>The standalone flow retains the original path and dirty state and reports save failures in a dialog.</remarks>
    public void SaveAs()
    {
        if (SaveRequested is { } save) { save(true); return; }
        using var dialog = new SaveFileDialog
        {
            Title = "Save Asset File As",
            Filter = AssetFileFilter,
            DefaultExt = Path.GetExtension(FilePath),
            InitialDirectory = Path.GetDirectoryName(FilePath),
            FileName = Path.GetFileNameWithoutExtension(FilePath) + "-copy" + Path.GetExtension(FilePath)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var destination = Path.GetFullPath(dialog.FileName);
        if (IsDocumentOpen(destination))
        {
            ThemedMessageBox.Show(
                this,
                "Save As creates a copy. Choose a destination different from any currently open asset file.",
                "Choose Another Destination",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            var encrypt = ThemedMessageBox.Show(
                this,
                "Enable password protection for the saved copy?",
                "Encryption",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes;

            string? password = null;
            if (encrypt)
            {
                password = InputDialog.Show(this, "Password", "Enter password for the saved copy:");
                if (string.IsNullOrWhiteSpace(password))
                {
                    ThemedMessageBox.Show(
                        this,
                        "A password is required when encryption is enabled.",
                        "Password Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            SaveCopyTo(destination, password, encrypt);
            WorkspaceChanged();
            SetStatus($"Saved copy: {destination}");
        }
        catch (Exception ex)
        {
            ShowError("Failed to save copy of asset file.", ex);
        }
    }

    /// <summary>Saves a standalone copy without changing source identity, history, or checkpoint.</summary>
    public void SaveCopyTo(string destination, string? password, bool encrypt)
    {
        destination = Path.GetFullPath(destination);
        if (IsDocumentOpen(destination)) throw new InvalidOperationException("The copy destination is already open.");
        AssetArchiveCopy.Save(_assetsFile, destination, password, encrypt);
    }

    /// <summary>Notify the editor after a host edits its borrowed AssetsFile directly.</summary>
    public void MarkChanged()
    {
        History.MarkUntracked();
    }

    /// <summary>Accepts a host-persisted snapshot, clears the dirty state, refreshes entries, and raises <see cref="Changed"/>.</summary>
    /// <param name="replacement">A distinct saved archive whose ownership transfers to this control.</param>
    /// <remarks>Preserves the borrowed instance for same-path saves; owns snapshots adopted at new paths. External refreshes discard obsolete local history; ordinary saves preserve it.</remarks>
    public void AcceptSavedArchive(AssetsFile replacement, bool externalRefresh = false)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        if (History.IsExecuting || History.InTransaction) throw new InvalidOperationException("Archive history is busy.");
        if (ReferenceEquals(_assetsFile, replacement))
        {
            _edits.AcceptSavedArchive(_assetsFile, externalRefresh);
        }
        else if (string.Equals(FilePath, replacement.SourcePath, StringComparison.OrdinalIgnoreCase) &&
            _assetsFile.Password == replacement.Password && _assetsFile.UseEncryption == replacement.UseEncryption)
        {
            // Preserve borrowed-instance compatibility for same-path refreshes. Read everything
            // before mutation and compensate if copying fails; never advance a failed checkpoint.
            var before = AssetImportPlan.Snapshot(_assetsFile);
            var after = AssetImportPlan.Snapshot(replacement);
            void Copy(IReadOnlyDictionary<AssetsFileEntry, byte[]> contents)
            {
                foreach (var entry in _assetsFile.GetAllEntries().ToArray()) _assetsFile.Remove(entry.AssetType, entry.AssetName);
                foreach (var pair in contents)
                {
                    using var bytes = new MemoryStream(pair.Value, writable: false);
                    _assetsFile.Add(pair.Key.AssetType, pair.Key.AssetName, bytes);
                }
            }
            try { Copy(after); }
            catch { Copy(before); throw; }
            replacement.Dispose();
            _edits.AcceptSavedArchive(_assetsFile, externalRefresh);
        }
        else
        {
            var previous = _ownedSavedCopy;
            _ownedSavedCopy = replacement;
            _assetsFile = replacement;
            _edits.AcceptSavedArchive(replacement, externalRefresh);
            previous?.Dispose();
        }
        SetStatus($"Saved: {FilePath}");
    }

    /// <summary>
    /// Saves and adopts a new path for document hosts. Standalone Save As retains
    /// its save-copy behavior. The caller still owns the originally supplied file;
    /// the editor owns any replacement created here. Encryption is preserved.
    /// </summary>
    /// <param name="destination">The final archive path. A different path creates a copy and must not exist.</param>
    public void SaveTo(string destination)
    {
        if (SaveRequested is not null)
            throw new InvalidOperationException("This editor is hosted. Save through the host's asynchronous save command so archive writes are coordinated.");
        destination = Path.GetFullPath(destination);
        if (string.Equals(destination, FilePath, StringComparison.OrdinalIgnoreCase))
            _assetsFile.Save();
        else
        {
            AssetArchiveCopy.Save(_assetsFile, destination, _assetsFile.Password, _assetsFile.UseEncryption);
            var replacement = AssetsFile.LoadOrCreate(destination, _assetsFile.Password, _assetsFile.UseEncryption,
                register: AssetsFile.AllAssetsFiles.Contains(_assetsFile));
            _ownedSavedCopy?.Dispose();
            _ownedSavedCopy = replacement;
            _assetsFile = replacement;
        }

        _edits.AcceptSavedArchive(_assetsFile);
        WorkspaceChanged();
        SetStatus($"Saved: {FilePath}");
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SaveRequested = null;
            History.Changed -= HistoryChanged;
            _edits.Dispose();
            _ownedSavedCopy?.Dispose();
            _ownedSavedCopy = null;
        }
        base.Dispose(disposing);
    }

    /// <summary>Applies a reviewed plan to the current borrowed archive and marks successful additions dirty once.</summary>
    /// <param name="plan">The prepared plan for this archive instance.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <returns>The number of new entries.</returns>
    public int ApplyImport(AssetImportPlan plan, CancellationToken cancellation = default)
    {
        int count = _edits.Import(plan, ConfirmNonUndoable, cancellation);
        if (count > 0) SetStatus(History.CanUndo ? $"Imported {count} asset(s). Undo available; Save to persist." :
            $"Imported {count} asset(s) without Undo (history budget). Earlier history cleared; Save to persist.");
        else SetStatus("No new entries imported; archive and history unchanged.");
        return count;
    }

    /// <summary>Replaces an existing exact typed entry; suppresses identical payloads.</summary>
    public bool ReplaceAsset(AssetTypes type, string name, byte[] replacement)
    {
        bool changed = _edits.Replace(type, name, replacement, ConfirmNonUndoable);
        SetStatus(!changed ? "Replacement identical; archive and history unchanged." : History.CanUndo ?
            $"Replaced: {name}. Undo available; Save to persist." : $"Replaced: {name} without Undo (history budget). Earlier history cleared; Save to persist.");
        return changed;
    }

    private void AddAsset()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import Asset",
            Multiselect = true,
            Filter = "All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        using var typePicker = new AssetTypePickerForm(dialog.FileNames);
        if (typePicker.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var plan = AssetImportPlan.Create(_assetsFile, typePicker.Sources, typePicker.ImportDependencies);
            using var preview = new AssetImportPreviewForm(plan);
            if (preview.ShowDialog(this) != DialogResult.OK) return;
            ApplyImport(plan);
        }
        catch (OperationCanceledException) { SetStatus("Import cancelled; archive and history unchanged."); }
        catch (Exception ex)
        {
            ShowError("Failed to import one or more assets.", ex);
        }
    }

    private void ReplaceSelectedAsset()
    {
        var selected = GetSelectedRecord();
        if (selected is null)
            return;

        using var dialog = new OpenFileDialog
        {
            Title = $"Replace '{selected.AssetName}'",
            Filter = "All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            ReplaceAsset(selected.AssetType, selected.AssetName, File.ReadAllBytes(dialog.FileName));
        }
        catch (OperationCanceledException) { SetStatus("Replacement cancelled; archive and history unchanged."); }
        catch (Exception ex)
        {
            ShowError("Failed to replace asset.", ex);
        }
    }

    /// <summary>Commits a reviewed entry move after protecting dirty/open documents, then refreshes the borrowed archive.</summary>
    /// <param name="plan">Prepared move transaction.</param>
    public void ApplyRelocation(AssetRelocationPlan plan)
    {
        ValidateRelocation?.Invoke(plan);
        ValidateDiskOperation(plan.AffectedFiles, plan.RetiredFiles);
        plan.Commit();
        RefreshAfterDiskOperation("Move committed. In-scope references updated.");
    }

    /// <summary>Applies a reviewed safe or explicitly forced deletion and refreshes the same borrowed archive.</summary>
    /// <param name="plan">Prepared deletion transaction.</param>
    /// <param name="force">Explicit permission to break the listed surviving references.</param>
    /// <param name="cancellation">Cancellation token, honored through transaction retirement.</param>
    public void ApplyDeletion(AssetDeletionPlan plan, bool force = false, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        ValidateDeletion?.Invoke(plan);
        ValidateDiskOperation(plan.AffectedFiles, plan.RetiredFiles);
        plan.Commit(force, cancellation);
        RefreshAfterDiskOperation("Deletion committed. Only selected resources were removed.");
    }

    private void ValidateDiskOperation(IReadOnlyList<string> affectedFiles, IReadOnlyList<string> retiredFiles)
    {
        if (IsDirty) throw new InvalidOperationException("Save this archive before modifying resources.");
        if (retiredFiles.Contains(FilePath, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Close this archive and use the workspace or CLI to remove or move the whole GAF.");
        foreach (var path in affectedFiles)
            if (!string.Equals(path, FilePath, StringComparison.OrdinalIgnoreCase) && IsDocumentOpen(path))
                throw new InvalidOperationException($"Save and close affected document before modifying: {path}");
        using (var disk = AssetsFile.LoadOrCreate(FilePath, _assetsFile.Password, _assetsFile.UseEncryption, register: false))
        {
            var current = AssetImportPlan.Snapshot(_assetsFile);
            var saved = AssetImportPlan.Snapshot(disk);
            if (current.Count != saved.Count || current.Any(p => !saved.TryGetValue(p.Key, out var bytes) || !bytes.AsSpan().SequenceEqual(p.Value)))
                throw new InvalidOperationException("The open archive differs from disk. Save or reopen it before modifying.");
        }
    }

    private void RefreshAfterDiskOperation(string status)
    {
        using (var disk = AssetsFile.LoadOrCreate(FilePath, _assetsFile.Password, _assetsFile.UseEncryption, register: false))
        {
            foreach (var key in _assetsFile.GetAllEntries().ToArray()) _assetsFile.Remove(key.AssetType, key.AssetName);
            foreach (var pair in AssetImportPlan.Snapshot(disk))
            {
                using var stream = new MemoryStream(pair.Value, writable: false);
                _assetsFile.Add(pair.Key.AssetType, pair.Key.AssetName, stream);
            }
        }
        History.ResetSaved();
        WorkspaceChanged();
        SetStatus(status + " Committed disk operation; not locally undoable. Earlier history cleared.");
    }

    private void RenameSelectedAsset()
    {
        var selected = GetSelectedRecord();
        if (selected is null) return;
        if (IsDirty)
        {
            ThemedMessageBox.Show(this, "Save this archive before moving resources.", "Save required");
            return;
        }
        AssetMoveDialog.Show(this, new(selected.AssetType, FilePath, selected.AssetName),
            RelocationScope?.Invoke() ?? Path.GetDirectoryName(FilePath)!, ApplyRelocation,
            knownPasswords: new Dictionary<string, string?> { [FilePath] = _assetsFile.Password });
    }

    /// <summary>Exports a copy without changing archive ownership, dirty state, or notifications.</summary>
    /// <param name="entry">Exact typed entry.</param>
    /// <param name="destination">Final loose destination filename.</param>
    /// <param name="overwrite">Whether replacement of an existing loose file was authorized.</param>
    /// <param name="cancellation">Cancellation token.</param>
    internal void ExportAsset(AssetsFileEntry entry, string destination, bool overwrite, CancellationToken cancellation = default)
    {
        if (IsDirty && PackDefinitionDocument.IsNative(entry.AssetType))
            throw new InvalidOperationException("Save this archive before exporting a definition. Dependencies left packed must resolve from the saved archive.");
        SemanticUnpackPlan.CreateEntry(_assetsFile, entry, destination, overwrite, cancellation).Commit();
    }

    private void ExportSelectedAsset()
    {
        var selected = GetSelectedRecord();
        if (selected is null)
            return;

        if (IsDirty && PackDefinitionDocument.IsNative(selected.AssetType))
        {
            ThemedMessageBox.Show(this, "Save this archive before exporting a definition. Dependencies left packed must resolve from the saved archive.", "Save required");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = $"Export '{selected.AssetName}'",
            FileName = selected.AssetName,
            Filter = "All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            ExportAsset(new AssetsFileEntry { AssetType = selected.AssetType, AssetName = selected.AssetName },
                dialog.FileName, overwrite: true);
            SetStatus($"Exported: {selected.AssetName}");
        }
        catch (Exception ex)
        {
            ShowError("Failed to export asset.", ex);
        }
    }

    private void DeleteSelectedAsset()
    {
        var selected = GetSelectedRecord();
        if (selected is null) return;
        if (IsDirty)
        {
            ThemedMessageBox.Show(this, "Save this archive before deleting resources.", "Save required");
            return;
        }
        AssetDeleteDialog.Show(this, [new(selected.AssetType, FilePath, selected.AssetName)], [],
            RelocationScope?.Invoke() ?? Path.GetDirectoryName(FilePath)!, (plan, force) => ApplyDeletion(plan, force),
            new Dictionary<string, string?> { [FilePath] = _assetsFile.Password });
    }

    /// <summary>Rebuilds the entry list from the in-memory archive using the current type and search filters; does not reload the file from disk.</summary>
    public void RefreshEntries()
    {
        var selected = GetSelectedRecord();
        int oldRow = _grid.CurrentRow?.Index ?? 0;
        int scroll = _grid.FirstDisplayedScrollingRowIndex;
        _records.Clear();

        try
        {
            var selectedType = _typeComboBox.SelectedItem;
            var search = _searchTextBox.Text.Trim();

            foreach (var entry in _assetsFile.GetAllEntries()
                         .OrderBy(e => e.AssetType)
                         .ThenBy(e => e.AssetName))
            {
                if (selectedType is AssetTypes assetType && entry.AssetType != assetType)
                    continue;

                if (!string.IsNullOrWhiteSpace(search) &&
                    entry.AssetName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                long size = 0;
                using var stream = _assetsFile[entry.AssetType, entry.AssetName];
                if (stream is not null && stream.CanSeek)
                {
                    size = stream.Length;
                }
                else if (stream is not null)
                {
                    using var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    size = buffer.Length;
                }

                _records.Add(new AssetRecord
                {
                    AssetType = entry.AssetType,
                    AssetName = entry.AssetName,
                    SizeBytes = size
                });
            }

            SetStatus($"{FilePath} ({_records.Count} asset(s))");
        }
        catch (Exception ex)
        {
            ShowError("Failed to load asset entries.", ex);
        }

        if (_grid.Rows.Count > 0)
        {
            int index = _records.ToList().FindIndex(r => r.AssetType == selected?.AssetType && r.AssetName == selected.AssetName);
            _grid.CurrentCell = _grid.Rows[index >= 0 ? index : Math.Min(oldRow, _grid.Rows.Count - 1)].Cells[0];
            if (scroll >= 0) _grid.FirstDisplayedScrollingRowIndex = Math.Min(scroll, _grid.Rows.Count - 1);
        }
        UpdateUiState();
    }

    private AssetRecord? GetSelectedRecord() =>
        _grid.CurrentRow?.DataBoundItem as AssetRecord;

    private void UpdateUiState()
    {
        var hasSelection = GetSelectedRecord() is not null;

        _saveButton.Enabled = true;
        _saveAsButton.Enabled = true;
        _refreshButton.Enabled = true;
        _addButton.Enabled = true;
        _replaceButton.Enabled = hasSelection;
        _renameButton.Enabled = hasSelection;
        _exportButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    /// <inheritdoc/>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) =>
        UndoRedoMenu.TryShortcut(this, keyData, Enabled ? History : null, () => Enabled) || base.ProcessCmdKey(ref msg, keyData);

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private void ShowError(string message, Exception ex)
    {
        ThemedMessageBox.Show(
            this,
            message + Environment.NewLine + Environment.NewLine + ex.Message,
            "AssetFiles Editor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        SetStatus(message);
    }

}
