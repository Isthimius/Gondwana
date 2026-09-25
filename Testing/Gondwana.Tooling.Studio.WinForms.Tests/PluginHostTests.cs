using CorePlugin = Gondwana.Tooling.Studio.Core.Extensibility.IStudioPlugin;
using Host = Gondwana.Tooling.Studio.WinForms.Extensibility.StudioPluginHost;
using WinPlugin = Gondwana.Tooling.Studio.WinForms.Extensibility.IStudioPlugin;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed class PluginHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiscoverySharesBothContractsAndIsolatesLifecycleFailures(bool failOnClose)
    {
        // Keep mapped DLLs in build output; Windows releases them when the test process exits.
        var directory = Path.Combine(AppContext.BaseDirectory, "plugin-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        foreach (var assembly in new[] { GetType().Assembly, typeof(CorePlugin).Assembly, typeof(WinPlugin).Assembly })
            File.Copy(assembly.Location, Path.Combine(directory, Path.GetFileName(assembly.Location)));
        var dependencies = Path.ChangeExtension(GetType().Assembly.Location, ".deps.json");
        File.Copy(dependencies, Path.Combine(directory, Path.GetFileName(dependencies)));
        File.WriteAllText(Path.Combine(directory, "corrupt.dll"), "not a managed assembly");
        var messages = new List<string>();
        var host = new Host(messages.Add);
        host.DiscoverAndLoad(directory);
        var plugin = Assert.Single(host.Plugins);
        Assert.IsAssignableFrom<WinPlugin>(plugin);
        Assert.NotSame(System.Runtime.Loader.AssemblyLoadContext.Default,
            System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly));
        Assert.Equal("Contract fixture", plugin.Name);
        host.NotifyProjectOpened(directory);
        Assert.Equal(directory, plugin.GetType().GetProperty("ProjectPath")!.GetValue(plugin));
        host.NotifyProjectClosed();
        Assert.Null(plugin.GetType().GetProperty("ProjectPath")!.GetValue(plugin));
        host.NotifyProjectOpened("throw-contribution");
        Assert.Empty(host.GetPluginPanels());
        Assert.Empty(host.GetPluginMenuItems());
        Assert.Contains(messages, message => message.Contains("CreatePanel threw"));
        Assert.Contains(messages, message => message.Contains("CreateMenuItem threw"));
        host.NotifyProjectOpened(failOnClose ? "throw-on-close" : "throw");
        if (failOnClose) host.NotifyProjectClosed();
        Assert.Empty(host.Plugins);
        Assert.Contains(messages, message => message.Contains("Disabled 'Contract fixture'"));
        Assert.Contains(messages, message => message.Contains("Failed loading corrupt.dll"));
        Assert.Contains(messages, message => message.Contains("Failed to instantiate") && message.Contains(nameof(ThrowingConstructorPlugin)));
    }

    [Fact]
    public void AbsentPluginsAreOptional()
    {
        var host = new Host(_ => { });
        host.DiscoverAndLoad(Path.Combine(AppContext.BaseDirectory, "plugin-fixtures", Guid.NewGuid().ToString("N")));
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
    public void OnProjectClosed()
    {
        if (ProjectPath == "throw-on-close") throw new InvalidOperationException("Expected close failure");
        ProjectPath = null;
    }
    public Control? CreatePanel() => ProjectPath == "throw-contribution" ? throw new InvalidOperationException("Expected panel failure") : null;
    public ToolStripMenuItem? CreateMenuItem() => ProjectPath == "throw-contribution" ? throw new InvalidOperationException("Expected menu failure") : null;
}

public sealed class ThrowingConstructorPlugin : CorePlugin
{
    public ThrowingConstructorPlugin() => throw new InvalidOperationException("Expected constructor failure");
    public string Name => "Constructor fixture";
    public void OnProjectOpened(string projectPath) { }
    public void OnProjectClosed() { }
}
