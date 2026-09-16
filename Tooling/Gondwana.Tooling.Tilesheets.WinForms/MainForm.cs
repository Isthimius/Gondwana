using Gondwana.Tooling.Tilesheets.Editing;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace Gondwana.Tooling.Tilesheets.WinForms;

internal sealed class MainForm : Form
{
    internal const string ImageFilter = "Images|*.png;*.bmp;*.jpg;*.jpeg;*.gif;*.webp;*.ico;*.wbmp;*.avif;*.heif;*.heic;*.dng;*.ktx;*.astc;*.pkm|All files|*.*";
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".bmp", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".wbmp", ".avif", ".heif", ".heic", ".dng", ".ktx", ".astc", ".pkm" };
    private readonly VS2015DarkTheme _theme = new();
    private readonly DockPanel _dock;
    private readonly DockContent _workspace = new() { Text = "Working directory", HideOnClose = true };
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false, ShowNodeToolTips = true };
    private readonly List<EditorDocument> _documents = [];
    private string _directory = Environment.CurrentDirectory;
    private EditorDocument? ActiveEditor => _dock.ActiveDocument as EditorDocument;

    public MainForm()
    {
        Text = "Gondwana Tilesheets — GTS editor";
        Size = new Size(1450, 900);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        _dock = new DockPanel { Dock = DockStyle.Fill, Theme = _theme, DocumentStyle = DocumentStyle.DockingWindow };
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        Add(file, "&New", Keys.Control | Keys.N, () => NewDocument());
        Add(file, "&Open GTS…", Keys.Control | Keys.O, OpenFiles);
        Add(file, "Open working &directory…", Keys.Control | Keys.Shift | Keys.O, ChooseDirectory);
        Add(file, "&Save", Keys.Control | Keys.S, () => { if (ActiveEditor is { } doc) SaveDocument(doc, false); });
        Add(file, "Save &As…", Keys.Control | Keys.Shift | Keys.S, () => { if (ActiveEditor is { } doc) SaveDocument(doc, true); });
        Add(file, "&Close", Keys.Control | Keys.W, () => ActiveEditor?.Close());
        Add(file, "E&xit", Keys.Alt | Keys.F4, Close);
        var view = new ToolStripMenuItem("&View");
        Add(view, "Working directory", Keys.None, () => _workspace.Show(_dock, DockState.DockLeft));
        Add(view, "Refresh directory", Keys.F5, RefreshDirectory);
        menu.Items.AddRange([file, view]);
        MainMenuStrip = menu;
        Controls.Add(_dock);
        Controls.Add(menu);
        var workspaceTools = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        workspaceTools.Items.Add("Directory…", null, (_, _) => ChooseDirectory());
        workspaceTools.Items.Add("Refresh", null, (_, _) => RefreshDirectory());
        _workspace.Controls.Add(_tree);
        _workspace.Controls.Add(workspaceTools);
        _tree.BeforeExpand += (_, e) =>
        {
            if (e.Node is { Tag: DirectoryInfo directory } node && node.Nodes.Count == 1 && node.Nodes[0].Tag is null)
                FillDirectory(node, directory.FullName);
        };
        _tree.AfterSelect += (_, e) => { if (e.Node?.Tag is string path && Path.GetExtension(path).Equals(".gts", StringComparison.OrdinalIgnoreCase)) OpenDocument(path); };
        _tree.NodeMouseDoubleClick += (_, e) => { if (e.Node.Tag is string path && IsImage(path)) NewDocument(path); };
        var context = new ContextMenuStrip();
        context.Items.Add("Create GTS from image", null, (_, _) => { if (_tree.SelectedNode?.Tag is string p && IsImage(p)) NewDocument(p); });
        context.Items.Add("Use image in active document", null, (_, _) =>
        {
            if (_tree.SelectedNode?.Tag is string p && IsImage(p)) ActiveEditor?.ChooseImage(p);
        });
        _tree.NodeMouseClick += (_, e) => { if (e.Button == MouseButtons.Right) _tree.SelectedNode = e.Node; };
        _tree.ContextMenuStrip = context;
        DarkTheme.Apply(this);
        DarkTheme.Apply(_workspace);
        DarkTheme.Apply(context);
        Shown += (_, _) => { _workspace.Show(_dock, DockState.DockLeft); RefreshDirectory(); };
        FormClosing += (_, e) =>
        {
            // Ask all documents first; a cancelled exit leaves every document open.
            foreach (var doc in _documents.ToArray())
                if (!doc.ConfirmClose()) { e.Cancel = true; return; }
            foreach (var doc in _documents) doc.CloseApproved = true;
        };
    }

    private static void Add(ToolStripMenuItem menu, string label, Keys shortcut, Action action)
    {
        var item = new ToolStripMenuItem(label) { ShortcutKeys = shortcut, ForeColor = DarkTheme.Foreground };
        item.Click += (_, _) => action();
        menu.DropDownItems.Add(item);
    }

    private void ChooseDirectory()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose the tilesheet working directory", UseDescriptionForTitle = true, SelectedPath = _directory };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _directory = dialog.SelectedPath;
        RefreshDirectory();
    }

    private void RefreshDirectory()
    {
        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            var root = new TreeNode(_directory) { Tag = new DirectoryInfo(_directory), ToolTipText = _directory };
            _tree.Nodes.Add(root);
            FillDirectory(root, _directory);
            root.Expand();
        }
        finally { _tree.EndUpdate(); }
    }

    private static bool IsImage(string path) => ImageExtensions.Contains(Path.GetExtension(path));

    private static void FillDirectory(TreeNode parent, string path)
    {
        parent.Nodes.Clear();
        try
        {
            foreach (var directory in new DirectoryInfo(path).EnumerateDirectories().OrderBy(d => d.Name))
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                var node = new TreeNode(directory.Name) { Tag = directory };
                node.Nodes.Add("Expand to load…");
                parent.Nodes.Add(node);
            }
            var files = Directory.EnumerateFiles(path).OrderBy(Path.GetFileName).ToArray();
            var paired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string gts in files.Where(f => Path.GetExtension(f).Equals(".gts", StringComparison.OrdinalIgnoreCase)))
            {
                var node = new TreeNode(Path.GetFileName(gts)) { Tag = gts, ToolTipText = gts };
                parent.Nodes.Add(node);
                try
                {
                    string? image = TilesheetDocument.Open(gts).ResolveImagePath();
                    if (image is not null && File.Exists(image))
                    {
                        node.Nodes.Add(new TreeNode(Path.GetFileName(image) + " (referenced image)") { Tag = image, ToolTipText = image });
                        paired.Add(image);
                    }
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or NotSupportedException)
                {
                    node.ToolTipText = "Cannot read definition: " + ex.Message;
                }
                // Name pairing is only a browsing hint, never an image association.
                foreach (string image in files.Where(f => IsImage(f) && !paired.Contains(f) &&
                    Path.GetFileNameWithoutExtension(f).Equals(Path.GetFileNameWithoutExtension(gts), StringComparison.OrdinalIgnoreCase)))
                {
                    node.Nodes.Add(new TreeNode(Path.GetFileName(image) + " (same name)") { Tag = image, ToolTipText = image });
                    paired.Add(image);
                }
            }
            foreach (string image in files.Where(f => IsImage(f) && !paired.Contains(f)))
                parent.Nodes.Add(new TreeNode(Path.GetFileName(image)) { Tag = image, ToolTipText = "Double-click to create GTS. Right-click to use in the active document." });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            parent.Nodes.Add("Cannot read directory: " + ex.Message);
        }
    }

    private void OpenFiles()
    {
        using var dialog = new OpenFileDialog { Filter = "Gondwana tilesheets|*.gts", Multiselect = true, InitialDirectory = _directory };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            foreach (string path in dialog.FileNames) OpenDocument(path);
    }

    private void OpenDocument(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            var existing = _documents.FirstOrDefault(d => string.Equals(d.Document.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) { existing.Activate(); return; }
            ShowDocument(TilesheetDocument.Open(path));
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        { ShowError(ex); }
    }

    private void NewDocument(string? imagePath = null)
    {
        var editor = ShowDocument(TilesheetDocument.Create(_directory));
        if (imagePath is not null)
        {
            editor.Document.Definition.Name = Path.GetFileNameWithoutExtension(imagePath);
            editor.ChooseImage(imagePath);
            editor.AddRegion();
        }
    }

    private EditorDocument ShowDocument(TilesheetDocument document)
    {
        var editor = new EditorDocument(document, SaveDocument);
        _documents.Add(editor);
        editor.FormClosed += (_, _) => _documents.Remove(editor);
        editor.Show(_dock, DockState.Document);
        return editor;
    }

    private bool SaveDocument(EditorDocument editor, bool saveAs)
    {
        try
        {
            if (!editor.CommitEdits()) return false;
            editor.RefreshView();
            string? path = editor.Document.FilePath;
            if (saveAs || path is null)
            {
                using var dialog = new SaveFileDialog { Filter = "Gondwana tilesheets|*.gts", DefaultExt = "gts", AddExtension = true,
                    InitialDirectory = editor.Document.BaseDirectory, FileName = path is null ? "Untitled.gts" : Path.GetFileName(path) };
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                path = dialog.FileName;
            }
            if (_documents.Any(d => d != editor && string.Equals(d.Document.FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("That destination is already open in another document. Close it before overwriting it.");
            var errors = editor.UpdateValidation();
            bool allowInvalid = errors.Count > 0;
            if (allowInvalid && MessageBox.Show(this, "This definition has validation errors. See the validation panel. Save this invalid definition anyway?",
                "Validation failed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return false;
            editor.Document.Save(path, editor.ImageSize, allowInvalid);
            editor.RefreshView();
            RefreshDirectory();
            return true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        { ShowError(ex); return false; }
    }

    private void ShowError(Exception ex) => MessageBox.Show(this, ex.Message, "GTS editor", MessageBoxButtons.OK, MessageBoxIcon.Error);

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _workspace.Dispose(); _dock.Dispose(); _theme.Dispose(); }
        base.Dispose(disposing);
    }
}
