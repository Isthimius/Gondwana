using System.Drawing;
using Gondwana.Tooling.Studio.Tilesheets.WinForms.Editing;

namespace Gondwana.Tests;

/// <summary>
/// Contains regression tests for overlay settings.
/// </summary>
public sealed class OverlaySettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OverlaySettings_" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_directory, "tool.settings.json");
    /// <summary>
    /// Initializes a new instance of the <c>OverlaySettingsTests</c> class.
    /// </summary>
    public OverlaySettingsTests() => Directory.CreateDirectory(_directory);
    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_directory, true);

    /// <summary>
    /// Verifies every color round trips through settings file.
    /// </summary>
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

    /// <summary>
    /// Verifies invalid file uses defaults and reports warning.
    /// </summary>
    /// <param name="json">The json value for this test case.</param>
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

    /// <summary>
    /// Verifies invalid color does not discard other valid colors.
    /// </summary>
    [Fact]
    public void InvalidColor_DoesNotDiscardOtherValidColors()
    {
        File.WriteAllText(SettingsPath, """{"OverlayColors":{"Frames":"#123456","Collision":"wrong"}}""");
        var settings = OverlaySettings.Load(SettingsPath);
        Assert.Equal(Color.FromArgb(0x12, 0x34, 0x56).ToArgb(), settings[OverlayKind.Frames].ToArgb());
        Assert.Equal(Color.Tomato.ToArgb(), settings[OverlayKind.Collision].ToArgb());
        Assert.NotNull(settings.Warning);
    }

    /// <summary>Legacy standalone colors load into the new preference scope without modifying the old file.</summary>
    [Fact]
    public void LegacyFilename_LoadsAndSavesToCurrentFilename()
    {
        var legacyPath = Path.Combine(_directory, "Gondwana.Tooling.Tilesheets.WinForms.settings.json");
        var currentPath = Path.Combine(_directory, "Gondwana.Tooling.Studio.Tilesheets.WinForms.settings.json");
        File.WriteAllText(legacyPath, """{"OverlayColors":{"Frames":"#123456"}}""");

        var settings = OverlaySettings.LoadWithLegacyFallback(currentPath);
        Assert.Equal(currentPath, settings.FilePath);
        Assert.Equal(Color.FromArgb(0x12, 0x34, 0x56).ToArgb(), settings[OverlayKind.Frames].ToArgb());
        Assert.False(File.Exists(currentPath));

        settings.SetColor(OverlayKind.Collision, Color.Red);
        Assert.Equal(Color.Red.ToArgb(), OverlaySettings.LoadWithLegacyFallback(currentPath)[OverlayKind.Collision].ToArgb());
        Assert.Equal(Color.FromArgb(0x12, 0x34, 0x56).ToArgb(), OverlaySettings.Load(currentPath)[OverlayKind.Frames].ToArgb());
        Assert.Equal("""{"OverlayColors":{"Frames":"#123456"}}""", File.ReadAllText(legacyPath));
    }

    /// <summary>Current settings always win over an older file.</summary>
    [Fact]
    public void CurrentFilename_WinsOverLegacyFilename()
    {
        File.WriteAllText(Path.Combine(_directory, "Gondwana.Tooling.Tilesheets.WinForms.settings.json"),
            """{"OverlayColors":{"Frames":"#123456"}}""");
        var currentPath = Path.Combine(_directory, "Gondwana.Tooling.Studio.Tilesheets.WinForms.settings.json");
        File.WriteAllText(currentPath, """{"OverlayColors":{"Frames":"#ABCDEF"}}""");

        var settings = OverlaySettings.LoadWithLegacyFallback(currentPath);
        Assert.Equal(Color.FromArgb(0xAB, 0xCD, 0xEF).ToArgb(), settings[OverlayKind.Frames].ToArgb());
    }

    /// <summary>
    /// Verifies failed save does not change current colors or notify documents.
    /// </summary>
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
