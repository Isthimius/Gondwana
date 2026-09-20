using Gondwana.Tooling.Audio.Editing;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace Gondwana.Tooling.Audio.WinForms;

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

    private readonly List<AudioEditorDocument> _documents = [];
    private string _directory = Environment.CurrentDirectory;

    private AudioEditorDocument? ActiveEditor =>
        _dock.ActiveDocument as AudioEditorDocument;

    public MainForm()
    {
        Text = "Gondwana Audio — GSND editor";
        Size = new Size(1350, 850);
        MinimumSize = new Size(850, 550);
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
        Add(file, "&Open GSND…", Keys.Control | Keys.O, OpenFiles);
        Add(file, "Add audio &file…", Keys.Control | Keys.Shift | Keys.O, ChooseAudioFiles);
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
        Add(view, "Working directory", Keys.None, () =>
            _workspace.Show(_dock, DockState.DockLeft));
        Add(view, "Refresh directory", Keys.F5, RefreshDirectory);

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

            if (Path.GetExtension(path).Equals(".gsnd", StringComparison.OrdinalIgnoreCase))
                OpenDocument(path);
            else if (IsAudioFile(path))
                EnsureEditor().AddLooseFiles([path]);
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

    private static void Add(
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
    }

    private void ChooseDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the audio working directory",
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
                         .Where(file =>
                             Path.GetExtension(file).Equals(".gsnd", StringComparison.OrdinalIgnoreCase) ||
                             IsAudioFile(file))
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

    private static bool IsAudioFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".flac", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".aif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".aiff", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".wma", StringComparison.OrdinalIgnoreCase);
    }

    private void OpenFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Gondwana audio definitions|*.gsnd",
            Multiselect = true,
            InitialDirectory = _directory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        foreach (var path in dialog.FileNames)
            OpenDocument(path);
    }

    private void ChooseAudioFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Audio files|*.wav;*.mp3;*.ogg;*.flac;*.aif;*.aiff;*.wma|All files|*.*",
            Multiselect = true,
            InitialDirectory = _directory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        EnsureEditor().AddLooseFiles(dialog.FileNames);
    }

    private AudioEditorDocument EnsureEditor()
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

            ShowDocument(AudioDocument.Open(path));
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
        ShowDocument(AudioDocument.Create(_directory));

    private AudioEditorDocument ShowDocument(AudioDocument document)
    {
        var editor = new AudioEditorDocument(document, SaveDocument);
        _documents.Add(editor);
        editor.FormClosed += (_, _) => _documents.Remove(editor);
        editor.Show(_dock, DockState.Document);
        return editor;
    }

    private bool SaveDocument(AudioEditorDocument editor, bool saveAs)
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
                    Filter = "Gondwana audio definitions|*.gsnd",
                    DefaultExt = "gsnd",
                    AddExtension = true,
                    InitialDirectory = editor.Document.BaseDirectory,
                    FileName = path is null ? "audio.gsnd" : Path.GetFileName(path)
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
            "GSND editor",
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
