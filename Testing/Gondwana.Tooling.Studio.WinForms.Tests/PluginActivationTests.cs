using System.Text.Json;
using Gondwana.Tooling.Studio.Core.Extensibility;
using Host = Gondwana.Tooling.Studio.Core.Extensibility.StudioPluginHost;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class PluginPackageTests
{
    [Fact]
    public void DisabledPackageHasMetadataButNoConstructorsOrLifecycle()
    {
        var marker = Path.Combine(_root, "constructed.txt");
        var entry = Package("one", "sample.one", source: PluginSource("One", constructor: $"System.IO.File.WriteAllText({JsonSerializer.Serialize(marker)}, \"constructed\");"));
        var manifestPath = Path.Combine(Path.GetDirectoryName(entry)!, "plugin.json");
        var manifest = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(manifestPath))!;
        manifest["description"] = "Fixture description";
        manifest["author"] = "Fixture author";
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
        var preferences = new PluginPreferences(Path.Combine(_root, "preferences.json"), _ => { });
        Assert.True(preferences.TrySetEnabled("sample.one", false, out _));
        preferences = new PluginPreferences(preferences.FilePath, _ => { });
        var host = new Host(_ => { }, preferences.WasEnabledAtStartup);
        host.DiscoverAndLoad(_root);
        host.NotifyProjectOpened("throw");
        host.NotifyProjectClosed();
        Assert.Empty(host.Plugins);
        Assert.False(File.Exists(marker));
        var row = Assert.Single(host.Discovery);
        Assert.Equal(PluginLoadStatus.UserDisabled, row.Status);
        Assert.False(row.EnabledAtStartup);
        Assert.Equal("Fixture description", row.Description);
        Assert.Equal("Fixture author", row.Author);
        Assert.Equal("sample.one", row.PreferenceId);
    }

    [Fact]
    public void ChangingPreferencesDoesNotChangeRuntimeUntilNewHost()
    {
        Package("one", "sample.one");
        var path = Path.Combine(_root, "preferences.json");
        var preferences = new PluginPreferences(path, _ => { });
        var host = new Host(_ => { }, preferences.WasEnabledAtStartup);
        host.DiscoverAndLoad(_root);
        var instance = Assert.Single(host.Plugins);
        preferences.TrySetEnabled("sample.one", false, out _);
        host.DiscoverAndLoad(_root);
        Assert.Same(instance, Assert.Single(host.Plugins));
        Assert.Equal(PluginLoadStatus.Loaded, Assert.Single(host.Discovery).Status);
        var nextPreferences = new PluginPreferences(path, _ => { });
        var next = new Host(_ => { }, nextPreferences.WasEnabledAtStartup);
        next.DiscoverAndLoad(_root);
        Assert.Empty(next.Plugins);
        nextPreferences.TrySetEnabled("sample.one", true, out _);
        Assert.Empty(next.Plugins);
        var finalPreferences = new PluginPreferences(path, _ => { });
        var final = new Host(_ => { }, finalPreferences.WasEnabledAtStartup);
        final.DiscoverAndLoad(_root);
        Assert.Single(final.Plugins);
    }

    [Fact]
    public void DisabledPackageReservesImplementationsAgainstLegacyCopies()
    {
        var entry = Package("one", "sample.one", source: PluginSource("One", constructor: "throw new System.Exception(\"must not construct\");"));
        File.Copy(entry, Path.Combine(_root, Path.GetFileName(entry)));
        var host = new Host(_ => { }, id => id != "sample.one");
        host.DiscoverAndLoad(_root);
        Assert.Empty(host.Plugins);
        Assert.Equal(2, host.Discovery.Count);
        Assert.All(host.Discovery, row => Assert.Equal("sample.one", row.PreferenceId));
        Assert.Contains(host.Discovery, row => row.Status == PluginLoadStatus.UserDisabled);
        Assert.Contains(host.Discovery, row => row.Status == PluginLoadStatus.Conflict);
        Assert.DoesNotContain(host.Discovery, row => row.Status == PluginLoadStatus.FailedToLoad);
    }

    [Fact]
    public void PackageWithMultipleImplementationsHasOnePreference()
    {
        Package("many", "sample.many", source: PluginSource("First") + PluginSource("Second"));
        var host = new Host(_ => { }, _ => false);
        host.DiscoverAndLoad(_root);
        Assert.Equal(2, host.Discovery.Count);
        Assert.All(host.Discovery, row =>
        {
            Assert.Equal("sample.many", row.PreferenceId);
            Assert.Equal(PluginLoadStatus.UserDisabled, row.Status);
        });
        Assert.Empty(host.Plugins);
    }

    [Fact]
    public void LegacyPreferenceSurvivesMovingInstallation()
    {
        var first = Path.Combine(_root, "first");
        var second = Path.Combine(_root, "second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        Compile(Path.Combine(first, "Legacy.dll"), PluginSource("Legacy"));
        var host = new Host(_ => { });
        host.DiscoverAndLoad(first);
        var id = Assert.Single(host.Discovery).PreferenceId!;
        var preferences = new PluginPreferences(Path.Combine(_root, "preferences.json"), _ => { });
        preferences.TrySetEnabled(id, false, out _);
        File.Copy(Path.Combine(first, "Legacy.dll"), Path.Combine(second, "Renamed.dll"));
        var reloaded = new PluginPreferences(preferences.FilePath, _ => { });
        var next = new Host(_ => { }, reloaded.WasEnabledAtStartup);
        next.DiscoverAndLoad(second);
        var row = Assert.Single(next.Discovery);
        Assert.Equal(id, row.PreferenceId);
        Assert.Equal(PluginLoadStatus.UserDisabled, row.Status);
        Assert.Null(row.DisplayName); // No call into Name before construction.
        Assert.Empty(next.Plugins);
    }

    [Fact]
    public void PreferencesNeverOverrideCompatibilityOrConflicts()
    {
        var entry = Package("incompatible", "sample.incompatible");
        var manifestPath = Path.Combine(Path.GetDirectoryName(entry)!, "plugin.json");
        File.WriteAllText(manifestPath, File.ReadAllText(manifestPath).Replace("\"contractVersion\":1", "\"contractVersion\":2", StringComparison.Ordinal));
        Package("a", "sample.duplicate");
        Package("b", "sample.duplicate");
        var host = new Host(_ => { }, _ => true);
        host.DiscoverAndLoad(_root);
        Assert.Empty(host.Plugins);
        Assert.Contains(host.Discovery, row => row.Status == PluginLoadStatus.Incompatible);
        Assert.Equal(2, host.Discovery.Count(row => row.Status == PluginLoadStatus.Conflict));
    }
}
