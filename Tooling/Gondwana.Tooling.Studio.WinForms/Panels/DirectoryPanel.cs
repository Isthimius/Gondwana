using System.Runtime.InteropServices;
using Gondwana.Assets;
using Gondwana.Tooling.Studio.WinForms.Documents;
using ICSharpCode.SharpZipLib;

namespace Gondwana.Tooling.Studio.WinForms.Panels;

/// <summary>One lazy filesystem browser for all supported Studio authoring formats.</summary>
public sealed class DirectoryPanel : UserControl
{
    private readonly Action<string> _log;
    private readonly ImageList _images = new() { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
    private readonly ContextMenuStrip _fileMenu = new();
    private string? _menuPath;
    private PackedAssetNodeIdentity? _menuPacked;
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

    /// <summary>Gets the full path used as the browser root; initially the process current directory.</summary>
    public string WorkingDirectory { get; private set; } = Environment.CurrentDirectory;

    /// <summary>Occurs when a supported loose file is double-clicked or opened from its context menu; supplies its full path.</summary>
    public event Action<string>? FileActivated;

    /// <summary>Occurs when the Directory button is clicked so the host can choose and set a new browser root.</summary>
    public event Action? ChooseDirectoryRequested;

    /// <summary>Creates the browser controls and file actions without populating the initial directory.</summary>
    /// <param name="log">Callback for directory, archive, and external-open failures.</param>
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
                MessageBox.Show(this, ex.Message, "Open External", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });

        Tree.NodeMouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && PrepareFileMenu(e.Node))
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

    /// <summary>Rebuilds the browser root, clearing selection and cancelling obsolete archive scans.</summary>
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
                                foreach (var entry in entries)
                                {
                                    var key = entry.AssetType switch
                                    {
                                        AssetTypes.TilesheetDefinition => "gts",
                                        AssetTypes.AnimationDefinition => "gani",
                                        AssetTypes.SceneDefinition => "gscn",
                                        AssetTypes.SpriteDefinition => "gspr",
                                        AssetTypes.AudioDefinition => "gsnd",
                                        _ => "misc"
                                    };

                                    groups[key].Nodes.Add(new TreeNode($"{entry.EntryName} ({Path.GetFileName(entry.ArchivePath)})")
                                    {
                                        Tag = entry,
                                        ToolTipText = $"Archive: {entry.ArchivePath}\nEntry: {entry.EntryName}\nAsset type: {entry.AssetType}",
                                        ImageKey = key,
                                        SelectedImageKey = key
                                    });

                                    groups[key].Expand();
                                }
                            }
                            finally
                            {
                                Tree.EndUpdate();
                            }
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

    internal bool PrepareFileMenu(TreeNode node)
    {
        _fileMenu.Close();
        _menuPath = null;
        _menuPacked = null;
        Tree.SelectedNode = node;

        if (node.Tag is PackedAssetNodeIdentity packed)
        {
            if (StudioDocument.FormatFor(packed.AssetType) is null)
                return false;

            _menuPacked = packed;
            _fileMenu.Items[0].Visible = true;
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
            Tree.ImageList = null;
            _images.Dispose();
        }

        base.Dispose(disposing);
    }
}
