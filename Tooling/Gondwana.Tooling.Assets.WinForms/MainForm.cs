using Gondwana.Tooling.WinForms;
using Gondwana.Assets;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace Gondwana.Tooling.Assets.WinForms;

public sealed class MainForm : Form
{
    private readonly VS2015DarkTheme _theme = new();
    private readonly DockPanel _dock;
    private readonly DockLayoutPersistence _layout;
    private readonly PersistentDockContent _workspace = new("shell.workspace")
    {
        Text = "Asset files",
        HideOnClose = true
    };

    private readonly AssetWorkspaceControl _workspaceControl;
    private readonly List<AssetEditorDocument> _documents = [];

    private AssetEditorDocument? ActiveEditor =>
        _dock.ActiveDocument as AssetEditorDocument;

    private bool IsDocumentOpen(string path) =>
        _documents.Any(document =>
            string.Equals(
                document.FilePath,
                path,
                StringComparison.OrdinalIgnoreCase));

    public MainForm()
    {
        Text = "Gondwana Assets — GAF editor";
        Size = new Size(1450, 900);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        _workspaceControl = new AssetWorkspaceControl(
            Environment.CurrentDirectory);
        _workspaceControl.OpenAssetFileRequested += OpenDocument;
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
        Add(file, "&New", Keys.Control | Keys.N, CreateNewAssetsFile);
        Add(file, "&Open…", Keys.Control | Keys.O, OpenFiles);
        Add(
            file,
            "Open working &directory…",
            Keys.Control | Keys.Shift | Keys.O,
            () => _workspaceControl.ChooseDirectory(this));

        file.DropDownItems.Add(new ToolStripSeparator());

        Add(file, "&Save", Keys.Control | Keys.S, () => ActiveEditor?.Save());
        Add(
            file,
            "Save &As…",
            Keys.Control | Keys.Shift | Keys.S,
            () => ActiveEditor?.SaveAs());
        Add(file, "&Close", Keys.Control | Keys.W, () => ActiveEditor?.Close());

        file.DropDownItems.Add(new ToolStripSeparator());
        Add(file, "E&xit", Keys.Alt | Keys.F4, Close);

        var view = new ToolStripMenuItem("&View");
        var outerWorkspaceItem = Add(
            view,
            "Asset files",
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

        foreach (var paneName in AssetEditorControl.PaneNames)
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
            "Show all asset panes",
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
            Title = "Open Asset File",
            Filter = AssetWorkspaceControl.AssetFileFilter,
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
        path = Path.GetFullPath(path);

        var existing = _documents.FirstOrDefault(document =>
            string.Equals(
                document.FilePath,
                path,
                StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Activate();
            return;
        }

        AssetsFile? assetsFile = null;

        try
        {
            try
            {
                assetsFile = AssetsFile.LoadOrCreate(path);
                _ = assetsFile.GetAllEntries().ToList();
            }
            catch
            {
                assetsFile?.Dispose();
                assetsFile = null;

                var password = InputDialog.Show(
                    this,
                    "Password",
                    "Enter password for this asset file:");

                if (password is null)
                    return;

                assetsFile = AssetsFile.LoadOrCreate(
                    path,
                    password,
                    encrypt: true);

                _ = assetsFile.GetAllEntries().ToList();
            }

            ShowDocument(assetsFile);
            assetsFile = null;
        }
        catch (Exception ex)
        {
            ShowError("Failed to open asset file.", ex);
        }
        finally
        {
            assetsFile?.Dispose();
        }
    }

    private void CreateNewAssetsFile()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Create Asset File",
            Filter = AssetWorkspaceControl.AssetFileFilter,
            DefaultExt = "gaf",
            InitialDirectory = _workspaceControl.WorkingDirectory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var path = Path.GetFullPath(dialog.FileName);

        var existing = _documents.FirstOrDefault(document =>
            string.Equals(
                document.FilePath,
                path,
                StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Activate();
            return;
        }

        var encrypt = MessageBox.Show(
            this,
            "Enable password protection for this asset file?",
            "Encryption",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;

        string? password = null;

        if (encrypt)
        {
            password = InputDialog.Show(
                this,
                "Password",
                "Enter password for the new asset file:");

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show(
                    this,
                    "A password is required when encryption is enabled.",
                    "Password Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
        }

        AssetsFile? assetsFile = null;

        try
        {
            assetsFile = AssetsFile.LoadOrCreate(
                path,
                password,
                encrypt);

            assetsFile.Save();
            ShowDocument(assetsFile);
            assetsFile = null;

            _workspaceControl.SetWorkingDirectory(
                Path.GetDirectoryName(path) ??
                _workspaceControl.WorkingDirectory);
        }
        catch (Exception ex)
        {
            ShowError("Failed to create asset file.", ex);
        }
        finally
        {
            assetsFile?.Dispose();
        }
    }

    private AssetEditorDocument ShowDocument(
        AssetsFile assetsFile)
    {
        var editor = new AssetEditorDocument(
            assetsFile,
            _workspaceControl.RefreshDirectory,
            IsDocumentOpen);

        _documents.Add(editor);
        editor.FormClosed += (_, _) =>
            _documents.Remove(editor);

        editor.Show(_dock, DockState.Document);
        return editor;
    }

    private void ShowError(
        string message,
        Exception ex)
    {
        MessageBox.Show(
            this,
            message + Environment.NewLine + Environment.NewLine + ex.Message,
            "AssetFiles Editor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _layout.Dispose();
            foreach (var document in _documents.ToArray()) document.Dispose();
            _workspace.Dispose();
        }
        base.Dispose(disposing);
        if (disposing) _theme.Dispose();
    }
}
