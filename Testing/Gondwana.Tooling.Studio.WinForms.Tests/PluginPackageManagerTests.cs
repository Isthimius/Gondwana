using Gondwana.Tooling.Studio.Core.Extensibility;
using Gondwana.Tooling.Studio.WinForms.Panels;
using Host = Gondwana.Tooling.Studio.Core.Extensibility.StudioPluginHost;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class PluginPackageTests
{
    [Fact]
    public void ManagerReviewsPackagesAndCancellationPreservesActivationRestartNotice() => OnSta(() =>
    {
        var source = SourcePackage();
        using var operations = Operations(); operations.StartSession();
        var preferences = new PluginPreferences(Path.Combine(_root, "preferences.json"), _ => { });
        var host = new Host(_ => { }); host.DiscoverAndLoad(InstallRoot);
        using var panel = new PluginManagerPanel(host, preferences, operations);
        using var form = new Form(); form.Controls.Add(panel); panel.Dock = DockStyle.Fill; form.Show();
        Assert.True(panel.InstallButton.Enabled);
        Assert.False(panel.UpdateButton.Enabled);
        Assert.False(panel.RemoveButton.Enabled);
        var reviewed = 0;
        panel.ConfirmPackage = preview =>
        {
            reviewed++;
            Assert.Contains("not a security sandbox", PluginManagerPanel.PreviewText(preview));
            Assert.Contains("sample.package", PluginManagerPanel.PreviewText(preview));
            return reviewed > 1;
        };
        WaitForPackageTask(panel.RunPackageChange(source, null));
        Assert.Empty(operations.Operations);
        WaitForPackageTask(panel.RunPackageChange(source, null));
        Assert.Single(operations.Operations);
        Assert.Single(panel.PendingGrid.Rows.Cast<DataGridViewRow>());
        Assert.Contains("Restart required", panel.StatusLabel.Text);
        Assert.Empty(host.Plugins);
        Assert.True(panel.CancelButton.Enabled);
        preferences.TrySetEnabled("temporarily.missing", false, out _);
        panel.CancelButton.PerformClick();
        WaitForPackageCondition(() => panel.PendingGrid.Rows.Count == 0);
        Assert.Contains("Restart required", panel.StatusLabel.Text);
        preferences.TrySetEnabled("temporarily.missing", true, out _);
        panel.RefreshSnapshot();
        Assert.DoesNotContain("Restart required", panel.StatusLabel.Text);
    });

    [Fact]
    public void ManagerProtectsBundledAndLegacyRowsAndAllowsManagedActions() => OnSta(() =>
    {
        InstallFixture();
        PluginPackageInspection.CopyDirectory(SourcePackage("bundled", "gondwana.project-diagnostics"), Path.Combine(InstallRoot, "ProjectDiagnostics"));
        Compile(Path.Combine(InstallRoot, "Legacy.dll"), PluginSource("Legacy"));
        using var operations = Operations(); operations.StartSession();
        var host = new Host(_ => { }); host.DiscoverAndLoad(InstallRoot);
        using var panel = new PluginManagerPanel(host, new PluginPreferences(Path.Combine(_root, "preferences.json"), _ => { }), operations);
        using var form = new Form(); form.Controls.Add(panel); form.Show();
        Select(panel, "sample.package"); Assert.True(panel.UpdateButton.Enabled); Assert.True(panel.RemoveButton.Enabled);
        Select(panel, "gondwana.project-diagnostics"); Assert.False(panel.UpdateButton.Enabled); Assert.False(panel.RemoveButton.Enabled); Assert.True(panel.EnabledChoice.Enabled);
        Select(panel, host.Discovery.Single(r => r.IsLegacy).PreferenceId!); Assert.False(panel.UpdateButton.Enabled); Assert.False(panel.RemoveButton.Enabled);
        Assert.Contains("legacy flat DLLs", panel.DetailsBox.Text);
    });

    [Fact]
    public void MainFormProcessesPackagesBeforePluginActivation() => OnSta(() =>
    {
        using var operations = Operations();
        operations.Queue(operations.Preview(SourcePackage()), true);
        using var studio = new MainForm(loadPlugins: true, packageOperations: operations) { ShowInTaskbar = false };
        studio.Show(); Application.DoEvents();
        Assert.Empty(operations.Operations);
        Assert.Single(studio.PluginHost.Plugins);
        Assert.Equal("sample.package", Assert.Single(studio.PluginHost.Discovery).Id);
        Assert.True(studio.PluginManager.InstallButton.Enabled);
    });

    [Fact]
    public void ManagerDisplaysFailedOperationDiagnostics() => OnSta(() =>
    {
        var target = InstallFixture();
        using (var queue = Operations()) queue.Queue(queue.PreviewRemoval("sample.package"), true);
        File.WriteAllText(Path.Combine(target, "external.txt"), "changed");
        using var operations = Operations(); operations.StartSession();
        var host = new Host(_ => { }); host.DiscoverAndLoad(InstallRoot);
        using var panel = new PluginManagerPanel(host, new PluginPreferences(Path.Combine(_root, "preferences.json"), _ => { }), operations);
        using var form = new Form(); form.Controls.Add(panel); form.Show();
        Assert.Equal(PluginPackageOperationState.Failed, panel.PendingGrid.Rows[0].Cells[2].Value);
        Assert.Contains("changed or disappeared", panel.PendingGrid.Rows[0].Cells[3].Value.ToString());
        Assert.Contains("operation failed", panel.StatusLabel.Text);
        Assert.True(panel.CancelButton.Enabled);
    });

    [Fact]
    public void BundledDeploymentStillPassesStaticPackageValidation()
    {
        var inspection = new PluginPackageInspection([typeof(IStudioPlugin).Assembly, typeof(Extensibility.IStudioPlugin).Assembly]);
        var root = Path.Combine(AppContext.BaseDirectory, "plugins");
        Assert.Equal("gondwana.project-diagnostics", inspection.Validate(Path.Combine(root, "ProjectDiagnostics")).Id);
        Assert.Equal("gondwana.external-import", inspection.Validate(Path.Combine(root, "ExternalImport")).Id);
    }

    private static void WaitForPackageTask(Task task)
    {
        WaitForPackageCondition(() => task.IsCompleted);
        task.GetAwaiter().GetResult();
    }
    private static void WaitForPackageCondition(Func<bool> completed)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!completed() && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
        Assert.True(completed(), "Package operation did not complete.");
    }
}
