using System.Runtime.ExceptionServices;
using Gondwana.Tooling.Studio.Core.Extensibility;
using Gondwana.Tooling.Studio.WinForms.Panels;
using Gondwana.Tooling.Studio.WinForms.Services;
using WeifenLuo.WinFormsUI.Docking;
using Host = Gondwana.Tooling.Studio.Core.Extensibility.StudioPluginHost;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class PluginPackageTests
{
    [Fact]
    public void ManagerShowsMetadataAllStatesAndLiveLifecycleFailures() => OnSta(() =>
    {
        Package("loaded", "sample.loaded");
        Package("disabled", "sample.disabled");
        Package("runtime", "sample.runtime");
        Package("failed", "sample.failed", source: PluginSource("Failed", constructor: "throw new System.Exception(\"Constructor needs a dependency\");"));
        Package("conflict-a", "sample.conflict");
        Package("conflict-b", "sample.conflict");
        Directory.CreateDirectory(Path.Combine(_root, "invalid"));
        var incompatible = Package("incompatible", "sample.incompatible");
        var path = Path.Combine(Path.GetDirectoryName(incompatible)!, "plugin.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"contractVersion\":1", "\"contractVersion\":2", StringComparison.Ordinal));
        var preferences = new PluginPreferences(Path.Combine(_root, "preferences.json"), _ => { });
        preferences.TrySetEnabled("sample.disabled", false, out _);
        preferences = new PluginPreferences(preferences.FilePath, _ => { });
        var host = new Host(_ => { }, preferences.WasEnabledAtStartup);
        using var manager = new PluginManagerPanel(host, preferences);
        using var form = new Form();
        form.Controls.Add(manager);
        manager.Dock = DockStyle.Fill;
        form.Show();
        host.DiscoverAndLoad(_root);
        Assert.Equal(7, manager.Grid.Rows.Count); // Two conflict sources share one package ID/choice.
        foreach (var status in new[] { "Loaded", "Disabled by preference", "Failed to load", "Incompatible", "Identity conflict", "Invalid package" })
            Assert.Contains(manager.Grid.Rows.Cast<DataGridViewRow>(), row => row.Cells[2].Value?.ToString() == status);
        Select(manager, "sample.failed");
        Assert.Contains("Constructor needs a dependency", manager.DetailsBox.Text);
        Assert.Contains("System.Exception", manager.DetailsBox.Text);
        Assert.Contains("Suggested action:", manager.DetailsBox.Text);
        Assert.Contains("Entry assembly:", manager.DetailsBox.Text);
        Assert.True(manager.DetailsBox.ReadOnly);
        manager.DetailsBox.SelectAll();
        Assert.NotEmpty(manager.DetailsBox.SelectedText);
        Select(manager, "sample.loaded");
        Assert.Contains("Version: 1.0.0", manager.DetailsBox.Text);
        host.NotifyProjectOpened("throw");
        Assert.Contains("Disabled after runtime failure", manager.DetailsBox.Text);
        Assert.DoesNotContain("Restart required", manager.StatusLabel.Text);
    });

    [Fact]
    public void ManagerSavesNextStartupAndRevertingClearsNoticeWithoutChangingRuntime() => OnSta(() =>
    {
        Package("one", "sample.one", source: PluginSource("First") + PluginSource("Second"));
        var preferences = new PluginPreferences(Path.Combine(_root, "preferences.json"), _ => { });
        var host = new Host(_ => { }, preferences.WasEnabledAtStartup);
        host.DiscoverAndLoad(_root);
        using var manager = new PluginManagerPanel(host, preferences);
        using var form = new Form();
        form.Controls.Add(manager);
        form.Show();
        Assert.Single(manager.Grid.Rows.Cast<DataGridViewRow>());
        Select(manager, "sample.one");
        Assert.True(manager.EnabledChoice.Checked);
        var instances = host.Plugins.ToArray();
        manager.EnabledChoice.Checked = false;
        Assert.Contains("Restart required", manager.StatusLabel.Text);
        Assert.Contains("restart required", manager.Grid.Rows[0].Cells[3].Value.ToString());
        Assert.Equal(instances, host.Plugins);
        Assert.False(new PluginPreferences(preferences.FilePath, _ => { }).IsEnabled("sample.one"));
        manager.EnabledChoice.Checked = true;
        Assert.DoesNotContain("Restart required", manager.StatusLabel.Text);
        Assert.Equal(instances, host.Plugins);
        Assert.True(new PluginPreferences(preferences.FilePath, _ => { }).IsEnabled("sample.one"));
    });

    [Fact]
    public void ManagerRollsBackCheckboxAndReportsUnwritableSettings() => OnSta(() =>
    {
        Package("one", "sample.one");
        var path = Path.Combine(_root, "preferences.json");
        var preferences = new PluginPreferences(path, _ => { });
        var host = new Host(_ => { });
        // Discover only the valid package root before the obstructing settings directory is added.
        host.DiscoverAndLoad(_root);
        Directory.CreateDirectory(path);
        using var manager = new PluginManagerPanel(host, preferences);
        using var form = new Form();
        form.Controls.Add(manager);
        form.Show();
        Select(manager, "sample.one");
        manager.EnabledChoice.Checked = false;
        Assert.True(manager.EnabledChoice.Checked);
        Assert.Contains("Could not save", manager.StatusLabel.Text);
        Assert.False(preferences.RestartRequired);
        Assert.Single(host.Plugins);
    });

    [Fact]
    public void DisabledBundledPluginsHaveNoContributionsAndManagerRemainsAvailable() => OnSta(() =>
    {
        var preferences = PluginPreferenceStore.Open(_ => { });
        preferences.TrySetEnabled("gondwana.project-diagnostics", false, out _);
        preferences.TrySetEnabled("gondwana.external-import", false, out _);
        using var studio = new MainForm(loadPlugins: true) { ShowInTaskbar = false };
        studio.Show();
        Application.DoEvents();
        Assert.Empty(studio.PluginHost.Plugins);
        Assert.Empty(studio.PluginHost.GetPluginMenuItems());
        Assert.Empty(studio.PluginHost.GetPluginPanels());
        studio.PluginHost.AttachHostServices(studio);
        studio.PluginHost.NotifyProjectOpened("throw");
        studio.PluginHost.NotifyProjectClosed();
        Assert.All(studio.PluginHost.Discovery, row => Assert.Equal(PluginLoadStatus.UserDisabled, row.Status));
        var menu = (ToolStripMenuItem)studio.MainMenuStrip!.Items["PluginsMenu"]!;
        Assert.Single(menu.DropDownItems.OfType<ToolStripMenuItem>());
        menu.DropDownItems["ManagePlugins"]!.PerformClick();
        var pane = studio.Workspace.Contents.OfType<DockContent>().Single(tool => tool.Text == "Plugin Manager");
        Assert.False(pane.IsHidden);
        Assert.Equal(2, studio.PluginManager.Grid.Rows.Count);
        Assert.Contains("Project Diagnostics", studio.PluginManager.Grid.Rows.Cast<DataGridViewRow>().Select(row => row.Cells[0].Value));
    });

    [Fact]
    public void ManagerRestoresDockLayoutAndBothMenusWorkWithoutPlugins() => OnSta(() =>
    {
        var missing = Path.Combine(_root, "absent");
        using (var studio = new MainForm(loadPlugins: true, pluginDirectory: missing) { ShowInTaskbar = false })
        {
            studio.Show();
            Application.DoEvents();
            Assert.Contains("No plugins", studio.PluginManager.StatusLabel.Text);
            Assert.False(studio.PluginManager.EnabledChoice.Enabled);
            var menu = (ToolStripMenuItem)studio.MainMenuStrip!.Items["PluginsMenu"]!;
            menu.DropDownItems["ManagePlugins"]!.PerformClick();
            var pane = studio.Workspace.Contents.OfType<DockContent>().Single(tool => tool.Text == "Plugin Manager");
            Assert.False(pane.IsHidden);
            pane.Show(studio.Workspace, DockState.DockBottom);
            pane.Close();
            Assert.True(pane.IsHidden);
            Assert.False(pane.IsDisposed);
        }
        using (var restored = new MainForm(loadPlugins: true, pluginDirectory: missing) { ShowInTaskbar = false })
        {
            restored.Show();
            Application.DoEvents();
            var pane = restored.Workspace.Contents.OfType<DockContent>().Single(tool => tool.Text == "Plugin Manager");
            Assert.True(pane.IsHidden);
            Assert.Equal(DockState.DockBottom, pane.Pane.DockState);
            restored.RebuildViewMenu();
            var view = restored.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "&View");
            view.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "Plugin Manager").PerformClick();
            Assert.False(pane.IsHidden);
            Assert.Equal(DockState.DockBottom, pane.DockState);
        }
    });

    private static void Select(PluginManagerPanel manager, string id)
    {
        var row = manager.Grid.Rows.Cast<DataGridViewRow>().Single(row => row.Tag is PluginDiscoveryInfo[] records && records[0].PreferenceId == id);
        manager.Grid.CurrentCell = row.Cells[0];
    }

    private static void OnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
