using Gondwana.Tooling.Studio.Core.Extensibility;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed class PluginPreferenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "GondwanaPluginPreferences-" + Guid.NewGuid());
    private string FilePath => Path.Combine(_root, "plugin-preferences.json");

    [Fact]
    public void MissingSettingsDefaultEnabledAndSaveSurvivesNewInstances()
    {
        var preferences = new PluginPreferences(FilePath, _ => { });
        Assert.True(preferences.IsEnabled("sample.plugin"));
        Assert.False(preferences.RestartRequired);
        Assert.True(preferences.TrySetEnabled("sample.plugin", false, out var error));
        Assert.Null(error);
        Assert.False(preferences.IsEnabled("sample.plugin"));
        Assert.True(preferences.WasEnabledAtStartup("sample.plugin"));
        Assert.True(preferences.RestartRequired);
        var next = new PluginPreferences(FilePath, _ => { });
        Assert.False(next.IsEnabled("sample.plugin"));
        Assert.False(next.WasEnabledAtStartup("sample.plugin"));
        Assert.False(next.RestartRequired);
        Assert.True(next.TrySetEnabled("sample.plugin", true, out _));
        Assert.True(next.RestartRequired);
        Assert.True(new PluginPreferences(FilePath, _ => { }).IsEnabled("sample.plugin"));
    }

    [Fact]
    public void RevertingAllChangesClearsRestartAndPreservesMissingPluginChoices()
    {
        var preferences = new PluginPreferences(FilePath, _ => { });
        preferences.TrySetEnabled("temporarily.missing", false, out _);
        preferences = new PluginPreferences(FilePath, _ => { });
        preferences.TrySetEnabled("sample.one", false, out _);
        preferences.TrySetEnabled("sample.two", false, out _);
        preferences.TrySetEnabled("sample.one", true, out _);
        Assert.True(preferences.RestartRequired);
        preferences.TrySetEnabled("sample.two", true, out _);
        Assert.False(preferences.RestartRequired);
        var next = new PluginPreferences(FilePath, _ => { });
        Assert.False(next.IsEnabled("temporarily.missing"));
        Assert.True(next.IsEnabled("sample.one"));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":2,\"disabledPluginIds\":[\"missing\"]}")]
    [InlineData("{\"schemaVersion\":1,\"disabledPluginIds\":[null]}")]
    public void InvalidSettingsAreReportedAndNotOverwritten(string contents)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(FilePath, contents);
        var messages = new List<string>();
        var preferences = new PluginPreferences(FilePath, messages.Add);
        Assert.True(preferences.IsEnabled("sample.plugin"));
        Assert.NotNull(preferences.ReadError);
        Assert.False(preferences.TrySetEnabled("sample.plugin", false, out var error));
        Assert.NotNull(error);
        Assert.Equal(contents, File.ReadAllText(FilePath));
        Assert.False(preferences.RestartRequired);
        Assert.NotEmpty(messages);
    }

    [Fact]
    public void UnreadableSettingsDoNotCrash()
    {
        Directory.CreateDirectory(FilePath); // Opening a directory as a file deterministically fails.
        var preferences = new PluginPreferences(FilePath, _ => { });
        Assert.NotNull(preferences.ReadError);
        Assert.True(preferences.IsEnabled("sample.plugin"));
    }

    [Fact]
    public void FailedWriteLeavesPriorChoiceAndFileIntactAndCanBeRetried()
    {
        var preferences = new PluginPreferences(FilePath, _ => { });
        preferences.TrySetEnabled("missing.plugin", false, out _);
        var saved = File.ReadAllText(FilePath);
        File.Delete(FilePath);
        Directory.CreateDirectory(FilePath);
        Assert.False(preferences.TrySetEnabled("sample.plugin", false, out var error));
        Assert.Contains("Could not save", error);
        Assert.True(preferences.IsEnabled("sample.plugin"));
        Assert.False(preferences.IsEnabled("missing.plugin"));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp"));
        Directory.Delete(FilePath);
        File.WriteAllText(FilePath, saved);
        Assert.True(preferences.TrySetEnabled("sample.plugin", false, out _));
        var next = new PluginPreferences(FilePath, _ => { });
        Assert.False(next.IsEnabled("sample.plugin"));
        Assert.False(next.IsEnabled("missing.plugin"));
    }

    [Fact]
    public void StaleInstanceCannotOverwriteAnotherInstancesSavedChoice()
    {
        var first = new PluginPreferences(FilePath, _ => { });
        var second = new PluginPreferences(FilePath, _ => { });
        Assert.True(first.TrySetEnabled("sample.first", false, out _));
        Assert.False(second.TrySetEnabled("sample.second", false, out var error));
        Assert.Contains("changed in another Studio instance", error);
        Assert.False(second.RestartRequired);
        var latest = new PluginPreferences(FilePath, _ => { });
        Assert.False(latest.IsEnabled("sample.first"));
        Assert.True(latest.IsEnabled("sample.second"));
        Assert.True(latest.TrySetEnabled("sample.second", false, out _));
        var saved = new PluginPreferences(FilePath, _ => { });
        Assert.False(saved.IsEnabled("sample.first"));
        Assert.False(saved.IsEnabled("sample.second"));
    }

    [Fact]
    public void ConcurrentWriterLockRejectsSaveWithoutLosingChoicesAndAllowsRetry()
    {
        var preferences = new PluginPreferences(FilePath, _ => { });
        Directory.CreateDirectory(_root);
        using (var saveLock = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(preferences.TrySetEnabled("sample.plugin", false, out var error));
            Assert.NotNull(error);
            Assert.True(preferences.IsEnabled("sample.plugin"));
            Assert.False(preferences.RestartRequired);
            Assert.False(File.Exists(FilePath));
        }
        Assert.True(preferences.TrySetEnabled("sample.plugin", false, out _));
        Assert.False(new PluginPreferences(FilePath, _ => { }).IsEnabled("sample.plugin"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
