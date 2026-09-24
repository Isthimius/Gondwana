using Gondwana.Tooling.Studio.WinForms.Documents;

namespace Gondwana.Tooling.Studio.WinForms.Panels;

/// <summary>One lazy filesystem browser for all supported Studio authoring formats.</summary>
public sealed class DirectoryPanel : UserControl
{
    private readonly Action<string> _log;
    internal TreeView Tree { get; } = new()
    {
        Dock = DockStyle.Fill,
        HideSelection = false,
        ShowNodeToolTips = true,
        BackColor = Color.FromArgb(30, 30, 30),
        ForeColor = Color.Gainsboro
    };
    public string WorkingDirectory { get; private set; } = Environment.CurrentDirectory;
    public event Action<string>? FileActivated;
    public event Action? ChooseDirectoryRequested;

    public DirectoryPanel(Action<string> log)
    {
        _log = log;
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
            if (e.Node.Tag is string path)
                FileActivated?.Invoke(path);
        };
    }

    public void SetDirectory(string path)
    {
        WorkingDirectory = Path.GetFullPath(path);
        RefreshDirectory();
    }

    public void RefreshDirectory()
    {
        Tree.BeginUpdate();
        try
        {
            Tree.Nodes.Clear();
            var root = new TreeNode(WorkingDirectory) { Tag = new DirectoryInfo(WorkingDirectory) };
            Tree.Nodes.Add(root);
            FillDirectory(root, WorkingDirectory);
            root.Expand();
        }
        finally { Tree.EndUpdate(); }
    }

    private void FillDirectory(TreeNode parent, string path)
    {
        parent.Nodes.Clear();
        try
        {
            foreach (var directory in new DirectoryInfo(path).EnumerateDirectories().OrderBy(item => item.Name))
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                var node = new TreeNode(directory.Name) { Tag = directory };
                node.Nodes.Add("Expand to load…");
                parent.Nodes.Add(node);
            }
            foreach (var file in Directory.EnumerateFiles(path).Where(file => StudioDocument.FormatFor(file) is not null).OrderBy(Path.GetFileName))
                parent.Nodes.Add(new TreeNode(Path.GetFileName(file)) { Tag = file, ToolTipText = file });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            parent.Nodes.Add("Cannot read directory: " + ex.Message);
            _log($"Cannot read {path}: {ex.Message}");
        }
    }
}
