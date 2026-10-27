using System.Reflection;
using Gondwana.Tooling.WinForms;
using Gondwana.Tooling.WinForms.Theming;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Theory]
    [InlineData(NavigationIconSize.Small)]
    [InlineData(NavigationIconSize.Medium)]
    [InlineData(NavigationIconSize.Large)]
    public void NavigationPreferenceMigratesStudioAndIsSharedAcrossApplicationIds(NavigationIconSize size)
    {
        var root = (string)AppContext.GetData("Gondwana.Tooling.SettingsRoot")!;
        // Exercise Studio's actual assembly name as well as the configured test-host scope.
        AppContext.SetData("Gondwana.Tooling.ApplicationId", null);
        var legacy = Path.Combine(root, "Gondwana.Studio.WinForms", "Studio", "directory-icon-size.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, size.ToString());
        var studio = new NavigationPreferenceStore();
        Assert.Equal(size, studio.Read());
        Assert.Equal(size.ToString(), File.ReadAllText(studio.FilePath));
        File.WriteAllText(legacy, "Small"); // The migrated preference takes precedence.
        foreach (var app in new[] { "Assets", "Tilesheets", "Animations", "Audio", "Scenes", "Sprites", "Studio" })
        {
            AppContext.SetData("Gondwana.Tooling.ApplicationId", app);
            var standalone = new NavigationPreferenceStore();
            Assert.Equal(studio.FilePath, standalone.FilePath);
            Assert.Equal(size, standalone.Read());
        }
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }

    [Fact]
    public void ConfiguredStudioScopeMigratesAndInvalidSharedPreferenceDoesNotResurrectLegacy()
    {
        var root = (string)AppContext.GetData("Gondwana.Tooling.SettingsRoot")!;
        var legacy = Path.Combine(root, "test-host", "Studio", "directory-icon-size.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, "Large");
        var store = new NavigationPreferenceStore();
        Assert.Equal(NavigationIconSize.Large, store.Read());
        File.WriteAllText(store.FilePath, "128");
        Assert.Null(store.Read());
        File.WriteAllText(store.FilePath, new string('x', 5000));
        Assert.Null(store.Read());
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Write((NavigationIconSize)128));
    }

    [Fact]
    public void DelayedMigrationReadsTheWinningExplicitChoiceWithoutOverwritingIt()
    {
        var messages = new List<string>();
        var migrating = new NavigationPreferenceStore(report: messages.Add);
        Assert.False(File.Exists(migrating.FilePath));
        // The migration has already read legacy Large, then another application saves Small.
        var explicitWriter = new NavigationPreferenceStore();
        Assert.True(explicitWriter.Write(NavigationIconSize.Small));
        Assert.Equal(NavigationIconSize.Small, migrating.Migrate(NavigationIconSize.Large));
        Assert.Equal("Small", File.ReadAllText(migrating.FilePath));
        Assert.Empty(messages); // Losing a migration race is expected, not a persistence error.
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(migrating.FilePath)!, "*.tmp"));
        Assert.True(explicitWriter.Write(NavigationIconSize.Medium));
        Assert.Equal(NavigationIconSize.Medium, migrating.Read()); // Explicit changes still replace earlier choices.
    }

    [Theory]
    [InlineData(96, 16, 32, 64)]
    [InlineData(120, 20, 40, 80)]
    [InlineData(144, 24, 48, 96)]
    [InlineData(192, 32, 64, 128)]
    [InlineData(384, 64, 128, 256)]
    [InlineData(768, 128, 256, 256)]
    public void NavigationMetricsScaleDevicePixelsButKeepPointFonts(int dpi, int small, int medium, int large)
    {
        Assert.Equal(small, NavigationSizing.IconPixels(NavigationIconSize.Small, dpi));
        Assert.Equal(medium, NavigationSizing.IconPixels(NavigationIconSize.Medium, dpi));
        Assert.Equal(large, NavigationSizing.IconPixels(NavigationIconSize.Large, dpi));
        Assert.Equal(new[] { 9f, 11f, 13f }, Enum.GetValues<NavigationIconSize>().Select(NavigationSizing.FontPoints));
        Assert.Throws<ArgumentOutOfRangeException>(() => NavigationSizing.IconPixels((NavigationIconSize)128, dpi));
        Assert.Throws<ArgumentOutOfRangeException>(() => NavigationSizing.IconPixels(NavigationIconSize.Small, 0));
    }

    [Fact]
    public void NavigationDefaultsRemainUntouchedUntilAnExplicitChoiceAndFailedSaveRetainsPresentation() => RunSta(directory =>
    {
        using var owner = new Form();
        using var tree = new TreeView { ItemHeight = 37 };
        using var font = new Font("Segoe UI", 10f);
        tree.Font = font;
        using var images = new ImageList { ImageSize = new Size(32, 32) };
        tree.ImageList = images;
        using var binding = NavigationSizing.Attach(tree);
        Assert.Null(binding.Size);
        Assert.Same(font, tree.Font);
        Assert.Same(images, tree.ImageList);
        Assert.Equal(37, tree.ItemHeight);
        var warnings = new List<string>();
        using var menu = NavigationSizing.CreateMenu(owner, notify: warnings.Add);
        var store = new NavigationPreferenceStore();
        Directory.CreateDirectory(store.FilePath); // Save is blocked by a directory at the destination.
        menu.DropDownItems.Cast<ToolStripMenuItem>().Single(i => i.Text == "Large").PerformClick();
        Assert.Single(warnings);
        Assert.Null(binding.Size);
        Assert.Same(font, tree.Font);
        Assert.Null(store.Read());
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(store.FilePath)!, "*.tmp"));
    });

    [Fact]
    public void AnotherApplicationsPreferenceIsAppliedOnActivationWithoutRestart() => RunSta(directory =>
    {
        var store = new NavigationPreferenceStore();
        Assert.True(store.Write(NavigationIconSize.Small));
        using var owner = new Form();
        using var tree = new TreeView();
        owner.Controls.Add(tree);
        using var binding = NavigationSizing.Attach(tree);
        using var menu = NavigationSizing.CreateMenu(owner);
        Assert.Equal(9f, tree.Font.SizeInPoints);
        Assert.True(store.Write(NavigationIconSize.Large)); // Another executable updates the same file.
        typeof(Form).GetMethod("OnActivated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(owner, new object[] { EventArgs.Empty });
        Assert.Equal(13f, tree.Font.SizeInPoints);
        Assert.Equal(NavigationIconSize.Large, Assert.Single(menu.DropDownItems.Cast<ToolStripMenuItem>(), i => i.Checked).Tag);
    });

    [Fact]
    public void StudioActivationRefreshesBeforeViewIsEverOpenedAndAfterMenuRebuilds() => RunSta(directory =>
    {
        var store = new NavigationPreferenceStore();
        Assert.True(store.Write(NavigationIconSize.Small));
        using var studio = Host(directory);
        var view = studio.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "&View");
        Assert.Empty(view.DropDownItems.Cast<ToolStripItem>());
        Assert.True(store.Write(NavigationIconSize.Large));
        typeof(Form).GetMethod("OnActivated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(studio, new object[] { EventArgs.Empty });
        Assert.Equal(NavigationIconSize.Large, studio.Browser.IconSize);
        studio.RebuildViewMenu();
        studio.RebuildViewMenu();
        // Menu disposal must not unregister the shell's activation synchronization.
        foreach (ToolStripItem item in view.DropDownItems.Cast<ToolStripItem>().ToArray()) item.Dispose();
        Assert.True(store.Write(NavigationIconSize.Medium));
        typeof(Form).GetMethod("OnActivated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(studio, new object[] { EventArgs.Empty });
        Assert.Equal(NavigationIconSize.Medium, studio.Browser.IconSize);
    });

    [Fact]
    public void IsolatedMenusAndBindingsShareOneSourceWithoutChangingOtherScopes() => RunSta(directory =>
    {
        var global = new NavigationPreferenceStore();
        Assert.True(global.Write(NavigationIconSize.Small));
        var isolated = new NavigationPreferenceStore(Path.Combine(directory, "isolated-preferences"));
        Assert.True(isolated.Write(NavigationIconSize.Medium));
        using var owner = new Form();
        using var isolatedTree = new TreeView();
        using var sameScopeTree = new TreeView();
        using var globalTree = new TreeView();
        owner.Controls.AddRange(new Control[] { isolatedTree, sameScopeTree });
        using var binding = NavigationSizing.Attach(isolatedTree, store: isolated);
        using var sameScopeBinding = NavigationSizing.Attach(sameScopeTree,
            store: new NavigationPreferenceStore(Path.GetDirectoryName(isolated.FilePath)));
        using var globalBinding = NavigationSizing.Attach(globalTree);
        using var menu = NavigationSizing.CreateMenu(owner, isolated);
        Assert.Equal(11f, isolatedTree.Font.SizeInPoints);
        menu.DropDownItems.Cast<ToolStripMenuItem>().Single(i => i.Text == "Large").PerformClick();
        Assert.Equal(NavigationIconSize.Large, isolated.Read());
        Assert.Equal(13f, isolatedTree.Font.SizeInPoints);
        Assert.Equal(13f, sameScopeTree.Font.SizeInPoints);
        Assert.Equal(NavigationIconSize.Large, Assert.Single(menu.DropDownItems.Cast<ToolStripMenuItem>(), i => i.Checked).Tag);
        Assert.Equal(9f, globalTree.Font.SizeInPoints);
        Assert.Equal(NavigationIconSize.Small, global.Read());
        Assert.True(isolated.Write(NavigationIconSize.Small));
        typeof(Form).GetMethod("OnActivated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(owner, new object[] { EventArgs.Empty });
        Assert.Equal(9f, isolatedTree.Font.SizeInPoints);
        Assert.Equal(9f, sameScopeTree.Font.SizeInPoints);
        Assert.Equal(NavigationIconSize.Small, Assert.Single(menu.DropDownItems.Cast<ToolStripMenuItem>(), i => i.Checked).Tag);
        Assert.Throws<ArgumentException>(() => NavigationSizing.Attach(isolatedTree, store: global));
        Assert.Throws<ArgumentException>(() => NavigationSizing.CreateMenu(owner, global));
    });

    [Fact]
    public void NavigationThumbnailRefreshKeepsOriginalKeysPixelsNodesAndSelection() => RunSta(directory =>
    {
        using var owner = new Form { Size = new Size(600, 600), Opacity = 0, ShowInTaskbar = false };
        using var images = new ImageList { ImageSize = new Size(32, 32), ColorDepth = ColorDepth.Depth32Bit };
        using var red = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(red)) graphics.Clear(Color.Red);
        images.Images.Add("red", red);
        _ = images.Handle;
        using var tree = new TreeView { Dock = DockStyle.Fill, ImageList = images };
        owner.Controls.Add(tree);
        var root = tree.Nodes.Add("root");
        var selected = root.Nodes.Add("selected");
        selected.ImageKey = selected.SelectedImageKey = "red";
        owner.Show();
        root.Expand();
        tree.SelectedNode = selected;
        int selections = 0;
        tree.AfterSelect += (_, _) => selections++;
        using var binding = NavigationSizing.Attach(tree);
        using var menu = NavigationSizing.CreateMenu(owner);
        menu.DropDownItems.Cast<ToolStripMenuItem>().Single(i => i.Text == "Large").PerformClick();
        var first = tree.ImageList!;
        Assert.NotSame(images, first);
        Assert.Equal("red", first.Images.Keys[0]);
        using (var pixel = new Bitmap(first.Images[0])) Assert.Equal(Color.Red.ToArgb(), pixel.GetPixel(20, 20).ToArgb());
        bool disposed = false;
        first.Disposed += (_, _) => disposed = true;
        using var blue = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(blue)) graphics.Clear(Color.Blue);
        images.Images.Add(blue); // Lazy frame thumbnails use numeric indices without keys.
        NavigationSizing.RefreshImages(tree);
        Assert.True(disposed);
        Assert.Equal(2, tree.ImageList!.Images.Count);
        Assert.Equal("red", tree.ImageList.Images.Keys[0]);
        Assert.True(string.IsNullOrEmpty(tree.ImageList.Images.Keys[1]));
        using (var pixel = new Bitmap(tree.ImageList.Images[1])) Assert.Equal(Color.Blue.ToArgb(), pixel.GetPixel(20, 20).ToArgb());
        Assert.Equal(new Size(32, 32), images.ImageSize);
        Assert.Same(selected, tree.SelectedNode);
        Assert.Equal("red", selected.ImageKey);
        Assert.True(root.IsExpanded);
        Assert.Equal(0, selections);
        var currentImages = tree.ImageList;
        var currentFont = tree.Font;
        binding.Apply(NavigationIconSize.Large); // Same size/DPI is a no-op.
        Assert.Same(currentImages, tree.ImageList);
        Assert.Same(currentFont, tree.Font);
        images.Images.Clear();
        NavigationSizing.RefreshImages(tree);
        Assert.Empty(tree.ImageList!.Images.Cast<Image>());
    });

    [Theory]
    [InlineData("Assets")]
    [InlineData("Tilesheets")]
    [InlineData("Animations")]
    [InlineData("Audio")]
    [InlineData("Scenes")]
    [InlineData("Sprites")]
    public void AllStandaloneMenusResizeOnlyNavigationAndRestoreTheSharedChoice(string app) => RunSta(directory =>
    {
        string previousDirectory = Environment.CurrentDirectory;
        Environment.CurrentDirectory = directory;
        try
        {
            var assembly = Assembly.Load($"Gondwana.Tooling.{app}.WinForms");
            var type = assembly.GetType($"Gondwana.Tooling.{app}.WinForms.MainForm", throwOnError: true)!;
            using var shell = (Form)Activator.CreateInstance(type, nonPublic: true)!;
            shell.Opacity = 0;
            shell.ShowInTaskbar = false;
            shell.Show();
            if (app == "Assets")
                type.GetMethod("OpenDocument", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                    .Invoke(shell, new object?[] { Path.Combine(directory, "test.gaf") });
            else
            {
                var create = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .First(m => m.Name == "NewDocument" && m.GetParameters().All(p => p.IsOptional));
                create.Invoke(shell, create.GetParameters().Select(p => p.DefaultValue).ToArray());
            }
            Application.DoEvents();
            var trees = Descendants(shell).OfType<TreeView>().ToArray();
            Assert.NotEmpty(trees);
            var untouched = Descendants(shell).Where(c => c is PropertyGrid or ToolStrip ||
                c.GetType().Name.Contains("PreviewControl") || c.GetType().Name == "ImageViewport")
                .ToDictionary(c => c, c => (c.Font, c.Size, c.BackColor));
            var selected = trees.ToDictionary(tree => tree, tree => tree.SelectedNode);
            var roots = trees.ToDictionary(tree => tree, tree => tree.Nodes.Cast<TreeNode>().ToArray());
            var view = shell.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "&View");
            var menu = view.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "Icon Size");
            Assert.Equal(new[] { "Small", "Medium", "Large" }, menu.DropDownItems.Cast<ToolStripMenuItem>().Select(i => i.Text));
            foreach (var size in Enum.GetValues<NavigationIconSize>())
            {
                menu.DropDownItems.Cast<ToolStripMenuItem>().Single(i => Equals(i.Tag, size)).PerformClick();
                Assert.Equal(size, Assert.Single(menu.DropDownItems.Cast<ToolStripMenuItem>(), i => i.Checked).Tag);
                Assert.Equal(size, new NavigationPreferenceStore().Read());
                foreach (var tree in trees)
                {
                    Assert.Equal(NavigationSizing.FontPoints(size), tree.Font.SizeInPoints);
                    Assert.True(tree.ItemHeight > NavigationSizing.IconPixels(size, tree.DeviceDpi));
                    if (tree.ImageList is { } list)
                        Assert.Equal(NavigationSizing.IconPixels(size, tree.DeviceDpi), list.ImageSize.Width);
                    Assert.Equal(roots[tree], tree.Nodes.Cast<TreeNode>());
                    Assert.Same(selected[tree], tree.SelectedNode);
                    Assert.Equal(ToolingAppearance.Palette.Background, tree.BackColor);
                }
                foreach (var (control, snapshot) in untouched)
                {
                    Assert.Same(snapshot.Font, control.Font);
                    Assert.Equal(snapshot.Size, control.Size);
                    Assert.Equal(snapshot.BackColor, control.BackColor);
                }
            }
            using var studio = Host(directory);
            Assert.Equal(NavigationIconSize.Large, studio.Browser.IconSize);
            using var reopened = (Form)Activator.CreateInstance(type, nonPublic: true)!;
            reopened.Opacity = 0;
            reopened.ShowInTaskbar = false;
            reopened.Show(); // DockPanelSuite attaches the workspace controls when the shell is shown.
            Application.DoEvents();
            var reopenedTrees = Descendants(reopened).OfType<TreeView>().ToArray();
            Assert.NotEmpty(reopenedTrees);
            Assert.All(reopenedTrees, tree => Assert.Equal(13f, tree.Font.SizeInPoints));
        }
        finally { Environment.CurrentDirectory = previousDirectory; }
    });
}
