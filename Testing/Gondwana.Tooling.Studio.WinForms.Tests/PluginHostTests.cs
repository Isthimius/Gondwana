using System.Text.Json;
using Gondwana.Tooling.Studio.Core.Extensibility;
using CorePlugin = Gondwana.Tooling.Studio.Core.Extensibility.IStudioPlugin;
using Host = Gondwana.Tooling.Studio.WinForms.Extensibility.StudioPluginHost;
using WinPlugin = Gondwana.Tooling.Studio.WinForms.Extensibility.IStudioPlugin;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

/// <summary>
/// Contains regression tests for plugin host.
/// </summary>
public sealed class PluginHostTests
{
    /// <summary>
    /// Verifies discovery shares both contracts and isolates lifecycle failures.
    /// </summary>
    /// <param name="failOnClose">The fail on close value for this test case.</param>
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
        var legacy = Assert.Single(host.DiscoveryResults, result => result.Status == StudioPluginStatus.Loaded);
        Assert.Equal($"legacy:{GetType().Assembly.GetName().Name}:{typeof(ContractFixturePlugin).FullName}", legacy.Id);
        Assert.Null(legacy.Version);
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
        Assert.Contains(host.DiscoveryResults, result => result is { Status: StudioPluginStatus.Disabled, DisplayName: "Contract fixture" });
        Assert.Contains(messages, message => message.Contains("Disabled 'Contract fixture'"));
        Assert.Contains(messages, message => message.Contains("Failed loading corrupt.dll"));
        Assert.Contains(messages, message => message.Contains("Failed to instantiate") && message.Contains(nameof(ThrowingConstructorPlugin)));
    }

    [Fact]
    public void PackagesResolveIndependentPrivateVersionsAndWinOverLegacyCopy()
    {
        var root = NewPluginDirectory();
        var first = CreatePackage(root, "FirstPlugin", "fixture.first");
        CreatePackage(root, "SecondPlugin", "fixture.second");
        File.Copy(Path.Combine(first, "FirstPlugin.dll"), Path.Combine(root, "FirstPlugin.dll"));
        File.Copy(Path.Combine(first, "FirstPlugin.deps.json"), Path.Combine(root, "FirstPlugin.deps.json"));
        File.Copy(Path.Combine(first, "StudioPluginFixtures.PrivateDependency.dll"),
            Path.Combine(root, "StudioPluginFixtures.PrivateDependency.dll"));

        var host = new Host(_ => { });
        host.DiscoverAndLoad(root);
        var snapshot = host.DiscoveryResults;
        host.DiscoverAndLoad(root);

        Assert.Equal(new[] { "1.0.0.0", "2.0.0.0" }, host.Plugins.Select(plugin => plugin.Name));
        Assert.Equal(snapshot.ToArray(), host.DiscoveryResults.ToArray());
        Assert.Equal(2, snapshot.Count(result => result is
        {
            Source: StudioPluginSource.ManifestPackage,
            Compatibility: StudioPluginCompatibility.Compatible,
            Status: StudioPluginStatus.Loaded,
            Version: "1.0.0"
        }));
        Assert.Contains(snapshot, result => result is
        {
            Source: StudioPluginSource.LegacyAssembly,
            Status: StudioPluginStatus.Duplicate,
            Compatibility: StudioPluginCompatibility.Unknown,
            Version: null
        });
    }

    [Fact]
    public void DuplicatePackageIdsAreRejectedBeforeActivation()
    {
        var root = NewPluginDirectory();
        CreatePackage(root, "FirstPlugin", "fixture.duplicate");
        CreatePackage(root, "SecondPlugin", "fixture.duplicate");

        var host = new Host(_ => { });
        host.DiscoverAndLoad(root);

        Assert.Empty(host.Plugins);
        Assert.Equal(2, host.DiscoveryResults.Count);
        Assert.All(host.DiscoveryResults, result => Assert.Equal(StudioPluginStatus.Duplicate, result.Status));
    }

    [Theory]
    [InlineData("missing-manifest", StudioPluginStatus.InvalidPackage, "plugin.json")]
    [InlineData("corrupt", StudioPluginStatus.InvalidPackage, "JSON")]
    [InlineData("schema", StudioPluginStatus.InvalidPackage, "schemaVersion")]
    [InlineData("contract", StudioPluginStatus.Incompatible, "contract")]
    [InlineData("version", StudioPluginStatus.InvalidPackage, "SemVer")]
    [InlineData("entry", StudioPluginStatus.InvalidPackage, "Entry assembly")]
    [InlineData("traversal", StudioPluginStatus.InvalidPackage, "filename")]
    [InlineData("absolute", StudioPluginStatus.InvalidPackage, "filename")]
    [InlineData("dependency", StudioPluginStatus.FailedToLoad, "PrivateDependency")]
    [InlineData("contract-copy", StudioPluginStatus.InvalidPackage, "belongs to the host")]
    public void BrokenPackageDoesNotPreventLoadingAnother(string problem, StudioPluginStatus expectedStatus, string diagnostic)
    {
        var root = NewPluginDirectory();
        var broken = CreatePackage(root, "FirstPlugin", "fixture.broken");
        CreatePackage(root, "SecondPlugin", "fixture.working");
        var manifest = Path.Combine(broken, "plugin.json");

        switch (problem)
        {
            case "missing-manifest": File.Delete(manifest); break;
            case "corrupt": File.WriteAllText(manifest, "{"); break;
            case "schema": WriteManifest(broken, "fixture.broken", "FirstPlugin.dll", schema: 2); break;
            case "contract": WriteManifest(broken, "fixture.broken", "FirstPlugin.dll", contract: 2); break;
            case "version": WriteManifest(broken, "fixture.broken", "FirstPlugin.dll", version: "1.02.3"); break;
            case "entry": File.Delete(Path.Combine(broken, "FirstPlugin.dll")); break;
            case "traversal": WriteManifest(broken, "fixture.broken", @"..\SecondPlugin.dll"); break;
            case "absolute": WriteManifest(broken, "fixture.broken", Path.Combine(root, "SecondPlugin.dll")); break;
            case "dependency": File.Delete(Path.Combine(broken, "StudioPluginFixtures.PrivateDependency.dll")); break;
            case "contract-copy": File.Copy(typeof(CorePlugin).Assembly.Location, Path.Combine(broken, "renamed.dll")); break;
        }

        var host = new Host(_ => { });
        host.DiscoverAndLoad(root);

        Assert.Equal("2.0.0.0", Assert.Single(host.Plugins).Name);
        var result = Assert.Single(host.DiscoveryResults, entry => entry.SourceDirectory == broken);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Contains(diagnostic, result.Diagnostic!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PackageRejectsDirectoryJunctionOutsideItsBoundary()
    {
        var root = NewPluginDirectory();
        var package = CreatePackage(root, "FirstPlugin", "fixture.link");
        var outside = NewPluginDirectory();
        var link = Path.Combine(package, "outside");
        using var command = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c mklink /J \"{link}\" \"{outside}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        })!;
        command.WaitForExit();
        Assert.Equal(0, command.ExitCode);

        var host = new Host(_ => { });
        host.DiscoverAndLoad(root);

        Assert.Empty(host.Plugins);
        var result = Assert.Single(host.DiscoveryResults);
        Assert.Equal(StudioPluginStatus.InvalidPackage, result.Status);
        Assert.Contains("reparse point", result.Diagnostic!, StringComparison.OrdinalIgnoreCase);
    }

    private static string NewPluginDirectory() => Directory.CreateDirectory(
        Path.Combine(AppContext.BaseDirectory, "plugin-fixtures", Guid.NewGuid().ToString("N"))).FullName;

    private static string CreatePackage(string root, string plugin, string id)
    {
        var package = Directory.CreateDirectory(Path.Combine(root, plugin)).FullName;
        var source = Path.Combine(AppContext.BaseDirectory, "plugin-test-assets", plugin);
        foreach (var path in Directory.GetFiles(source))
            File.Copy(path, Path.Combine(package, Path.GetFileName(path)));

        WriteManifest(package, id, plugin + ".dll");
        return package;
    }

    private static void WriteManifest(string package, string id, string entry, int schema = 1, int contract = 1,
        string version = "1.0.0")
    {
        var manifest = JsonSerializer.Serialize(new
        {
            schemaVersion = schema,
            id,
            name = id,
            version,
            entryAssembly = entry,
            contractVersion = contract
        });
        File.WriteAllText(Path.Combine(package, "plugin.json"), manifest);
    }

    /// <summary>
    /// Verifies absent plugins are optional.
    /// </summary>
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

/// <summary>
/// Represents contract fixture plugin.
/// </summary>
public sealed class ContractFixturePlugin : WinPlugin
{
    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name => "Contract fixture";
    /// <summary>
    /// Gets the project path.
    /// </summary>
    public string? ProjectPath { get; private set; }
    /// <summary>
    /// Updates the plugin for the newly opened project.
    /// </summary>
    /// <param name="projectPath">The project path value for this test case.</param>
    public void OnProjectOpened(string projectPath)
    {
        if (projectPath == "throw") throw new InvalidOperationException("Expected fixture failure");
        ProjectPath = projectPath;
    }
    /// <summary>
    /// Clears plugin state associated with the closed project.
    /// </summary>
    public void OnProjectClosed()
    {
        if (ProjectPath == "throw-on-close") throw new InvalidOperationException("Expected close failure");
        ProjectPath = null;
    }
    /// <summary>
    /// Creates the plugin's dockable user interface.
    /// </summary>
    /// <returns>The resulting control, or <see langword="null"/> when unavailable.</returns>
    public Control? CreatePanel() => ProjectPath == "throw-contribution" ? throw new InvalidOperationException("Expected panel failure") : null;
    /// <summary>
    /// Creates the menu item that opens the plugin panel.
    /// </summary>
    /// <returns>The resulting tool strip menu item, or <see langword="null"/> when unavailable.</returns>
    public ToolStripMenuItem? CreateMenuItem() => ProjectPath == "throw-contribution" ? throw new InvalidOperationException("Expected menu failure") : null;
}

/// <summary>
/// Represents throwing constructor plugin.
/// </summary>
public sealed class ThrowingConstructorPlugin : CorePlugin
{
    /// <summary>
    /// Initializes a new instance of the <c>ThrowingConstructorPlugin</c> class.
    /// </summary>
    public ThrowingConstructorPlugin() => throw new InvalidOperationException("Expected constructor failure");
    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name => "Constructor fixture";
    /// <summary>
    /// Updates the plugin for the newly opened project.
    /// </summary>
    /// <param name="projectPath">The project path value for this test case.</param>
    public void OnProjectOpened(string projectPath) { }
    /// <summary>
    /// Clears plugin state associated with the closed project.
    /// </summary>
    public void OnProjectClosed() { }
}
