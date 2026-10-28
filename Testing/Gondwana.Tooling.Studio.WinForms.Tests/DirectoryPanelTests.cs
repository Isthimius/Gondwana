using System.Diagnostics;
using System.Reflection;
using Gondwana.Assets;
using Gondwana.Tooling.Studio.WinForms.Panels;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Fact]
    public void BrowserDisplaysTypedPackedEntriesAfterLooseFilesWithoutChangingContent() => RunSta(directory =>
    {
        var types = new[] { AssetTypes.TilesheetDefinition, AssetTypes.AnimationDefinition,
            AssetTypes.SceneDefinition, AssetTypes.SpriteDefinition, AssetTypes.AudioDefinition,
            AssetTypes.Image, AssetTypes.Audio, AssetTypes.Video, AssetTypes.Cursor, AssetTypes.Font,
            AssetTypes.Svg, AssetTypes.Misc, (AssetTypes)999 };
        foreach (var extension in new[] { "gts", "gani", "gscn", "gspr", "gsnd", "txt" })
        {
            File.WriteAllText(Path.Combine(directory, "z." + extension), "unchanged");
            File.WriteAllText(Path.Combine(directory, "A." + extension), "unchanged");
        }
        foreach (var name in new[] { "b.GAF", "a.gaf" })
        {
            using var archive = AssetsFile.LoadOrCreate(Path.Combine(directory, name), null, false, register: false);
            foreach (var type in types)
                archive.Add(type, "nested/same.gts", new MemoryStream([1, 2, 3]));
            archive.Add(AssetTypes.TilesheetDefinition, "z.gts", new MemoryStream([4]));
            archive.Save();
        }
        var before = Directory.GetFiles(directory).ToDictionary(path => path, File.ReadAllBytes);
        var registered = AssetsFile.AllAssetsFiles.ToArray();
        using var panel = new DirectoryPanel(_ => { });
        panel.SetDirectory(directory);
        WaitForArchives(panel);
        var groups = panel.Tree.Nodes[0].Nodes;
        Assert.Equal(2, groups[0].Nodes.Count);
        for (int group = 1; group < 7; group++)
        {
            Assert.Equal(new[] { "A.", "z." }, groups[group].Nodes.Cast<TreeNode>().Take(2).Select(n => n.Text[..2]));
            Assert.All(groups[group].Nodes.Cast<TreeNode>().Take(2), n => Assert.IsType<string>(n.Tag));
            var archives = groups[group].Nodes.Cast<TreeNode>().Skip(2).ToArray();
            Assert.Equal(new[] { "a.gaf", "b.GAF" }, archives.Select(n => n.Text));
            Assert.All(archives, node =>
            {
                Assert.Null(node.Tag);
                Assert.False(panel.PrepareFileMenu(node));
                Assert.Equal("gaf", node.ImageKey);
                Assert.Equal(Path.Combine(directory, node.Text), node.ToolTipText);
                Assert.Equal(node.Nodes.Cast<TreeNode>().Select(n => n.Text).Order(StringComparer.OrdinalIgnoreCase),
                    node.Nodes.Cast<TreeNode>().Select(n => n.Text));
            });
            Assert.All(archives.SelectMany(n => n.Nodes.Cast<TreeNode>()), node =>
            {
                var identity = Assert.IsType<PackedAssetNodeIdentity>(node.Tag);
                Assert.True(Path.IsPathFullyQualified(identity.ArchivePath));
                if (group < 6) Assert.Equal(types[group - 1], identity.AssetType);
                else Assert.DoesNotContain(identity.AssetType, types.Take(5));
                Assert.Equal(identity.EntryName, node.Text);
                Assert.Equal($"Archive: {identity.ArchivePath}\nEntry: {identity.EntryName}\nAsset type: {identity.AssetType}", node.ToolTipText);
                Assert.Equal(groups[group].ImageKey, node.ImageKey);
                Assert.Equal(node.ImageKey, node.SelectedImageKey);
                Assert.True(panel.PrepareFileMenu(node)); // All packed resources support Find References.
                Assert.Equal(group < 6, panel.FileMenu.Items[0].Available);
            });
        }
        Assert.Equal(4, groups[1].Nodes.Count);
        Assert.Equal(2, groups[1].Nodes[2].Nodes.Count);
        Assert.Equal(4, groups[6].Nodes.Count);
        Assert.Equal(8, groups[6].Nodes[2].Nodes.Count);
        var sameNames = groups[1].Nodes.Cast<TreeNode>().Skip(2).SelectMany(n => n.Nodes.Cast<TreeNode>())
            .Select(node => (PackedAssetNodeIdentity)node.Tag!).Where(entry => entry.EntryName == "nested/same.gts").ToArray();
        Assert.Equal(2, sameNames.Length);
        Assert.NotEqual(sameNames[0], sameNames[1]);
        Assert.Equal(registered, AssetsFile.AllAssetsFiles);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(directory).Order());
        foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
    });

    [Fact]
    public void BrowserRefreshRereadsArchivesAndSkipsFailuresWithoutLocks() => RunSta(directory =>
    {
        var good = Path.Combine(directory, "good.gaf");
        WriteBrowserArchive(good, "old.gts");
        var corrupt = Path.Combine(directory, "corrupt.gaf");
        File.WriteAllText(corrupt, "not an archive");
        var locked = Path.Combine(directory, "locked.gaf");
        WriteBrowserArchive(locked, "locked.gts");
        var encrypted = Path.Combine(directory, "encrypted.gaf");
        using (var archive = AssetsFile.LoadOrCreate(encrypted, "secret", true, register: false))
        {
            archive.Add(AssetTypes.TilesheetDefinition, "secret.gts", new MemoryStream([1]));
            archive.Save();
        }
        File.WriteAllText(Path.Combine(directory, "loose.gts"), "{}");
        var logs = new List<string>();
        using var panel = new DirectoryPanel(logs.Add);
        using (File.Open(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            panel.SetDirectory(directory);
            WaitForArchives(panel);
        }
        Assert.Equal(new[] { "loose.gts", "good.gaf" },
            panel.Tree.Nodes[0].Nodes[1].Nodes.Cast<TreeNode>().Select(node => node.Text));
        Assert.Equal("old.gts", panel.Tree.Nodes[0].Nodes[1].Nodes[1].Nodes[0].Text);
        foreach (var path in new[] { corrupt, locked, encrypted })
            Assert.Contains(logs, message => message.Contains(path));
        using (var archive = AssetsFile.LoadOrCreate(good, null, false, register: false))
        {
            archive.Remove(AssetTypes.TilesheetDefinition, "old.gts");
            archive.Add(AssetTypes.SceneDefinition, "new.gts", new MemoryStream([2]));
            archive.Save();
        }
        panel.RefreshDirectory();
        WaitForArchives(panel);
        Assert.Equal("new.gts", Assert.Single(Assert.Single(panel.Tree.Nodes[0].Nodes[3].Nodes.Cast<TreeNode>()).Nodes.Cast<TreeNode>()).Text);
        Assert.DoesNotContain(panel.Tree.Nodes[0].Nodes[1].Nodes.Cast<TreeNode>(), node => node.Text.Contains("old.gts"));
        panel.KnownArchivePassword = path => path == encrypted ? "secret" : null;
        panel.RefreshDirectory();
        WaitForArchives(panel);
        Assert.Contains(panel.Tree.Nodes[0].Nodes[1].Nodes.Cast<TreeNode>(),
            node => node.Text == "encrypted.gaf" && node.Nodes[0].Text == "secret.gts");
        File.Delete(good);
        panel.RefreshDirectory();
        WaitForArchives(panel);
        Assert.Empty(panel.Tree.Nodes[0].Nodes[3].Nodes.Cast<TreeNode>());
    });

    [Fact]
    public void BrowserKeepsLooseActionsAndLoadsSubdirectoryArchivesLazily() => RunSta(directory =>
    {
        var child = Directory.CreateDirectory(Path.Combine(directory, "child")).FullName;
        WriteBrowserArchive(Path.Combine(child, "nested.gaf"), "entry.gts");
        WriteBrowserArchive(Path.Combine(directory, "root.gaf"), "entry.gts");
        File.WriteAllText(Path.Combine(directory, "loose.gts"), "{}");
        using var panel = new DirectoryPanel(_ => { });
        panel.SetDirectory(directory);
        WaitForArchives(panel);
        var root = panel.Tree.Nodes[0];
        var folder = root.Nodes.Cast<TreeNode>().Single(node => node.Tag is DirectoryInfo);
        Assert.Null(Assert.Single(folder.Nodes.Cast<TreeNode>()).Tag);
        _ = panel.Tree.Handle;
        folder.Expand();
        WaitForArchives(panel);
        Assert.Equal("entry.gts", Assert.Single(Assert.Single(folder.Nodes[1].Nodes.Cast<TreeNode>()).Nodes.Cast<TreeNode>()).Text);
        string? activated = null;
        panel.FileActivated += path => activated = path;
        var loose = root.Nodes[1].Nodes[0];
        DoubleClickNode(panel, loose);
        Assert.Equal(loose.Tag, activated);
        activated = null;
        panel.PrepareFileMenu(loose);
        panel.FileMenu.Items[0].PerformClick();
        Assert.Equal(loose.Tag, activated);
        activated = null;
        var grouping = root.Nodes[1].Nodes[1];
        Assert.False(panel.PrepareFileMenu(grouping));
        DoubleClickNode(panel, grouping);
        Assert.Null(activated);
        grouping.Expand();
        grouping.Collapse();
        var packed = grouping.Nodes[0];
        PackedAssetNodeIdentity? packedActivated = null;
        panel.PackedFileActivated += identity => packedActivated = identity;
        Assert.True(panel.PrepareFileMenu(packed));
        Assert.False(panel.FileMenu.Items[1].Available);
        panel.FileMenu.Items[0].PerformClick(); // A previous loose-file target must not leak.
        Assert.Equal(packed.Tag, packedActivated);
        packedActivated = null;
        DoubleClickNode(panel, packed);
        Assert.Equal(packed.Tag, packedActivated);
        Assert.Null(activated);
        var archive = root.Nodes[0].Nodes[0];
        Assert.True(panel.PrepareFileMenu(archive));
        Assert.False(panel.FileMenu.Items[1].Available);
        DoubleClickNode(panel, archive);
        Assert.Equal(archive.Tag, activated);
        string? external = null;
        panel.OpenExternal = path => external = path;
        panel.PrepareFileMenu(loose);
        panel.FileMenu.Items[1].PerformClick();
        Assert.Equal(loose.Tag, external);

        // A refresh/navigation supersedes pending results, even if a load has begun.
        panel.RefreshDirectory();
        panel.SetDirectory(child);
        WaitForArchives(panel);
        Assert.Equal(child, panel.Tree.Nodes[0].Text);
        Assert.Equal("entry.gts", Assert.Single(Assert.Single(panel.Tree.Nodes[0].Nodes[1].Nodes.Cast<TreeNode>()).Nodes.Cast<TreeNode>()).Text);
    });

    [Fact]
    public void BrowserDisposalCancelsPendingPublicationAndReleasesArchive() => RunSta(directory =>
    {
        var path = Path.Combine(directory, "pending.gaf");
        WriteBrowserArchive(path, "entry.gts");
        var logs = new List<string>();
        var panel = new DirectoryPanel(logs.Add);
        panel.SetDirectory(directory);
        // No UI messages have been pumped, so publication is still pending.
        panel.Dispose();
        panel.Dispose();
        WaitForArchives(panel);
        Assert.Empty(logs);
        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    });

    [Fact]
    public void BrowserOmitsEmptyArchiveGroupsAndKeepsPhysicalArchives() => RunSta(directory =>
    {
        using (var empty = AssetsFile.LoadOrCreate(Path.Combine(directory, "empty.gaf"), null, false, register: false))
            empty.Save();
        WriteBrowserArchive(Path.Combine(directory, "tiles.gaf"), "entry.gts");
        using var panel = new DirectoryPanel(_ => { });
        panel.SetDirectory(directory);
        WaitForArchives(panel);
        var groups = panel.Tree.Nodes[0].Nodes;
        Assert.Equal(new[] { "empty.gaf", "tiles.gaf" }, groups[0].Nodes.Cast<TreeNode>().Select(n => n.Text));
        Assert.Equal("tiles.gaf", Assert.Single(groups[1].Nodes.Cast<TreeNode>()).Text);
        foreach (var group in groups.Cast<TreeNode>().Skip(2))
            Assert.Empty(group.Nodes.Cast<TreeNode>());
    });

    [Fact]
    public void BrowserResizesExistingNodesWithoutArchiveReadsAndDisposesImages() => RunSta(directory =>
    {
        WriteBrowserArchive(Path.Combine(directory, "tiles.gaf"), "entry.gts");
        using var panel = new DirectoryPanel(_ => { });
        using var host = new Form { Size = new Size(600, 600), Opacity = 0, ShowInTaskbar = false };
        host.Controls.Add(panel);
        host.Show();
        panel.SetDirectory(directory);
        WaitForArchives(panel);
        var root = panel.Tree.Nodes[0];
        var archive = root.Nodes[1].Nodes[0];
        archive.Expand();
        var selected = archive.Nodes[0];
        panel.Tree.SelectedNode = selected;
        panel.Tree.TopNode = root;
        var pending = panel.PendingArchiveLoads;
        panel.ArchiveOpened = _ => throw new InvalidOperationException("Resizing must not read archives.");
        foreach (var size in new[] { NavigationIconSize.Medium, NavigationIconSize.Large, NavigationIconSize.Small })
        {
            var previous = panel.Tree.ImageList!;
            bool disposed = false;
            previous.Disposed += (_, _) => disposed = true;
            panel.ApplyIconSize(size);
            Assert.True(disposed);
            Assert.Same(pending, panel.PendingArchiveLoads);
            Assert.Same(root, panel.Tree.Nodes[0]);
            Assert.Same(selected, panel.Tree.SelectedNode);
            Assert.Same(root, panel.Tree.TopNode);
            Assert.True(archive.IsExpanded);
            Assert.Equal((int)Math.Round((int)size * panel.Tree.DeviceDpi / 96d), panel.Tree.ImageList!.ImageSize.Width);
            Assert.Equal(size == NavigationIconSize.Small ? 9f : size == NavigationIconSize.Medium ? 11f : 13f, panel.Tree.Font.SizeInPoints);
            Assert.True(panel.Tree.ItemHeight > panel.Tree.ImageList.ImageSize.Height);
            Assert.Equal(8, panel.Tree.ImageList.Images.Count);
            foreach (var key in panel.Tree.ImageList.Images.Keys.Cast<string>())
            {
                using var icon = new Bitmap(panel.Tree.ImageList.Images[key]!);
                Assert.Equal(panel.Tree.ImageList.ImageSize, icon.Size);
                Assert.True(icon.GetPixel(icon.Width / 4, icon.Height / 4).A > 0);
            }
        }
        archive.Collapse();
        panel.ApplyIconSize(NavigationIconSize.Large);
        Assert.False(archive.IsExpanded);
        var images = panel.Tree.ImageList!;
        bool finalDisposed = false;
        images.Disposed += (_, _) => finalDisposed = true;
        panel.Dispose();
        Assert.True(finalDisposed);
        using var invalid = new DirectoryPanel(_ => { });
        Assert.Throws<ArgumentOutOfRangeException>(() => invalid.ApplyIconSize((NavigationIconSize)123));
    });

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void StudioIconMenuPersistsImmediatelyAndSurvivesLayoutReset(int pixels) => RunSta(directory =>
    {
        var size = (NavigationIconSize)pixels;
        var store = new NavigationPreferenceStore();
        using (var first = Host(directory))
        {
            Assert.Equal(NavigationIconSize.Small, first.Browser.IconSize);
            first.RebuildViewMenu();
            var view = (ToolStripMenuItem)first.MainMenuStrip!.Items[1];
            var menu = view.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "Icon Size");
            Assert.Equal(new[] { "Small", "Medium", "Large" }, menu.DropDownItems.Cast<ToolStripMenuItem>().Select(i => i.Text));
            menu.DropDownItems.Cast<ToolStripMenuItem>().Single(i => i.Text == size.ToString()).PerformClick();
            Assert.Equal(size, first.Browser.IconSize);
            Assert.Equal(size, store.Read());
            Assert.Equal(size.ToString(), File.ReadAllText(store.FilePath));
            Assert.Equal(size.ToString(), Assert.Single(menu.DropDownItems.Cast<ToolStripMenuItem>(), i => i.Checked).Text);
            // A fresh instance reads the immediately saved preference before the first closes.
            using (var second = Host(Directory.CreateDirectory(Path.Combine(directory, "other-project")).FullName))
                Assert.Equal(size, second.Browser.IconSize);
            view.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "Reset application layout").PerformClick();
            Assert.Equal(size, store.Read());
            first.RebuildViewMenu();
            menu = view.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "Icon Size");
            Assert.Equal(size.ToString(), Assert.Single(menu.DropDownItems.Cast<ToolStripMenuItem>(), i => i.Checked).Text);
        }
        using var restored = Host(directory);
        Assert.Equal(size, restored.Browser.IconSize);
        Assert.Equal(size, new NavigationPreferenceStore().Read());
    });

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("undefined")]
    [InlineData("unreadable")]
    public void StudioFallsBackToSmallForBadIconPreferences(string fault) => RunSta(directory =>
    {
        var store = new NavigationPreferenceStore();
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        if (fault is "invalid" or "undefined") File.WriteAllText(store.FilePath, fault == "invalid" ? "broken" : "128");
        if (fault == "unreadable") Directory.CreateDirectory(store.FilePath);
        using var studio = Host(directory);
        Assert.Equal(NavigationIconSize.Small, studio.Browser.IconSize);
        studio.RebuildViewMenu();
        var view = (ToolStripMenuItem)studio.MainMenuStrip!.Items[1];
        var menu = view.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "Icon Size");
        if (fault == "unreadable") Directory.Delete(store.FilePath);
        menu.DropDownItems.Cast<ToolStripMenuItem>().Single(i => i.Text == "Large").PerformClick();
        Assert.Equal(NavigationIconSize.Large, studio.Browser.IconSize);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(store.FilePath)!, "*.tmp"));
    });

    /// <summary>Opening, activating and closing tabs keeps the browser highlight on the active file.</summary>
    [Fact]
    public void ActiveStudioTabsSelectWorkingDirectoryEntry() => RunSta(directory =>
    {
        var firstPath = Path.Combine(directory, "beach.gscn");
        var secondPath = Path.Combine(directory, "mountain.gscn");
        File.WriteAllText(firstPath, NativeJson("gscn"));
        File.WriteAllText(secondPath, NativeJson("gscn"));
        using var studio = Host(directory);

        var beach = studio.OpenDocument(firstPath);
        Assert.Equal(firstPath, studio.Browser.Tree.SelectedNode?.Tag);
        var mountain = studio.OpenDocument(secondPath);
        Assert.Equal(secondPath, studio.Browser.Tree.SelectedNode?.Tag);

        beach.Activate();
        Application.DoEvents();
        Assert.Same(beach, studio.ActiveDocument);
        Assert.Equal(firstPath, studio.Browser.Tree.SelectedNode?.Tag);

        studio.Browser.RefreshDirectory();
        Assert.Equal(firstPath, studio.Browser.Tree.SelectedNode?.Tag);

        mountain.Activate();
        Application.DoEvents();
        Assert.Same(mountain, studio.ActiveDocument);
        Assert.Equal(secondPath, studio.Browser.Tree.SelectedNode?.Tag);

        var untitled = studio.NewDocument("gscn");
        Assert.Same(untitled, studio.ActiveDocument);
        Assert.Null(studio.Browser.Tree.SelectedNode);

        beach.Activate();
        Application.DoEvents();
        Assert.Equal(firstPath, studio.Browser.Tree.SelectedNode?.Tag);
        beach.Close();
        Application.DoEvents();
        Assert.Equal(studio.ActiveDocument?.Document.Location?.Path, studio.Browser.Tree.SelectedNode?.Tag);
    });

    /// <summary>Active loose files are revealed through lazy subfolders and restored after refresh.</summary>
    [Fact]
    public void ActiveLooseDocumentSelectionExpandsOnlyItsAncestors() => RunSta(directory =>
    {
        string folder = Directory.CreateDirectory(Path.Combine(directory, "levels", "shore")).FullName;
        string beach = Path.Combine(folder, "beach.gscn");
        File.WriteAllText(beach, "{}");
        string unrelated = Directory.CreateDirectory(Path.Combine(directory, "other")).FullName;
        File.WriteAllText(Path.Combine(unrelated, "other.gscn"), "{}");

        using var panel = new DirectoryPanel(_ => { });
        using var host = new Form { Size = new(600, 600), Opacity = 0, ShowInTaskbar = false };
        host.Controls.Add(panel);
        host.Show();
        panel.SetDirectory(directory);
        var untouched = panel.Tree.Nodes[0].Nodes.Cast<TreeNode>()
            .Single(node => node.Tag is DirectoryInfo info && info.Name == "other");
        Assert.Null(Assert.Single(untouched.Nodes.Cast<TreeNode>()).Tag);

        panel.SelectActiveDocument(new AssetLocation(AssetTypes.SceneDefinition, beach));
        Assert.Equal(beach, panel.Tree.SelectedNode?.Tag);
        Assert.Equal("beach.gscn", panel.Tree.SelectedNode?.Text);
        Assert.Null(Assert.Single(untouched.Nodes.Cast<TreeNode>()).Tag);

        panel.RefreshDirectory();
        Assert.Equal(beach, panel.Tree.SelectedNode?.Tag);
        panel.ApplyIconSize(NavigationIconSize.Medium);
        Assert.Equal(beach, panel.Tree.SelectedNode?.Tag);

        // Outside the current root and untitled documents must not leave stale highlights.
        panel.SelectActiveDocument(new AssetLocation(AssetTypes.SceneDefinition, Path.Combine(Path.GetTempPath(), "outside.gscn")));
        Assert.Null(panel.Tree.SelectedNode);
        panel.SelectActiveDocument(null);
        Assert.Null(panel.Tree.SelectedNode);
    });

    /// <summary>Packed locations select by physical archive, entry and type, even when loaded asynchronously.</summary>
    [Fact]
    public void ActivePackedDocumentSelectionWaitsForArchivePublication() => RunSta(directory =>
    {
        string first = Path.Combine(directory, "first.gaf");
        string second = Path.Combine(directory, "second.gaf");
        foreach (var path in new[] { first, second })
        {
            using var archive = AssetsFile.LoadOrCreate(path, null, false, register: false);
            archive.Add(AssetTypes.SceneDefinition, "beach.gscn", new MemoryStream([1]));
            archive.Add(AssetTypes.TilesheetDefinition, "beach.gscn", new MemoryStream([2]));
            archive.Save();
        }

        using var panel = new DirectoryPanel(_ => { });
        using var host = new Form { Size = new(600, 600), Opacity = 0, ShowInTaskbar = false };
        host.Controls.Add(panel);
        host.Show();
        panel.SetDirectory(directory);
        var selectedLocation = new AssetLocation(AssetTypes.SceneDefinition, second, "beach.gscn");
        panel.SelectActiveDocument(selectedLocation);
        WaitForArchives(panel);
        var selected = Assert.IsType<PackedAssetNodeIdentity>(panel.Tree.SelectedNode?.Tag);
        Assert.Equal(second, selected.ArchivePath);
        Assert.Equal(AssetTypes.SceneDefinition, selected.AssetType);
        Assert.Equal("beach.gscn", selected.EntryName);

        panel.RefreshDirectory();
        WaitForArchives(panel);
        selected = Assert.IsType<PackedAssetNodeIdentity>(panel.Tree.SelectedNode?.Tag);
        Assert.Equal(second, selected.ArchivePath);

        panel.SelectActiveDocument(new AssetLocation(AssetTypes.TilesheetDefinition, first, "beach.gscn"));
        selected = Assert.IsType<PackedAssetNodeIdentity>(panel.Tree.SelectedNode?.Tag);
        Assert.Equal(first, selected.ArchivePath);
        Assert.Equal(AssetTypes.TilesheetDefinition, selected.AssetType);

        panel.SelectActiveDocument(new AssetLocation(AssetTypes.SceneDefinition, first, "missing.gscn"));
        Assert.Null(panel.Tree.SelectedNode);
    });

    private static void WriteBrowserArchive(string path, string entry)
    {
        using var archive = AssetsFile.LoadOrCreate(path, null, false, register: false);
        archive.Add(AssetTypes.TilesheetDefinition, entry, new MemoryStream([1]));
        archive.Save();
    }

    private static T AwaitUi<T>(Task<T> task)
    {
        var context = SynchronizationContext.Current;
        var timeout = Stopwatch.StartNew();
        while (!task.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(20))
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }
        SynchronizationContext.SetSynchronizationContext(context);
        Assert.True(task.IsCompleted, "UI operation timed out.");
        return task.GetAwaiter().GetResult();
    }

    [Theory]
    [InlineData("save")]
    [InlineData("saveAs")]
    [InlineData("close")]
    [InlineData("shutdown")]
    public void GafSaveWaitsAsynchronouslyForActiveArchiveReader(string command) => RunSta(directory =>
    {
        var path = Path.Combine(directory, "active.gaf");
        using (var initial = AssetsFile.LoadOrCreate(path, null, false, register: false))
        {
            initial.Add(AssetTypes.Misc, "existing.bin", new MemoryStream([1]));
            initial.Save();
        }
        using var studio = Host(directory);
        WaitForArchives(studio.Browser);
        var document = studio.OpenDocument(path);
        var editor = Assert.IsType<Gondwana.Tooling.Assets.WinForms.AssetEditorControl>(document.Document.Editor);
        editor.AssetsFile.Add(AssetTypes.Misc, "changed.bin", new MemoryStream([2, 3]));
        editor.MarkChanged();
        using var opened = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        studio.Browser.ArchiveOpened = _ =>
        {
            opened.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(15)), "Test did not release the archive reader.");
        };
        studio.Browser.RefreshDirectory();
        Assert.True(opened.Wait(TimeSpan.FromSeconds(10)));
        try
        {
            // Prove the live read handle conflicts with the exact sharing mode used by Save.
            Assert.Throws<IOException>(() => File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None));
            var destination = command == "saveAs" ? Path.Combine(directory, "copy.gaf") : path;
            studio.AskSave = _ => DialogResult.Yes;
            if (command == "close") document.Close();
            if (command == "shutdown") studio.Close();
            var save = studio.SaveDocument(document, command == "saveAs", destination);
            Assert.False(save.IsCompleted);
            bool uiResponded = false;
            studio.BeginInvoke(() => uiResponded = true);
            Application.DoEvents();
            Assert.True(uiResponded);
            Assert.False(save.IsCompleted);
            Assert.True(document.Document.Dirty());
            Assert.False(document.Enabled);
            Assert.Same(save, studio.SaveDocument(document));
            studio.Browser.ArchiveOpened = null;
            release.Set();
            Assert.True(AwaitUi(save));
            if (command is "close" or "shutdown")
            {
                var timeout = Stopwatch.StartNew();
                while (!document.IsDisposed && timeout.Elapsed < TimeSpan.FromSeconds(10)) Application.DoEvents();
                Assert.True(document.IsDisposed);
                if (command == "shutdown") Assert.True(studio.IsDisposed);
            }
            else
            {
                Assert.True(document.Enabled);
                Assert.False(document.Document.Dirty());
                Assert.Equal(destination, document.Document.Path());
            }
            using var saved = AssetsFile.LoadOrCreate(destination, null, false, register: false);
            Assert.Equal(2, saved.GetAllEntries().Count());
            using var changed = saved[AssetTypes.Misc, "changed.bin"];
            Assert.Equal(2, changed!.ReadByte());
            if (!studio.IsDisposed) WaitForArchives(studio.Browser);
        }
        finally
        {
            studio.Browser.ArchiveOpened = null;
            release.Set();
        }
    });

    private static void DoubleClickNode(DirectoryPanel panel, TreeNode node) =>
        typeof(TreeView).GetMethod("OnNodeMouseDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(panel.Tree, [new TreeNodeMouseClickEventArgs(node, MouseButtons.Left, 2, 0, 0)]);

    private static void WaitForArchives(DirectoryPanel panel)
    {
        var timeout = Stopwatch.StartNew();
        while (!panel.PendingArchiveLoads.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(20))
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }
        Assert.True(panel.PendingArchiveLoads.IsCompleted, "Archive enumeration timed out.");
        panel.PendingArchiveLoads.GetAwaiter().GetResult();
    }
}
