using Gondwana.Tooling.Animations.Editing;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace Gondwana.Tooling.Animations.WinForms;

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

    private readonly List<AnimationEditorDocument> _documents = [];
    private string _directory = Environment.CurrentDirectory;

    private AnimationEditorDocument? ActiveEditor =>
        _dock.ActiveDocument as AnimationEditorDocument;

    public MainForm()
    {
        Text = "Gondwana Animations — GANI editor";
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
        Add(file, "&New", Keys.Control | Keys.N, NewDocument);
        Add(file, "&Open GANI…", Keys.Control | Keys.O, OpenFiles);
        Add(file, "Add &GTS source…", Keys.Control | Keys.Shift | Keys.O, ChooseGtsSources);
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
        Add(file, "&Close", Keys.Control | Keys.W, () =>
            ActiveEditor?.Close());
        Add(file, "E&xit", Keys.Alt | Keys.F4, Close);

        var view = new ToolStripMenuItem("&View");
        var outerWorkspaceItem = Add(
            view,
            "Working directory",
            Keys.None,
            () => _workspace.Show(_dock, DockState.DockLeft));
        Add(view, "Refresh directory", Keys.F5, RefreshDirectory);

        view.DropDownItems.Add(new ToolStripSeparator());
        var paneItems = new Dictionary<string, ToolStripMenuItem>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var paneName in AnimationEditorControl.PaneNames)
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
            "Show all animation panes",
            Keys.None,
            () => ActiveEditor?.ShowAllPanes());

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
        };

        menu.Items.AddRange([file, view]);
        MainMenuStrip = menu;

        Controls.Add(_dock);
        Controls.Add(menu);

        var workspaceTools = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

        workspaceTools.Items.Add(
            "Directory…",
            null,
            (_, _) => ChooseDirectory());
        workspaceTools.Items.Add(
            "Refresh",
            null,
            (_, _) => RefreshDirectory());

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

        _tree.NodeMouseDoubleClick += (_, e) =>
        {
            if (e.Node.Tag is not string path)
                return;

            var extension = Path.GetExtension(path);

            if (extension.Equals(
                    ".gani",
                    StringComparison.OrdinalIgnoreCase))
            {
                OpenDocument(path);
            }
            else if (extension.Equals(
                         ".gts",
                         StringComparison.OrdinalIgnoreCase))
            {
                EnsureEditor().AddTilesheetSources([path]);
            }
        };

        var context = new ContextMenuStrip();
        context.Items.Add(
            "Open GANI",
            null,
            (_, _) =>
            {
                if (_tree.SelectedNode?.Tag is string path &&
                    Path.GetExtension(path).Equals(
                        ".gani",
                        StringComparison.OrdinalIgnoreCase))
                {
                    OpenDocument(path);
                }
            });

        context.Items.Add(
            "Add GTS to active animation",
            null,
            (_, _) =>
            {
                if (_tree.SelectedNode?.Tag is string path &&
                    Path.GetExtension(path).Equals(
                        ".gts",
                        StringComparison.OrdinalIgnoreCase))
                {
                    EnsureEditor().AddTilesheetSources([path]);
                }
            });

        _tree.NodeMouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
                _tree.SelectedNode = e.Node;
        };

        _tree.ContextMenuStrip = context;

        DarkTheme.Apply(this);
        DarkTheme.Apply(_workspace);
        DarkTheme.Apply(context);

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
            Description = "Choose the animation working directory",
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

    private static void FillDirectory(
        TreeNode parent,
        string path)
    {
        parent.Nodes.Clear();

        try
        {
            foreach (var directory in new DirectoryInfo(path)
                         .EnumerateDirectories()
                         .OrderBy(directory => directory.Name))
            {
                if ((directory.Attributes &
                     FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                var node = new TreeNode(directory.Name)
                {
                    Tag = directory
                };

                node.Nodes.Add("Expand to load…");
                parent.Nodes.Add(node);
            }

            foreach (var file in Directory.EnumerateFiles(path)
                         .Where(file =>
                         {
                             var extension = Path.GetExtension(file);
                             return extension.Equals(
                                        ".gani",
                                        StringComparison.OrdinalIgnoreCase) ||
                                    extension.Equals(
                                        ".gts",
                                        StringComparison.OrdinalIgnoreCase);
                         })
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
            ex is IOException or
            UnauthorizedAccessException)
        {
            parent.Nodes.Add(
                "Cannot read directory: " + ex.Message);
        }
    }

    private void OpenFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Gondwana animations|*.gani",
            Multiselect = true,
            InitialDirectory = _directory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        foreach (var path in dialog.FileNames)
            OpenDocument(path);
    }

    private void ChooseGtsSources()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Gondwana tilesheets|*.gts",
            Multiselect = true,
            InitialDirectory = _directory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            EnsureEditor().AddTilesheetSources(
                dialog.FileNames);
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

    private AnimationEditorDocument EnsureEditor()
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

            ShowDocument(AnimationDocument.Open(path));
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

    private void NewDocument()
    {
        ShowDocument(
            AnimationDocument.Create(_directory));
    }

    private AnimationEditorDocument ShowDocument(
        AnimationDocument document)
    {
        var editor = new AnimationEditorDocument(
            document,
            SaveDocument);

        _documents.Add(editor);
        editor.FormClosed += (_, _) =>
            _documents.Remove(editor);

        editor.Show(_dock, DockState.Document);
        return editor;
    }

    private bool SaveDocument(
        AnimationEditorDocument editor,
        bool saveAs)
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
                    Filter = "Gondwana animations|*.gani",
                    DefaultExt = "gani",
                    AddExtension = true,
                    InitialDirectory =
                        editor.Document.BaseDirectory,
                    FileName = path is null
                        ? SanitizeFileName(
                            editor.Document.Definition.Key) +
                          ".gani"
                        : Path.GetFileName(path)
                };

                if (dialog.ShowDialog(this) !=
                    DialogResult.OK)
                {
                    return false;
                }

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
                allowInvalid);

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

    private static string SanitizeFileName(string value)
    {
        var text = string.IsNullOrWhiteSpace(value)
            ? "Untitled"
            : value;

        var invalid = Path.GetInvalidFileNameChars();
        return new string(
            text.Select(character =>
                    invalid.Contains(character)
                        ? '_'
                        : character)
                .ToArray());
    }

    private void ShowError(Exception ex) =>
        MessageBox.Show(
            this,
            ex.Message,
            "GANI editor",
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
