using System.Runtime.InteropServices;
using Gondwana.Tooling.Studio.WinForms.Documents;

namespace Gondwana.Tooling.Studio.WinForms.Panels;

/// <summary>One lazy filesystem browser for all supported Studio authoring formats.</summary>
public sealed class DirectoryPanel : UserControl
{
    private readonly Action<string> _log;
    private readonly ImageList _images = new() { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
    private readonly ContextMenuStrip _fileMenu = new();
    private string? _menuPath;
    internal Action<string> OpenExternal { get; set; }

    // Keep labels and placeholder icons together so artwork can be replaced easily.
    private static readonly (string Key, string Label, string Glyph, Color Color)[] Categories =
    [
        ("gaf", "Assets (.gaf)", "A", Color.Goldenrod),
        ("gts", "Tilesheets (.gts)", "T", Color.DodgerBlue),
        ("gani", "Animations (.gani)", "▶", Color.MediumOrchid),
        ("gscn", "Scenes (.gscn)", "S", Color.SeaGreen),
        ("gspr", "Sprites (.gspr)", "◆", Color.Coral),
        ("gsnd", "Sound (.gsnd)", "♪", Color.Teal),
        ("misc", "Misc", "?", Color.SlateGray)
    ];

    internal TreeView Tree { get; } = new()
    {
        Dock = DockStyle.Fill,
        HideSelection = false,
        ShowNodeToolTips = true,
        BackColor = Color.FromArgb(30, 30, 30),
        ForeColor = Color.Gainsboro
    };

    /// <summary>
    /// Gets the working directory.
    /// </summary>
    public string WorkingDirectory { get; private set; } = Environment.CurrentDirectory;

    /// <summary>
    /// Occurs when file activated.
    /// </summary>
    public event Action<string>? FileActivated;

    /// <summary>
    /// Occurs when choose directory requested.
    /// </summary>
    public event Action? ChooseDirectoryRequested;

    /// <summary>
    /// Initializes a new instance of the <c>DirectoryPanel</c> class.
    /// </summary>
    /// <param name="log">The log.</param>
    public DirectoryPanel(Action<string> log)
    {
        _log = log;
        OpenExternal = ShowOpenWithDialog;
        AddIcon("directory", "D", Color.DarkKhaki);
        foreach (var category in Categories)
            AddIcon(category.Key, category.Glyph, category.Color);
        Tree.ImageList = _images;
        _fileMenu.Items.Add("Open", null, (_, _) =>
        {
            if (_menuPath is { } path) FileActivated?.Invoke(path);
        });
        _fileMenu.Items.Add("Open External", null, (_, _) =>
        {
            if (_menuPath is not { } path) return;
            try { OpenExternal(path); }
            catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
            {
                _log($"Cannot open {path} externally: {ex.Message}");
                MessageBox.Show(this, ex.Message, "Open External", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        Tree.NodeMouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && PrepareFileMenu(e.Node))
                _fileMenu.Show(Tree, e.Location);
        };
        Dock = DockStyle.Fill;
        var tools = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        tools.Items.Add("Directory…", null, (_, _) => ChooseDirectoryRequested?.Invoke());
        tools.Items.Add("Refresh", null, (_, _) => RefreshDirectory());
        Controls.Add(Tree);
        Controls.Add(tools);
        Tree.BeforeExpand += (_, e) =>
        {
            if (e.Node?.Tag is DirectoryInfo directory && e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Tag is null)
                FillDirectory(e.Node, directory.FullName);
        };
        Tree.NodeMouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && e.Node.Tag is string path && StudioDocument.FormatFor(path) is not null)
                FileActivated?.Invoke(path);
        };
    }

    /// <summary>
    /// Sets directory.
    /// </summary>
    /// <param name="path">The path.</param>
    public void SetDirectory(string path)
    {
        WorkingDirectory = Path.GetFullPath(path);
        RefreshDirectory();
    }

    /// <summary>
    /// Refreshes the displayed contents of the working directory.
    /// </summary>
    public void RefreshDirectory()
    {
        Tree.BeginUpdate();

        try
        {
            _fileMenu.Close();
            _menuPath = null;
            Tree.Nodes.Clear();

            var root = new TreeNode(WorkingDirectory)
            {
                Tag = new DirectoryInfo(WorkingDirectory),
                ImageKey = "directory",
                SelectedImageKey = "directory"
            };

            Tree.Nodes.Add(root);
            FillDirectory(root, WorkingDirectory);
            root.Expand();
        }
        finally
        {
            Tree.EndUpdate();
        }
    }

    private void FillDirectory(TreeNode parent, string path)
    {
        parent.Nodes.Clear();

        try
        {
            var groups = Categories.ToDictionary(category => category.Key,
                category => new TreeNode(category.Label) { ImageKey = category.Key, SelectedImageKey = category.Key });
            parent.Nodes.AddRange(Categories.Select(category => groups[category.Key]).ToArray());

            foreach (var directory in new DirectoryInfo(path).EnumerateDirectories().OrderBy(item => item.Name))
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                var node = new TreeNode(directory.Name) { Tag = directory, ImageKey = "directory", SelectedImageKey = "directory" };
                node.Nodes.Add("Expand to load…");
                parent.Nodes.Add(node);
            }

            foreach (var file in Directory.EnumerateFiles(path).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                var key = StudioDocument.FormatFor(file) ?? "misc";
                groups[key].Nodes.Add(new TreeNode(Path.GetFileName(file))
                {
                    Tag = file,
                    ToolTipText = file,
                    ImageKey = key,
                    SelectedImageKey = key
                });
            }

            foreach (var group in groups.Values)
                group.Expand();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            parent.Nodes.Clear();
            parent.Nodes.Add("Cannot read directory: " + ex.Message);
            _log($"Cannot read {path}: {ex.Message}");
        }
    }

    internal bool PrepareFileMenu(TreeNode node)
    {
        _fileMenu.Close();
        _menuPath = null;
        Tree.SelectedNode = node;
        if (node.Tag is not string path) return false;
        _menuPath = path;
        _fileMenu.Items[0].Visible = StudioDocument.FormatFor(path) is not null;
        _fileMenu.Items[1].Visible = !Path.GetExtension(path).Equals(".gaf", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    internal ContextMenuStrip FileMenu => _fileMenu;

    private void AddIcon(string key, string glyph, Color color)
    {
        using var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        using var background = new SolidBrush(color);
        using var font = new Font("Segoe UI", 8, FontStyle.Bold, GraphicsUnit.Point);
        using var alignment = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        graphics.FillRectangle(background, 1, 1, 14, 14);
        graphics.DrawString(glyph, font, Brushes.White, new RectangleF(0, 0, 16, 16), alignment);
        _images.Images.Add(key, bitmap);
        // Materialize the native copy before disposing the source bitmap.
        _ = _images.Handle;
    }

    private void ShowOpenWithDialog(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("The file no longer exists.", path);
        var info = new OpenAsInfo { File = path, Flags = 0x00000004 }; // OAIF_EXEC
        int result = SHOpenWithDialog(Handle, ref info);
        if (result != unchecked((int)0x800704C7)) // HRESULT_FROM_WIN32(ERROR_CANCELLED)
            Marshal.ThrowExceptionForHR(result);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenAsInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string File;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Class;
        public uint Flags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHOpenWithDialog(IntPtr parent, ref OpenAsInfo info);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fileMenu.Dispose();
            Tree.ImageList = null;
            _images.Dispose();
        }
        base.Dispose(disposing);
    }
}
