using Gondwana.Tooling.Scenes.Editing;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace Gondwana.Tooling.Scenes.WinForms;

internal sealed class MainForm : Form
{
    private readonly VS2015DarkTheme _theme = new();
    private readonly DockPanel _dock;
    private readonly DockContent _workspace = new()
    {
        Text = "Working directory",
        HideOnClose = true
    };

    private readonly TreeView _tree = new()
    {
        Dock = DockStyle.Fill,
        HideSelection = false,
        ShowNodeToolTips = true
    };

    private readonly List<SceneEditorDocument> _documents = [];
    private string _directory = Environment.CurrentDirectory;

    private SceneEditorDocument? ActiveEditor =>
        _dock.ActiveDocument as SceneEditorDocument;

    public MainForm()
    {
        Text = "Gondwana Scenes — GSCN editor";
        Size = new Size(1500, 950);
        MinimumSize = new Size(1000, 650);
        StartPosition = FormStartPosition.CenterScreen;

        _dock = new DockPanel
        {
            Dock = DockStyle.Fill,
            Theme = _theme,
            DocumentStyle = DocumentStyle.DockingWindow
        };

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        Add(file, "&New", Keys.Control | Keys.N, NewDocument);
        Add(file, "&Open GSCN…", Keys.Control | Keys.O, OpenFiles);
        Add(file, "Add &GTS…", Keys.Control | Keys.Shift | Keys.T, ChooseTilesheets);
        Add(file, "Add G&ANI…", Keys.Control | Keys.Shift | Keys.A, ChooseAnimations);
        Add(file, "Open working &directory…", Keys.None, ChooseDirectory);
        Add(file, "&Save", Keys.Control | Keys.S, () =>
        {
            if (ActiveEditor is { } document)
                SaveDocument(document, saveAs: false);
        });
        Add(file, "Save &As…", Keys.Control | Keys.Shift | Keys.S, () =>
        {
            if (ActiveEditor is { } document)
                SaveDocument(document, saveAs: true);
        });
        Add(file, "&Close", Keys.Control | Keys.W, () => ActiveEditor?.Close());
        Add(file, "E&xit", Keys.Alt | Keys.F4, Close);

        var view = new ToolStripMenuItem("&View");
        var workingDirectoryItem = Add(
            view,
            "Working directory",
            Keys.None,
            () => _workspace.Show(_dock, DockState.DockLeft));

        Add(view, "Refresh directory", Keys.F5, RefreshDirectory);
        view.DropDownItems.Add(new ToolStripSeparator());

        var paneItems = new Dictionary<string, ToolStripMenuItem>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var paneName in SceneEditorControl.PaneNames)
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
            "Show all scene panes",
            Keys.None,
            () => ActiveEditor?.ShowAllPanes());

        view.DropDownOpening += (_, _) =>
        {
            workingDirectoryItem.Checked = !_workspace.IsHidden;

            var editor = ActiveEditor;
            foreach (var (paneName, item) in paneItems)
            {
                item.Enabled = editor is not null;
                item.Checked =
                    editor?.IsPaneVisible(paneName) == true;
            }

            showAllPanesItem.Enabled = editor is not null;
        };

        menu.Items.AddRange([file, view]);
        MainMenuStrip = menu;

        Controls.Add(_dock);
        Controls.Add(menu);

        var tools = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };
        tools.Items.Add("Directory…", null, (_, _) => ChooseDirectory());
        tools.Items.Add("Refresh", null, (_, _) => RefreshDirectory());
        _workspace.Controls.Add(_tree);
        _workspace.Controls.Add(tools);

        _tree.BeforeExpand += (_, e) =>
        {
            if (e.Node is { Tag: DirectoryInfo directory } node &&
                node.Nodes.Count == 1 &&
                node.Nodes[0].Tag is null)
            {
                FillDirectory(node, directory.FullName);
            }
        };

        _tree.NodeMouseDoubleClick += (_, e) =>
        {
            if (e.Node.Tag is not string path)
                return;

            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".gscn":
                    OpenDocument(path);
                    break;
                case ".gts":
                    EnsureEditor().AddTilesheetSources([path]);
                    break;
                case ".gani":
                    EnsureEditor().AddAnimationSources([path]);
                    break;
            }
        };

        DarkTheme.Apply(this);
        DarkTheme.Apply(_workspace);

        Shown += (_, _) =>
        {
            _workspace.Show(_dock, DockState.DockLeft);
            RefreshDirectory();
            NewDocument();
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

    private void ChooseDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the scene working directory",
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

    private static void FillDirectory(TreeNode parent, string path)
    {
        parent.Nodes.Clear();

        try
        {
            foreach (var directory in new DirectoryInfo(path)
                         .EnumerateDirectories()
                         .OrderBy(directory => directory.Name))
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                var node = new TreeNode(directory.Name) { Tag = directory };
                node.Nodes.Add("Expand to load…");
                parent.Nodes.Add(node);
            }

            foreach (var file in Directory.EnumerateFiles(path)
                         .Where(IsAuthoringFile)
                         .OrderBy(Path.GetFileName))
            {
                parent.Nodes.Add(
                    new TreeNode(Path.GetFileName(file))
                    {
                        Tag = file,
                        ToolTipText = file
                    });
            }
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            parent.Nodes.Add("Cannot read directory: " + ex.Message);
        }
    }

    private static bool IsAuthoringFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".gscn", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gts", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gani", StringComparison.OrdinalIgnoreCase);
    }

    private void OpenFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Gondwana scenes|*.gscn",
            Multiselect = true,
            InitialDirectory = _directory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        foreach (var path in dialog.FileNames)
            OpenDocument(path);
    }

    private void ChooseTilesheets()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Gondwana tilesheets|*.gts",
            Multiselect = true,
            InitialDirectory = _directory
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            EnsureEditor().AddTilesheetSources(dialog.FileNames);
    }

    private void ChooseAnimations()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Gondwana animations|*.gani",
            Multiselect = true,
            InitialDirectory = _directory
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            EnsureEditor().AddAnimationSources(dialog.FileNames);
    }

    private SceneEditorDocument EnsureEditor()
    {
        if (ActiveEditor is { } active)
            return active;

        NewDocument();
        return ActiveEditor!;
    }

    private void OpenDocument(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            var existing = _documents.FirstOrDefault(document =>
                string.Equals(
                    document.Document.FilePath,
                    path,
                    StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                existing.Activate();
                return;
            }

            ShowDocument(SceneDocument.Open(path));
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

    private void NewDocument() =>
        ShowDocument(SceneDocument.Create(_directory));

    private SceneEditorDocument ShowDocument(SceneDocument document)
    {
        var editor = new SceneEditorDocument(document, SaveDocument);
        _documents.Add(editor);
        editor.FormClosed += (_, _) => _documents.Remove(editor);
        editor.Show(_dock, DockState.Document);
        return editor;
    }

    private bool SaveDocument(SceneEditorDocument editor, bool saveAs)
    {
        try
        {
            if (!editor.CommitEdits())
                return false;

            string? path = editor.Document.FilePath;
            if (saveAs || path is null)
            {
                using var dialog = new SaveFileDialog
                {
                    Filter = "Gondwana scenes|*.gscn",
                    DefaultExt = "gscn",
                    AddExtension = true,
                    InitialDirectory = editor.Document.BaseDirectory,
                    FileName = path is null
                        ? "scene.gscn"
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
                    "This definition has validation errors. See the Validation pane. Save this invalid definition anyway?",
                    "Validation failed",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return false;
            }

            editor.Document.Save(path, allowInvalid);
            RefreshDirectory();
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
            "GSCN editor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _workspace.Dispose();
            _dock.Dispose();
            _theme.Dispose();
        }

        base.Dispose(disposing);
    }
}
