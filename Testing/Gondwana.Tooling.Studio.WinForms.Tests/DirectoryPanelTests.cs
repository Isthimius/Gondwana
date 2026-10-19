using System.Diagnostics;
using System.Reflection;
using Gondwana.Assets;
using Gondwana.Tooling.Studio.WinForms.Panels;

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
            File.WriteAllText(Path.Combine(directory, "z." + extension), "unchanged");
        foreach (var name in new[] { "a.gaf", "b.GAF" })
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
            Assert.IsType<string>(groups[group].Nodes[0].Tag);
            Assert.All(groups[group].Nodes.Cast<TreeNode>().Skip(1), node =>
            {
                var identity = Assert.IsType<PackedAssetNodeIdentity>(node.Tag);
                Assert.True(Path.IsPathFullyQualified(identity.ArchivePath));
                if (group < 6) Assert.Equal(types[group - 1], identity.AssetType);
                else Assert.DoesNotContain(identity.AssetType, types.Take(5));
                Assert.Equal($"{identity.EntryName} ({Path.GetFileName(identity.ArchivePath)})", node.Text);
                Assert.Equal($"Archive: {identity.ArchivePath}\nEntry: {identity.EntryName}\nAsset type: {identity.AssetType}", node.ToolTipText);
                Assert.Equal(groups[group].ImageKey, node.ImageKey);
                Assert.Equal(node.ImageKey, node.SelectedImageKey);
                Assert.False(panel.PrepareFileMenu(node));
            });
        }
        Assert.Equal(5, groups[1].Nodes.Count);
        Assert.Equal(17, groups[6].Nodes.Count);
        var sameNames = groups[1].Nodes.Cast<TreeNode>().Skip(1)
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
        Assert.Equal(new[] { "loose.gts", "old.gts (good.gaf)" },
            panel.Tree.Nodes[0].Nodes[1].Nodes.Cast<TreeNode>().Select(node => node.Text));
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
        Assert.Equal("new.gts (good.gaf)", Assert.Single(panel.Tree.Nodes[0].Nodes[3].Nodes.Cast<TreeNode>()).Text);
        Assert.DoesNotContain(panel.Tree.Nodes[0].Nodes[1].Nodes.Cast<TreeNode>(), node => node.Text.Contains("old.gts"));
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
        Assert.Equal("entry.gts (nested.gaf)", Assert.Single(folder.Nodes[1].Nodes.Cast<TreeNode>()).Text);
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
        var packed = root.Nodes[1].Nodes[1];
        Assert.False(panel.PrepareFileMenu(packed));
        panel.FileMenu.Items[0].PerformClick(); // A previous loose-file target must not leak.
        DoubleClickNode(panel, packed);
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
        Assert.Equal("entry.gts (nested.gaf)", Assert.Single(panel.Tree.Nodes[0].Nodes[1].Nodes.Cast<TreeNode>()).Text);
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

    private static void WriteBrowserArchive(string path, string entry)
    {
        using var archive = AssetsFile.LoadOrCreate(path, null, false, register: false);
        archive.Add(AssetTypes.TilesheetDefinition, entry, new MemoryStream([1]));
        archive.Save();
    }

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
