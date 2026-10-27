using Gondwana.Tooling.WinForms;
namespace Gondwana.Tooling.Assets.WinForms;

/// <summary>
/// Hostable browser for Gondwana asset-package files. File opening is delegated to
/// the host so the same control can be used by the standalone app or Studio.
/// </summary>
public sealed class AssetWorkspaceControl : UserControl
{
    /// <summary>File-dialog filter for GAF and ZIP archives, with an all-files fallback.</summary>
    public const string AssetFileFilter =
        "Asset Files (*.gaf;*.zip)|*.gaf;*.zip|All Files (*.*)|*.*";

    private static readonly HashSet<string> AssetFileExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".gaf", ".zip"
        };

    private readonly TreeView _tree = new()
    {
        Dock = DockStyle.Fill,
        HideSelection = false,
        ShowNodeToolTips = true
    };

    /// <summary>Gets the full path at the root of the workspace browser.</summary>
    public string WorkingDirectory { get; private set; }

    /// <summary>Requests that the host open the full archive path selected in the tree or chosen through the Open context command.</summary>
    public event Action<string>? OpenAssetFileRequested;

    /// <summary>Creates an archive browser and loads the initial directory listing.</summary>
    /// <param name="initialDirectory">Initial directory, or null for the process's current directory.</param>
    public AssetWorkspaceControl(string? initialDirectory = null)
    {
        WorkingDirectory = Path.GetFullPath(
            initialDirectory ?? Environment.CurrentDirectory);

        Dock = DockStyle.Fill;

        var tools = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

        tools.Items.Add(
            "Directory…",
            null,
            (_, _) => ChooseDirectory());

        tools.Items.Add(
            "Refresh",
            null,
            (_, _) => RefreshDirectory());

        Controls.Add(_tree);
        Controls.Add(tools);

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
            if (e.Node?.Tag is string path &&
                IsAssetFile(path))
            {
                OpenAssetFileRequested?.Invoke(path);
            }
        };

        _tree.NodeMouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
                _tree.SelectedNode = e.Node;
        };

        var context = new ContextMenuStrip();
        context.Items.Add(
            "Open asset file",
            null,
            (_, _) =>
            {
                if (_tree.SelectedNode?.Tag is string path &&
                    IsAssetFile(path))
                {
                    OpenAssetFileRequested?.Invoke(path);
                }
            });

        _tree.ContextMenuStrip = context;

        NavigationSizing.Attach(_tree);
        ToolingAppearance.Apply(this);
        ToolingAppearance.Apply(context);

        RefreshDirectory();
    }

    /// <summary>
    /// Displays a folder picker and updates the working directory.
    /// </summary>
    /// <param name="owner">The window that owns the dialog.</param>
    public void ChooseDirectory(IWin32Window? owner = null)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the asset-file working directory",
            UseDescriptionForTitle = true,
            SelectedPath = WorkingDirectory
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
            return;

        SetWorkingDirectory(dialog.SelectedPath);
    }

    /// <summary>Normalizes the workspace path and rebuilds the directory browser.</summary>
    /// <param name="path">A nonblank directory path; relative paths use the process's current directory.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null, empty, or whitespace.</exception>
    public void SetWorkingDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(
                "Working directory must be a non-empty string.",
                nameof(path));

        WorkingDirectory = Path.GetFullPath(path);
        RefreshDirectory();
    }

    /// <summary>
    /// Refreshes the displayed contents of the working directory.
    /// </summary>
    public void RefreshDirectory()
    {
        _tree.BeginUpdate();

        try
        {
            _tree.Nodes.Clear();

            var root = new TreeNode(WorkingDirectory)
            {
                Tag = new DirectoryInfo(WorkingDirectory),
                ToolTipText = WorkingDirectory
            };

            _tree.Nodes.Add(root);
            FillDirectory(root, WorkingDirectory);
            root.Expand();
        }
        finally
        {
            _tree.EndUpdate();
        }
    }

    internal static bool IsAssetFile(string path) =>
        AssetFileExtensions.Contains(Path.GetExtension(path));

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
            parent.Nodes.Add(
                "Cannot read directory: " + ex.Message);
        }
    }
}
