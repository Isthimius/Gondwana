using System.Drawing;
using Gondwana.Tooling.Tilesheets.Editing;

namespace Gondwana.Tests;

public sealed class OverlaySettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OverlaySettings_" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_directory, "tool.settings.json");
    public OverlaySettingsTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void EveryColor_RoundTripsThroughSettingsFile()
    {
        var settings = OverlaySettings.Load(SettingsPath);
        int notifications = 0;
        settings.Changed += (_, _) => notifications++;
        foreach (var kind in Enum.GetValues<OverlayKind>())
            settings.SetColor(kind, Color.FromArgb(10 + (int)kind, 33, 244));

        var loaded = OverlaySettings.Load(SettingsPath);
        foreach (var kind in Enum.GetValues<OverlayKind>())
            Assert.Equal(settings[kind].ToArgb(), loaded[kind].ToArgb());
        Assert.Equal(Enum.GetValues<OverlayKind>().Length, notifications);
        Assert.Null(loaded.Warning);
        Assert.Contains("\"Collision\": \"#", File.ReadAllText(SettingsPath));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("null")]
    [InlineData("{}")]
    public void InvalidFile_UsesDefaultsAndReportsWarning(string json)
    {
        File.WriteAllText(SettingsPath, json);
        var settings = OverlaySettings.Load(SettingsPath);
        Assert.Equal(Color.Tomato.ToArgb(), settings[OverlayKind.Collision].ToArgb());
        Assert.NotNull(settings.Warning);
        Assert.Equal(json, File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void InvalidColor_DoesNotDiscardOtherValidColors()
    {
        File.WriteAllText(SettingsPath, """{"OverlayColors":{"Frames":"#123456","Collision":"wrong"}}""");
        var settings = OverlaySettings.Load(SettingsPath);
        Assert.Equal(Color.FromArgb(0x12, 0x34, 0x56).ToArgb(), settings[OverlayKind.Frames].ToArgb());
        Assert.Equal(Color.Tomato.ToArgb(), settings[OverlayKind.Collision].ToArgb());
        Assert.NotNull(settings.Warning);
    }

    [Fact]
    public void FailedSave_DoesNotChangeCurrentColorsOrNotifyDocuments()
    {
        var settings = OverlaySettings.Load(Path.Combine(_directory, "missing", "settings.json"));
        bool notified = false;
        settings.Changed += (_, _) => notified = true;
        Assert.Throws<DirectoryNotFoundException>(() => settings.SetColor(OverlayKind.Frames, Color.Red));
        Assert.Equal(Color.SeaGreen.ToArgb(), settings[OverlayKind.Frames].ToArgb());
        Assert.False(notified);
    }
}
