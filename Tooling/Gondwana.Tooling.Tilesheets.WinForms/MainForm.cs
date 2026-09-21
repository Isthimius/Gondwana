using Gondwana.Tooling.WinForms;
using Gondwana.Tooling.Tilesheets.Editing;
using Gondwana.Tooling.Tilesheets.Sources;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace Gondwana.Tooling.Tilesheets.WinForms;

internal sealed class MainForm : Form
{
    private readonly VS2015DarkTheme _theme = new();
    private readonly DockPanel _dock;
    private readonly DockLayoutPersistence _layout;
    private readonly PersistentDockContent _workspace = new("shell.workspace")
    {
        Text = "Project sources",
        HideOnClose = true
    };

    private readonly AssetPackageCatalog _assetPackages = new();
    private readonly TilesheetWorkspaceControl _workspaceControl;
    private readonly List<EditorDocument> _documents = [];

    private EditorDocument? ActiveEditor =>
        _dock.ActiveDocument as EditorDocument;

    public MainForm()
    {
        Text = "Gondwana Tilesheets — GTS editor";
        Size = new Size(1450, 900);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        _assetPackages.PasswordProvider =
            path => PasswordPrompt.Show(this, path);

        _workspaceControl = new TilesheetWorkspaceControl(
            _assetPackages,
            Environment.CurrentDirectory);

        _workspaceControl.OpenGtsRequested += OpenDocument;
        _workspaceControl.CreateFromLooseImageRequested +=
            path => NewDocument(path);
        _workspaceControl.UseLooseImageRequested +=
            path => ActiveEditor?.ChooseImage(path);
        _workspaceControl.CreateFromPackedImageRequested +=
            source => NewDocument(source);
        _workspaceControl.UsePackedImageRequested +=
            source => ActiveEditor?.ChoosePackedImage(source);

        _workspace.Controls.Add(_workspaceControl);

        _dock = new DockPanel
        {
            Dock = DockStyle.Fill,
            Theme = _theme,
            DocumentStyle = DocumentStyle.DockingWindow
        };

        _layout = new DockLayoutPersistence(_dock, "shell");
        _layout.Register(_workspace, () => _workspace.Show(_dock, DockState.DockLeft));

        var menu = new MenuStrip();

        var file = new ToolStripMenuItem("&File");
        Add(file, "&New", Keys.Control | Keys.N, () => NewDocument());
        Add(file, "&Open GTS…", Keys.Control | Keys.O, OpenFiles);
        Add(
            file,
            "Open working &directory…",
            Keys.Control | Keys.Shift | Keys.O,
            () => _workspaceControl.ChooseDirectory(this));
        Add(
            file,
            "&Save",
            Keys.Control | Keys.S,
            () =>
            {
                if (ActiveEditor is { } document)
                    SaveDocument(document, false);
            });
        Add(
            file,
            "Save &As…",
            Keys.Control | Keys.Shift | Keys.S,
            () =>
            {
                if (ActiveEditor is { } document)
                    SaveDocument(document, true);
            });
        Add(file, "&Close", Keys.Control | Keys.W, () => ActiveEditor?.Close());
        Add(file, "E&xit", Keys.Alt | Keys.F4, Close);

        var view = new ToolStripMenuItem("&View");
        var outerWorkspaceItem = Add(
            view,
            "Project sources",
            Keys.None,
            () => _workspace.Show());
        Add(
            view,
            "Refresh directory",
            Keys.F5,
            _workspaceControl.RefreshDirectory);

        view.DropDownItems.Add(new ToolStripSeparator());
        var paneItems = new Dictionary<string, ToolStripMenuItem>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var paneName in TilesheetEditorControl.PaneNames)
        {
            string capturedName = paneName;
            paneItems[capturedName] = Add(
                view,
                capturedName,
                Keys.None,
                () => ActiveEditor?.ShowPane(capturedName));
        }

        view.DropDownItems.Add(new ToolStripSeparator());
        var showAllPanesItem = Add(
            view,
            "Show all tilesheet panes",
            Keys.None,
            () => ActiveEditor?.ShowAllPanes());

        Add(view, "Reset application layout", Keys.None, () => _layout.Reset());
        var resetEditor = Add(view, "Reset active editor layout", Keys.None, () => ActiveEditor?.Editor.ResetLayout());

        view.DropDownOpening += (_, _) =>
        {
            outerWorkspaceItem.Checked = !_workspace.IsHidden;
            var editor = ActiveEditor;
            foreach (var (paneName, item) in paneItems)
            {
                item.Enabled = editor is not null;
                item.Checked = editor?.IsPaneVisible(paneName) == true;
            }
            showAllPanesItem.Enabled = editor is not null;
            resetEditor.Enabled = editor is not null;
        };

        menu.Items.AddRange([file, view]);
        MainMenuStrip = menu;

        Controls.Add(_dock);
        Controls.Add(menu);

        DarkTheme.Apply(this);
        DarkTheme.Apply(_workspace);

        Shown += (_, _) =>
        {
            _workspace.Show(_dock, DockState.DockLeft);
            _layout.Start();
            _workspaceControl.RefreshDirectory();
        };

        FormClosing += (_, e) =>
        {
            foreach (var document in _documents.ToArray())
            {
                if (!document.ConfirmClose())
                {
                    e.Cancel = true;
                    return;
                }
            }

            foreach (var document in _documents)
                document.CloseApproved = true;
        };
    }

    private static ToolStripMenuItem Add(
        ToolStripMenuItem menu,
        string label,
        Keys shortcut,
        Action action)
    {
        var item = new ToolStripMenuItem(label)
        {
            ShortcutKeys = shortcut,
            ForeColor = DarkTheme.Foreground
        };

        item.Click += (_, _) => action();
        menu.DropDownItems.Add(item);
        return item;
    }

    private void OpenFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Gondwana tilesheets|*.gts",
            Multiselect = true,
            InitialDirectory = _workspaceControl.WorkingDirectory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        foreach (var path in dialog.FileNames)
            OpenDocument(path);
    }

    private void OpenDocument(string path)
    {
        try
        {
            path = Path.GetFullPath(path);

            var existing = _documents.FirstOrDefault(
                document => string.Equals(
                    document.Document.FilePath,
                    path,
                    StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                existing.Activate();
                return;
            }

            ShowDocument(TilesheetDocument.Open(path));
        }
        catch (Exception ex) when (
            ex is IOException or
            InvalidDataException or
            ArgumentException or
            UnauthorizedAccessException or
            InvalidOperationException or
            NotSupportedException)
        {
            ShowError(ex);
        }
    }

    private void NewDocument(string? imagePath = null)
    {
        var editor = ShowDocument(
            TilesheetDocument.Create(
                _workspaceControl.WorkingDirectory));

        if (imagePath is null)
            return;

        editor.Document.Definition.Name =
            Path.GetFileNameWithoutExtension(imagePath);
        editor.ChooseImage(imagePath);
        editor.AddRegion();
    }

    private void NewDocument(PackedImageSource source)
    {
        var editor = ShowDocument(
            TilesheetDocument.Create(
                _workspaceControl.WorkingDirectory));

        editor.Document.Definition.Name =
            Path.GetFileNameWithoutExtension(source.AssetEntryName);

        editor.ChoosePackedImage(source);
        editor.AddRegion();
    }

    private EditorDocument ShowDocument(
        TilesheetDocument document)
    {
        var editor = new EditorDocument(
            document,
            SaveDocument,
            assetPackages: _assetPackages,
            packedImagePicker: _ =>
                PackedImagePicker.Pick(
                    this,
                    _assetPackages,
                    _workspaceControl.WorkingDirectory));

        _documents.Add(editor);
        editor.FormClosed += (_, _) =>
            _documents.Remove(editor);

        editor.Show(_dock, DockState.Document);
        return editor;
    }

    private bool SaveDocument(
        EditorDocument editor,
        bool saveAs)
    {
        try
        {
            if (!editor.CommitEdits())
                return false;

            editor.RefreshView();

            string? path = editor.Document.FilePath;

            if (saveAs || path is null)
            {
                using var dialog = new SaveFileDialog
                {
                    Filter = "Gondwana tilesheets|*.gts",
                    DefaultExt = "gts",
                    AddExtension = true,
                    InitialDirectory = editor.Document.BaseDirectory,
                    FileName = path is null
                        ? "Untitled.gts"
                        : Path.GetFileName(path)
                };

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return false;

                path = dialog.FileName;
            }

            if (_documents.Any(document =>
                    document != editor &&
                    string.Equals(
                        document.Document.FilePath,
                        Path.GetFullPath(path),
                        StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "That destination is already open in another document. Close it before overwriting it.");
            }

            var errors = editor.UpdateValidation();
            bool allowInvalid = errors.Count > 0;

            if (allowInvalid &&
                MessageBox.Show(
                    this,
                    "This definition has validation errors. See the validation panel. Save this invalid definition anyway?",
                    "Validation failed",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) !=
                DialogResult.Yes)
            {
                return false;
            }

            editor.Document.Save(
                path,
                editor.ImageSize,
                allowInvalid);

            editor.RefreshView();
            _workspaceControl.RefreshDirectory();
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            ArgumentException or
            UnauthorizedAccessException or
            InvalidOperationException or
            NotSupportedException)
        {
            ShowError(ex);
            return false;
        }
    }

    private void ShowError(Exception ex) =>
        MessageBox.Show(
            this,
            ex.Message,
            "GTS editor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _layout.Dispose();
            _workspace.Dispose();
            _assetPackages.Dispose();
            _dock.Dispose();
            _theme.Dispose();
        }

        base.Dispose(disposing);
    }
}
