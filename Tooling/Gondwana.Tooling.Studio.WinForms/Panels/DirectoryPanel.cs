using System.Runtime.InteropServices;
using Gondwana.Assets;
using Gondwana.Tooling.Studio.WinForms.Documents;
using Gondwana.Tooling.WinForms;
using ICSharpCode.SharpZipLib;

namespace Gondwana.Tooling.Studio.WinForms.Panels;

/// <summary>One lazy filesystem browser for all supported Studio authoring formats.</summary>
public sealed class DirectoryPanel : UserControl
{
    private readonly Action<string> _log;
    private readonly TreeNavigationSizing _sizing;
    internal NavigationIconSize IconSize => _sizing.Size ?? NavigationIconSize.Small;
    private readonly ContextMenuStrip _fileMenu = new();
    private readonly ContextMenuStrip _categoryMenu = new();
    private string? _menuCategoryFormat;
    private string? _menuCategoryDirectory;
    private string? _menuPath;
    private PackedAssetNodeIdentity? _menuPacked;
    private AssetLocation? _activeDocumentLocation;
    internal event Action<PackedAssetNodeIdentity>? PackedFileActivated;
    private CancellationTokenSource _archiveRefresh = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _archiveGate = new(1, 1);
    private readonly SynchronizationContext _uiContext = new WindowsFormsSynchronizationContext();
    internal Task PendingArchiveLoads { get; private set; } = Task.CompletedTask;
    internal Action<string> OpenExternal { get; set; }
    internal Action<string>? ArchiveOpened { get; set; }

    // Called only on the UI thread; must return session credentials without prompting.
    internal Func<string, string?> KnownArchivePassword { get; set; } = _ => null;

    private static readonly (string Key, string Label, string Glyph, Color Color)[] Categories = StudioFileTypeIcons.Categories;

    internal TreeView Tree { get; } = new()
    {
        Dock = DockStyle.Fill,
        HideSelection = false,
        ShowNodeToolTips = true,
        BackColor = ToolingAppearance.Palette.Background,
        ForeColor = ToolingAppearance.Palette.Foreground
    };

    /// <summary>Gets the full path used as the browser root; initially the process current directory.</summary>
    public string WorkingDirectory { get; private set; } = Environment.CurrentDirectory;

    /// <summary>Occurs when a supported loose file is double-clicked or opened from its context menu; supplies its full path.</summary>
    public event Action<string>? FileActivated;

    /// <summary>Occurs when the Directory button is clicked so the host can choose and set a new browser root.</summary>
    public event Action? ChooseDirectoryRequested;

    /// <summary>Requests relationship inspection for an exact loose or packed resource without opening it.</summary>
    public event Action<Gondwana.Assets.AssetLocation, bool>? ReferencesRequested;

    /// <summary>Requests a new Studio document of the specified format in the selected directory.</summary>
    public event Action<string, string>? NewFileRequested;

    /// <summary>Creates the browser controls and file actions without populating the initial directory.</summary>
    /// <param name="log">Callback for directory, archive, and external-open failures.</param>
    public DirectoryPanel(Action<string> log)
    {
        _log = log;

        OpenExternal = ShowOpenWithDialog;
        _sizing = NavigationSizing.Attach(Tree, CreateImages, NavigationIconSize.Small);

        _fileMenu.Items.Add("Open", null, (_, _) =>
        {
            if (_menuPacked is { } packed)
                PackedFileActivated?.Invoke(packed);

            if (_menuPath is { } path)
                FileActivated?.Invoke(path);
        });

        _fileMenu.Items.Add("Open External", null, (_, _) =>
        {
            if (_menuPath is not { } path)
                return;

            try
            {
                OpenExternal(path);
            }
            catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
            {
                _log($"Cannot open {path} externally: {ex.Message}");
                ThemedMessageBox.Show(this, ex.Message, "Open External", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });

        foreach (var title in new[] { "Find References", "Show Dependencies", "Show Dependents" })
            _fileMenu.Items.Add(title, null, (_, _) =>
            {
                if (_menuPacked is { } packed)
                    ReferencesRequested?.Invoke(new(packed.AssetType, packed.ArchivePath, packed.EntryName), title != "Show Dependencies");
                else if (_menuPath is { } path)
                    ReferencesRequested?.Invoke(new(Gondwana.Tooling.Assets.AssetImportPlan.InferType(path), Path.GetFullPath(path)), title != "Show Dependencies");
            });

        _categoryMenu.Items.Add("Add New", null, (_, _) =>
        {
            if (_menuCategoryFormat is { } format && _menuCategoryDirectory is { } directory)
                NewFileRequested?.Invoke(format, directory);
        });

        Tree.NodeMouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            if (PrepareCategoryMenu(e.Node))
                _categoryMenu.Show(Tree, e.Location);
            else if (PrepareFileMenu(e.Node))
                _fileMenu.Show(Tree, e.Location);
        };

        Dock = DockStyle.Fill;

        var tools = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

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
            if (e.Button == MouseButtons.Left)
                ActivateNode(e.Node);
        };
    }

    /// <summary>Normalizes the browser root and refreshes its contents on the UI thread.</summary>
    /// <param name="path">Directory path, resolved against the process current directory if relative.</param>
    /// <remarks>Does not change the process current directory. Unreadable directories are shown as errors and logged.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or invalid.</exception>
    public void SetDirectory(string path)
    {
        WorkingDirectory = Path.GetFullPath(path);
        RefreshDirectory();
    }

    /// <summary>Rebuilds the browser root, restoring the active document selection when available and cancelling obsolete scans.</summary>
    /// <remarks>Call on the UI thread. Loose files load synchronously; packed entries load asynchronously and subdirectories load when expanded.</remarks>
    public void RefreshDirectory()
    {
        Tree.BeginUpdate();

        try
        {
            _archiveRefresh.Cancel();
            _archiveRefresh.Dispose();
            _archiveRefresh = new CancellationTokenSource();
            _fileMenu.Close();
            _categoryMenu.Close();
            _menuPath = null;
            _menuPacked = null;
            _menuCategoryFormat = null;
            _menuCategoryDirectory = null;
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

        SelectActiveDocumentNode();
    }

    /// <summary>Selects the active document in the tree without changing editor keyboard focus.</summary>
    /// <param name="location">Physical location of the active document, or null for an untitled document.</param>
    /// <remarks>Packed entries may not be selectable until an asynchronous archive scan finishes.</remarks>
    internal void SelectActiveDocument(AssetLocation? location)
    {
        _activeDocumentLocation = location;
        SelectActiveDocumentNode();
    }

    private void SelectActiveDocumentNode()
    {
        var node = FindActiveDocumentNode();
        if (!ReferenceEquals(Tree.SelectedNode, node))
            Tree.SelectedNode = node;

        node?.EnsureVisible();
    }

    private TreeNode? FindActiveDocumentNode()
    {
        if (_activeDocumentLocation is not { } location || Tree.Nodes.Count == 0 ||
            !File.Exists(location.Path))
            return null;

        string relative = Path.GetRelativePath(WorkingDirectory, location.Path);
        if (relative is "." or ".." ||
            Path.IsPathRooted(relative) ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return null;

        var segments = relative.Split(Path.DirectorySeparatorChar);
        TreeNode parent = Tree.Nodes[0];
        string parentPath = WorkingDirectory;

        for (int i = 0; i < segments.Length - 1; i++)
        {
            parentPath = Path.Combine(parentPath, segments[i]);
            var directory = parent.Nodes.Cast<TreeNode>().FirstOrDefault(node =>
                node.Tag is DirectoryInfo info &&
                string.Equals(info.FullName, parentPath, StringComparison.OrdinalIgnoreCase));

            if (directory is null)
                return null;

            // Materialize only ancestors of the selected file, leaving unrelated folders lazy.
            if (directory.Nodes.Count == 1 && directory.Nodes[0].Tag is null)
                FillDirectory(directory, parentPath);

            directory.Expand();
            parent = directory;
        }

        foreach (TreeNode group in parent.Nodes)
        {
            foreach (TreeNode child in group.Nodes)
            {
                if (location.AssetEntryName is null &&
                    child.Tag is string path &&
                    string.Equals(path, location.Path, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                if (location.AssetEntryName is not null)
                {
                    foreach (TreeNode entry in child.Nodes)
                    {
                        if (entry.Tag is PackedAssetNodeIdentity packed &&
                            packed.AssetType == location.AssetType &&
                            string.Equals(packed.ArchivePath, location.Path, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(packed.EntryName, location.AssetEntryName, StringComparison.OrdinalIgnoreCase))
                        {
                            return entry;
                        }
                    }
                }
            }
        }

        return null;
    }

    private void FillDirectory(TreeNode parent, string path)
    {
        parent.Nodes.Clear();

        try
        {
            var groups = Categories.ToDictionary(category => category.Key,
                category => new TreeNode(category.Label)
                {
                    ImageKey = category.Key,
                    SelectedImageKey = category.Key
                });
            parent.Nodes.AddRange(Categories.Select(category => groups[category.Key]).ToArray());

            foreach (var directory in new DirectoryInfo(path).EnumerateDirectories().OrderBy(item => item.Name))
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                var node = new TreeNode(directory.Name) { Tag = directory, ImageKey = "directory", SelectedImageKey = "directory" };
                node.Nodes.Add("Expand to load…");
                parent.Nodes.Add(node);
            }

            var files = Directory.EnumerateFiles(path).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var file in files)
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

            var scan = FillPackedEntriesAsync(groups,
                files.Where(file => Path.GetExtension(file).Equals(".gaf", StringComparison.OrdinalIgnoreCase))
                    .Select(file => (Path: file, Password: KnownArchivePassword(file))).ToArray(),
                _archiveRefresh.Token);

            PendingArchiveLoads = PendingArchiveLoads.IsCompleted ? scan : Task.WhenAll(PendingArchiveLoads, scan);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            parent.Nodes.Clear();
            parent.Nodes.Add("Cannot read directory: " + ex.Message);
            _log($"Cannot read {path}: {ex.Message}");
        }
    }

    private async Task FillPackedEntriesAsync(Dictionary<string, TreeNode> groups, (string Path, string? Password)[] archives, CancellationToken cancellationToken)
    {
        try
        {
            // Serialize scans across expanded folders and refreshes. AssetsFile buffers payloads;
            // only one archive is loaded at a time, off the UI thread, without runtime registration.
            await _archiveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (var (archivePath, password) in archives)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var entries = await Task.Run(() =>
                        {
                            // Sharing flags alone cannot coexist with an exclusive writer on Windows.
                            // Studio saves use RunWithoutArchiveReadsAsync to drain this handle first.
                            using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                            ArchiveOpened?.Invoke(archivePath);

                            using var archive = AssetsFile.Load(stream, password, register: false);

                            return archive.GetAllEntries()
                                .OrderBy(entry => entry.AssetName, StringComparer.OrdinalIgnoreCase)
                                .ThenBy(entry => entry.AssetType)
                                .Select(entry => new PackedAssetNodeIdentity(Path.GetFullPath(archivePath), entry.AssetType, entry.AssetName))
                                .ToArray();
                        }, cancellationToken).ConfigureAwait(false);

                        await OnUiAsync(() =>
                        {
                            Tree.BeginUpdate();

                            try
                            {
                                foreach (var group in entries.GroupBy(entry => entry.AssetType switch
                                    {
                                        AssetTypes.TilesheetDefinition => "gts",
                                        AssetTypes.AnimationDefinition => "gani",
                                        AssetTypes.SceneDefinition => "gscn",
                                        AssetTypes.SpriteDefinition => "gspr",
                                        AssetTypes.AudioDefinition => "gsnd",
                                        _ => "misc"
                                    }))
                                {
                                    // A grouping node has no file/packed identity: only its children activate.
                                    var archiveNode = new TreeNode(Path.GetFileName(archivePath))
                                    {
                                        ToolTipText = archivePath,
                                        ImageKey = "gaf",
                                        SelectedImageKey = "gaf"
                                    };
                                    foreach (var entry in group)
                                        archiveNode.Nodes.Add(new TreeNode(entry.EntryName)
                                        {
                                            Tag = entry,
                                            ToolTipText = $"Archive: {entry.ArchivePath}\nEntry: {entry.EntryName}\nAsset type: {entry.AssetType}",
                                            ImageKey = group.Key,
                                            SelectedImageKey = group.Key
                                        });

                                    groups[group.Key].Nodes.Add(archiveNode);
                                    groups[group.Key].Expand();
                                }
                            }
                            finally
                            {
                                Tree.EndUpdate();
                            }

                            // A packed tab may have become active before this archive finished loading.
                            if (_activeDocumentLocation?.AssetEntryName is not null &&
                                string.Equals(_activeDocumentLocation.Path, archivePath, StringComparison.OrdinalIgnoreCase))
                                SelectActiveDocumentNode();
                        }, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SharpZipBaseException)
                    {
                        if (!cancellationToken.IsCancellationRequested)
                            await OnUiAsync(() => _log($"Cannot browse archive {archivePath}: {ex.Message}"), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _archiveGate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task OnUiAsync(Action action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));

        _uiContext.Post(_ =>
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            try
            {
                action();
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }, null);

        await completion.Task.ConfigureAwait(false);
    }

    internal async Task<T> RunArchiveReadAsync<T>(Func<Task<T>> read, CancellationToken cancellationToken)
    {
        await _archiveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await read().ConfigureAwait(false); }
        finally { _archiveGate.Release(); }
    }

    internal async Task RunWithoutArchiveReadsAsync(Action action)
    {
        // Called on the UI thread. Cancel obsolete scans, but wait asynchronously for the
        // actual reader to dispose its handle; cancellation alone does not close that handle.
        _archiveRefresh.Cancel();
        var cancellationToken = _lifetime.Token;
        await _archiveGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await OnUiAsync(() =>
            {
                try { action(); }
                finally { RefreshDirectory(); }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _archiveGate.Release();
        }
    }

    /// <summary>Offers creation only for real type groups directly beneath a directory node.</summary>
    internal bool PrepareCategoryMenu(TreeNode node)
    {
        _categoryMenu.Close();
        _menuCategoryFormat = null;
        _menuCategoryDirectory = null;

        if (node.Tag is not null || node.Parent?.Tag is not DirectoryInfo directory)
            return false;

        var category = Categories.FirstOrDefault(item => item.Key == node.ImageKey);
        if (category.Key is null || !string.Equals(category.Label, node.Text, StringComparison.Ordinal))
            return false;

        string? label = category.Key switch
        {
            "gaf" => "Asset File",
            "gts" => "Tilesheet",
            "gani" => "Animation",
            "gscn" => "Scene",
            "gspr" => "Sprite",
            "gsnd" => "Sound",
            _ => null
        };
        if (label is null) return false;

        _fileMenu.Close();
        _menuPath = null;
        _menuPacked = null;
        _menuCategoryFormat = category.Key;
        _menuCategoryDirectory = directory.FullName;
        _categoryMenu.Items[0].Text = "Add New " + label;
        Tree.SelectedNode = node;
        return true;
    }

    internal bool PrepareFileMenu(TreeNode node)
    {
        _categoryMenu.Close();
        _menuCategoryFormat = null;
        _menuCategoryDirectory = null;
        _fileMenu.Close();
        _menuPath = null;
        _menuPacked = null;
        Tree.SelectedNode = node;

        if (node.Tag is PackedAssetNodeIdentity packed)
        {
            _menuPacked = packed;
            _fileMenu.Items[0].Visible = StudioDocument.FormatFor(packed.AssetType) is not null;
            _fileMenu.Items[1].Visible = false;

            return true;
        }

        if (node.Tag is not string path)
            return false;

        _menuPath = path;
        _fileMenu.Items[0].Visible = StudioDocument.FormatFor(path) is not null;
        _fileMenu.Items[1].Visible = !Path.GetExtension(path).Equals(".gaf", StringComparison.OrdinalIgnoreCase);

        return true;
    }

    internal void ActivateNode(TreeNode node)
    {
        if (node.Tag is PackedAssetNodeIdentity packed && StudioDocument.FormatFor(packed.AssetType) is not null)
            PackedFileActivated?.Invoke(packed);
        else if (node.Tag is string path && StudioDocument.FormatFor(path) is not null)
            FileActivated?.Invoke(path);
    }

    internal ContextMenuStrip FileMenu => _fileMenu;
    internal ContextMenuStrip CategoryMenu => _categoryMenu;

    internal void ApplyIconSize(NavigationIconSize size) => _sizing.Apply(size);

    private static ImageList CreateImages(int pixels)
    {
        var images = new ImageList { ImageSize = new Size(pixels, pixels), ColorDepth = ColorDepth.Depth32Bit };
        try
        {
            AddIcon(images, "directory", "D", Color.DarkKhaki);
            foreach (var category in Categories)
                AddIcon(images, category.Key, category.Glyph, category.Color);
            return images;
        }
        catch { images.Dispose(); throw; }
    }

    private static void AddIcon(ImageList images, string key, string glyph, Color color)
    {
        using var bitmap = StudioFileTypeIcons.CreateBitmap(images.ImageSize.Width, glyph, color);
        images.Images.Add(key, bitmap);

        // Materialize the native copy before disposing the source bitmap.
        _ = images.Handle;
    }

    private void ShowOpenWithDialog(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("The file no longer exists.", path);

        var info = new OpenAsInfo { File = path, Flags = 0x00000004 }; // OAIF_EXEC
        int result = SHOpenWithDialog(Handle, ref info);

        if (result != unchecked((int)0x800704C7)) // HRESULT_FROM_WIN32(ERROR_CANCELLED)
            Marshal.ThrowExceptionForHR(result);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenAsInfo
    {
        /// <summary>Full file path passed to the Windows Open With dialog.</summary>
        [MarshalAs(UnmanagedType.LPWStr)] public string File;
        /// <summary>Optional file-class name for the Windows Open With dialog; null uses the file path.</summary>
        [MarshalAs(UnmanagedType.LPWStr)] public string? Class;
        /// <summary>Native Open With behavior flags passed to the shell.</summary>
        public uint Flags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHOpenWithDialog(IntPtr parent, ref OpenAsInfo info);

    /// <summary>Cancels pending archive work and releases the browser's menus, images, and child controls.</summary>
    /// <param name="disposing">Whether to release managed resources as well as native control resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            _archiveRefresh.Cancel();
            _archiveRefresh.Dispose();
            _lifetime.Cancel();
            _lifetime.Dispose();
            _fileMenu.Dispose();
            _categoryMenu.Dispose();
            _sizing.Dispose();
        }

        base.Dispose(disposing);
    }
}
