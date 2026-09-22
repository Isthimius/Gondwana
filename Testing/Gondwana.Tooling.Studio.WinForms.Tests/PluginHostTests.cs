using CorePlugin = Gondwana.Tooling.Studio.Core.Extensibility.IStudioPlugin;
using WinPlugin = Gondwana.Tooling.Studio.WinForms.Extensibility.IStudioPlugin;
using Host = Gondwana.Tooling.Studio.WinForms.Extensibility.StudioPluginHost;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed class PluginHostTests
{
    [Fact]
    public void DiscoverySharesBothContractsAndIsolatesLifecycleFailures()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var assembly in new[] { GetType().Assembly, typeof(CorePlugin).Assembly, typeof(WinPlugin).Assembly })
                File.Copy(assembly.Location, Path.Combine(directory, Path.GetFileName(assembly.Location)));
            var messages = new List<string>();
            var host = new Host(messages.Add);
            host.DiscoverAndLoad(directory);
            var plugin = Assert.Single(host.Plugins);
            Assert.IsAssignableFrom<WinPlugin>(plugin);
            Assert.Equal("Contract fixture", plugin.Name);
            host.NotifyProjectOpened(directory);
            Assert.Equal(directory, plugin.GetType().GetProperty("ProjectPath")!.GetValue(plugin));
            host.NotifyProjectClosed();
            Assert.Null(plugin.GetType().GetProperty("ProjectPath")!.GetValue(plugin));
            host.NotifyProjectOpened("throw");
            Assert.Empty(host.Plugins);
            Assert.Contains(messages, message => message.Contains("Disabled 'Contract fixture'"));
        }
        finally { /* Loaded assemblies remain mapped until the test process exits on Windows. */ }
    }

    [Fact]
    public void AbsentPluginsAreOptional()
    {
        var host = new Host(_ => { });
        host.DiscoverAndLoad(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        host.NotifyProjectOpened("project");
        host.NotifyProjectClosed();
        Assert.Empty(host.Plugins);
    }
}

public sealed class ContractFixturePlugin : WinPlugin
{
    public string Name => "Contract fixture";
    public string? ProjectPath { get; private set; }
    public void OnProjectOpened(string projectPath)
    {
        if (projectPath == "throw") throw new InvalidOperationException("Expected fixture failure");
        ProjectPath = projectPath;
    }
    public void OnProjectClosed() => ProjectPath = null;
    public Control? CreatePanel() => null;
    public ToolStripMenuItem? CreateMenuItem() => null;
}

