using Gondwana.Tooling.Tilesheets.Editing;
using Gondwana.Tooling.Tilesheets.Sources;

namespace Gondwana.Tooling.Tilesheets.WinForms;

/// <summary>
/// Hostable project/source browser used by the standalone GTS tool and, later,
/// Gondwana Studio. It knows how to browse loose GTS/images and GAF image entries,
/// but delegates document actions to its host through events.
/// </summary>
public sealed class TilesheetWorkspaceControl : UserControl
{
    internal const string ImageFilter =
        "Images|*.png;*.bmp;*.jpg;*.jpeg;*.gif;*.webp;*.ico;*.wbmp;*.avif;*.heif;*.heic;*.dng;*.ktx;*.astc;*.pkm|All files|*.*";

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".bmp", ".jpg", ".jpeg", ".gif", ".webp", ".ico",
            ".wbmp", ".avif", ".heif", ".heic", ".dng", ".ktx", ".astc", ".pkm"
        };

    private static readonly HashSet<string> PackageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".gaf", ".zip"
        };

    private sealed record PackageNode(string Path);

    private readonly AssetPackageCatalog _assetPackages;
    private readonly TreeView _tree = new()
    {
        Dock = DockStyle.Fill,
        HideSelection = false,
        ShowNodeToolTips = true
    };

    public string WorkingDirectory { get; private set; }

    public event Action<string>? OpenGtsRequested;
    public event Action<string>? CreateFromLooseImageRequested;
    public event Action<string>? UseLooseImageRequested;
    public event Action<PackedImageSource>? CreateFromPackedImageRequested;
    public event Action<PackedImageSource>? UsePackedImageRequested;

    public TilesheetWorkspaceControl(
        AssetPackageCatalog assetPackages,
        string? initialDirectory = null)
    {
        _assetPackages = assetPackages ??
            throw new ArgumentNullException(nameof(assetPackages));

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

        _tree.BeforeExpand += TreeBeforeExpand;
        _tree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is string path &&
                Path.GetExtension(path).Equals(
                    ".gts",
                    StringComparison.OrdinalIgnoreCase))
            {
                OpenGtsRequested?.Invoke(path);
            }
        };

        _tree.NodeMouseDoubleClick += (_, e) =>
        {
            switch (e.Node.Tag)
            {
                case string path when IsImage(path):
                    CreateFromLooseImageRequested?.Invoke(path);
                    break;

                case PackedImageSource packed:
                    CreateFromPackedImageRequested?.Invoke(packed);
                    break;
            }
        };

        _tree.NodeMouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
                _tree.SelectedNode = e.Node;
        };

        var context = new ContextMenuStrip();
        context.Items.Add(
            "Create GTS from image",
            null,
            (_, _) => CreateFromSelected());

        context.Items.Add(
            "Use image in active document",
            null,
            (_, _) => UseSelected());

        _tree.ContextMenuStrip = context;

        DarkTheme.Apply(this);
        DarkTheme.Apply(context);

        RefreshDirectory();
    }

    public void ChooseDirectory(IWin32Window? owner = null)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the tilesheet working directory",
            UseDescriptionForTitle = true,
            SelectedPath = WorkingDirectory
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
            return;

        SetWorkingDirectory(dialog.SelectedPath);
    }

    public void SetWorkingDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(
                "Working directory must be a non-empty string.",
                nameof(path));

        WorkingDirectory = Path.GetFullPath(path);
        RefreshDirectory();
    }

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

    private void TreeBeforeExpand(
        object? sender,
        TreeViewCancelEventArgs e)
    {
        var node = e.Node;
        if (node is null ||
            node.Nodes.Count != 1 ||
            node.Nodes[0].Tag is not null)
        {
            return;
        }

        switch (node.Tag)
        {
            case DirectoryInfo directory:
                FillDirectory(node, directory.FullName);
                break;

            case PackageNode package:
                FillPackage(node, package.Path);
                break;
        }
    }

    private void FillDirectory(TreeNode parent, string path)
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

            var files = Directory.EnumerateFiles(path)
                .OrderBy(Path.GetFileName)
                .ToArray();

            var paired = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var gts in files.Where(file =>
                         Path.GetExtension(file).Equals(
                             ".gts",
                             StringComparison.OrdinalIgnoreCase)))
            {
                var node = new TreeNode(Path.GetFileName(gts))
                {
                    Tag = gts,
                    ToolTipText = gts
                };

                parent.Nodes.Add(node);

                try
                {
                    var document = TilesheetDocument.Open(gts);
                    var looseImage = document.ResolveImagePath();

                    if (looseImage is not null &&
                        File.Exists(looseImage))
                    {
                        node.Nodes.Add(
                            new TreeNode(
                                Path.GetFileName(looseImage) +
                                " (referenced image)")
                            {
                                Tag = looseImage,
                                ToolTipText = looseImage
                            });

                        paired.Add(looseImage);
                    }
                    else if (document.ResolveAssetsFilePath() is { } packagePath &&
                             !string.IsNullOrWhiteSpace(
                                 document.Definition.Image.AssetEntryName))
                    {
                        var packed = new PackedImageSource(
                            packagePath,
                            document.Definition.Image.AssetEntryName!);

                        node.Nodes.Add(
                            new TreeNode(
                                packed.AssetEntryName +
                                " (referenced GAF image)")
                            {
                                Tag = packed,
                                ToolTipText = packed.ToString()
                            });
                    }
                }
                catch (Exception ex) when (
                    ex is IOException or
                    ArgumentException or
                    UnauthorizedAccessException or
                    NotSupportedException or
                    InvalidDataException)
                {
                    node.ToolTipText =
                        "Cannot read definition: " + ex.Message;
                }

                foreach (var image in files.Where(file =>
                             IsImage(file) &&
                             !paired.Contains(file) &&
                             Path.GetFileNameWithoutExtension(file).Equals(
                                 Path.GetFileNameWithoutExtension(gts),
                                 StringComparison.OrdinalIgnoreCase)))
                {
                    node.Nodes.Add(
                        new TreeNode(
                            Path.GetFileName(image) +
                            " (same name)")
                        {
                            Tag = image,
                            ToolTipText = image
                        });

                    paired.Add(image);
                }
            }

            foreach (var package in files.Where(IsAssetPackage))
            {
                var node = new TreeNode(
                    Path.GetFileName(package) +
                    " [GAF]")
                {
                    Tag = new PackageNode(package),
                    ToolTipText =
                        "Expand to browse Image entries in this asset package."
                };

                node.Nodes.Add("Expand to load images…");
                parent.Nodes.Add(node);
            }

            foreach (var image in files.Where(file =>
                         IsImage(file) &&
                         !paired.Contains(file)))
            {
                parent.Nodes.Add(
                    new TreeNode(Path.GetFileName(image))
                    {
                        Tag = image,
                        ToolTipText =
                            "Double-click to create GTS. Right-click to use in the active document."
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

    private void FillPackage(TreeNode node, string packagePath)
    {
        node.Nodes.Clear();

        try
        {
            var images = _assetPackages.GetImages(packagePath);

            if (images.Count == 0)
            {
                node.Nodes.Add("(no Image assets)");
                return;
            }

            foreach (var image in images)
            {
                node.Nodes.Add(
                    new TreeNode(image.AssetEntryName)
                    {
                        Tag = image,
                        ToolTipText =
                            $"{image.AssetEntryName} in {image.AssetsFilePath}"
                    });
            }
        }
        catch (Exception ex)
        {
            node.Nodes.Add(
                "Cannot read GAF: " + ex.Message);
        }
    }

    private void CreateFromSelected()
    {
        switch (_tree.SelectedNode?.Tag)
        {
            case string path when IsImage(path):
                CreateFromLooseImageRequested?.Invoke(path);
                break;

            case PackedImageSource packed:
                CreateFromPackedImageRequested?.Invoke(packed);
                break;
        }
    }

    private void UseSelected()
    {
        switch (_tree.SelectedNode?.Tag)
        {
            case string path when IsImage(path):
                UseLooseImageRequested?.Invoke(path);
                break;

            case PackedImageSource packed:
                UsePackedImageRequested?.Invoke(packed);
                break;
        }
    }

    internal static bool IsImage(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path));

    internal static bool IsAssetPackage(string path) =>
        PackageExtensions.Contains(Path.GetExtension(path));
}
