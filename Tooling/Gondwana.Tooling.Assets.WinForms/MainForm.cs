using Gondwana.Assets;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace Gondwana.Tooling.Assets.WinForms;

public sealed class MainForm : Form
{
    private const string AssetFileFilter = "Asset Files (*.gaf;*.zip)|*.gaf;*.zip|All Files (*.*)|*.*";
    private static readonly HashSet<string> AssetFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".gaf", ".zip" };

    private readonly VS2015DarkTheme _theme = new();
    private readonly DockPanel _dock;
    private readonly DockContent _workspace = new() { Text = "Asset files", HideOnClose = true };
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false, ShowNodeToolTips = true };
    private readonly List<AssetEditorDocument> _documents = [];

    private string _directory = Environment.CurrentDirectory;
    private AssetEditorDocument? ActiveEditor => _dock.ActiveDocument as AssetEditorDocument;
    private bool IsDocumentOpen(string path) =>
        _documents.Any(document => string.Equals(document.FilePath, path, StringComparison.OrdinalIgnoreCase));

    public MainForm()
    {
        Text = "Gondwana Assets — GAF editor";
        Size = new Size(1450, 900);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        _dock = new DockPanel
        {
            Dock = DockStyle.Fill,
            Theme = _theme,
            DocumentStyle = DocumentStyle.DockingWindow
        };

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        Add(file, "&New", Keys.Control | Keys.N, CreateNewAssetsFile);
        Add(file, "&Open…", Keys.Control | Keys.O, OpenFiles);
        Add(file, "Open working &directory…", Keys.Control | Keys.Shift | Keys.O, ChooseDirectory);
        file.DropDownItems.Add(new ToolStripSeparator());
        Add(file, "&Save", Keys.Control | Keys.S, () => ActiveEditor?.Save());
        Add(file, "Save &As…", Keys.Control | Keys.Shift | Keys.S, () => ActiveEditor?.SaveAs());
        Add(file, "&Close", Keys.Control | Keys.W, () => ActiveEditor?.Close());
        file.DropDownItems.Add(new ToolStripSeparator());
        Add(file, "E&xit", Keys.Alt | Keys.F4, Close);

        var view = new ToolStripMenuItem("&View");
        Add(view, "Asset files", Keys.None, () => _workspace.Show(_dock, DockState.DockLeft));
        Add(view, "Refresh directory", Keys.F5, RefreshDirectory);

        menu.Items.AddRange([file, view]);
        MainMenuStrip = menu;

        Controls.Add(_dock);
        Controls.Add(menu);

        var workspaceTools = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };
        workspaceTools.Items.Add("Directory…", null, (_, _) => ChooseDirectory());
        workspaceTools.Items.Add("Refresh", null, (_, _) => RefreshDirectory());

        _workspace.Controls.Add(_tree);
        _workspace.Controls.Add(workspaceTools);

        _tree.BeforeExpand += (_, e) =>
        {
            if (e.Node is { Tag: DirectoryInfo directory } node &&
                node.Nodes.Count == 1 &&
                node.Nodes[0].Tag is null)
            {
                FillDirectory(node, directory.FullName);
            }
        };

        _tree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is string path && IsAssetFile(path))
                OpenDocument(path);
        };

        _tree.NodeMouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
                _tree.SelectedNode = e.Node;
        };

        var context = new ContextMenuStrip();
        context.Items.Add("Open asset file", null, (_, _) =>
        {
            if (_tree.SelectedNode?.Tag is string path && IsAssetFile(path))
                OpenDocument(path);
        });
        _tree.ContextMenuStrip = context;

        DarkTheme.Apply(this);
        DarkTheme.Apply(_workspace);
        DarkTheme.Apply(context);

        Shown += (_, _) =>
        {
            _workspace.Show(_dock, DockState.DockLeft);
            RefreshDirectory();
        };
    }

    private static void Add(ToolStripMenuItem menu, string label, Keys shortcut, Action action)
    {
        var item = new ToolStripMenuItem(label)
        {
            ShortcutKeys = shortcut,
            ForeColor = DarkTheme.Foreground
        };
        item.Click += (_, _) => action();
        menu.DropDownItems.Add(item);
    }

    private void ChooseDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the asset-file working directory",
            UseDescriptionForTitle = true,
            SelectedPath = _directory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _directory = dialog.SelectedPath;
        RefreshDirectory();
    }

    private void RefreshDirectory()
    {
        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            var root = new TreeNode(_directory)
            {
                Tag = new DirectoryInfo(_directory),
                ToolTipText = _directory
            };
            _tree.Nodes.Add(root);
            FillDirectory(root, _directory);
            root.Expand();
        }
        finally
        {
            _tree.EndUpdate();
        }
    }

    private static bool IsAssetFile(string path) =>
        AssetFileExtensions.Contains(Path.GetExtension(path));

    private static void FillDirectory(TreeNode parent, string path)
    {
        parent.Nodes.Clear();

        try
        {
            foreach (var directory in new DirectoryInfo(path).EnumerateDirectories().OrderBy(d => d.Name))
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                var node = new TreeNode(directory.Name)
                {
                    Tag = directory,
                    ToolTipText = directory.FullName
                };
                node.Nodes.Add("Expand to load…");
                parent.Nodes.Add(node);
            }

            foreach (var file in Directory.EnumerateFiles(path)
                         .Where(IsAssetFile)
                         .OrderBy(Path.GetFileName))
            {
                parent.Nodes.Add(new TreeNode(Path.GetFileName(file))
                {
                    Tag = file,
                    ToolTipText = file
                });
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            parent.Nodes.Add("Cannot read directory: " + ex.Message);
        }
    }

    private void OpenFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open Asset File",
            Filter = AssetFileFilter,
            Multiselect = true,
            InitialDirectory = _directory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        foreach (var path in dialog.FileNames)
            OpenDocument(path);
    }

    private void OpenDocument(string path)
    {
        path = Path.GetFullPath(path);

        var existing = _documents.FirstOrDefault(d =>
            string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
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

                var password = InputDialog.Show(this, "Password", "Enter password for this asset file:");
                if (password is null)
                    return;

                assetsFile = AssetsFile.LoadOrCreate(path, password, encrypt: true);
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
            Filter = AssetFileFilter,
            DefaultExt = "gaf",
            InitialDirectory = _directory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var path = Path.GetFullPath(dialog.FileName);
        var existing = _documents.FirstOrDefault(d =>
            string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
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
            password = InputDialog.Show(this, "Password", "Enter password for the new asset file:");
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
            assetsFile = AssetsFile.LoadOrCreate(path, password, encrypt);
            assetsFile.Save();
            ShowDocument(assetsFile);
            assetsFile = null;

            _directory = Path.GetDirectoryName(path) ?? _directory;
            RefreshDirectory();
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

    private AssetEditorDocument ShowDocument(AssetsFile assetsFile)
    {
        var editor = new AssetEditorDocument(assetsFile, RefreshDirectory, IsDocumentOpen);
        _documents.Add(editor);
        editor.FormClosed += (_, _) => _documents.Remove(editor);
        editor.Show(_dock, DockState.Document);
        return editor;
    }

    private void ShowError(string message, Exception ex)
    {
        MessageBox.Show(
            this,
            message + Environment.NewLine + Environment.NewLine + ex.Message,
            "AssetFiles Editor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        foreach (var document in _documents.ToArray())
            document.Dispose();

        _workspace.Dispose();
        _theme.Dispose();
        base.OnFormClosed(e);
    }
}
