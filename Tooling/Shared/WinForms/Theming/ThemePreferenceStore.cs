using System.Diagnostics;
using System.Text.Json;

namespace Gondwana.Tooling.WinForms.Theming;

/// <summary>Atomic, per-user preference shared by every tooling executable, independent of layouts and sessions.</summary>
public sealed class ThemePreferenceStore
{
    private readonly Action<string> _report;
    public string FilePath { get; }

    public ThemePreferenceStore(string? settingsRoot = null, Action<string>? report = null)
    {
        settingsRoot ??= AppContext.GetData("Gondwana.Tooling.SettingsRoot") as string
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Hidden Worlds Games", "Gondwana", "Tooling");
        FilePath = Path.Combine(settingsRoot, "theme.json");
        _report = report ?? (message => Trace.TraceWarning(message));
    }

    public ToolingTheme Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return ToolingTheme.Dark;
            if (new FileInfo(FilePath).Length > 4096) throw new InvalidDataException("Theme preference is too large.");
            using var json = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (json.RootElement.TryGetProperty("Theme", out var value) && value.ValueKind == JsonValueKind.String &&
                Enum.TryParse<ToolingTheme>(value.GetString(), out var theme) && Enum.IsDefined(theme)) return theme;
            throw new InvalidDataException("Unknown theme preference.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or InvalidDataException)
        {
            _report($"Theme preference ignored: {ex.Message}");
            return ToolingTheme.Dark;
        }
    }

    public bool Write(ToolingTheme theme)
    {
        if (!Enum.IsDefined(theme)) return false;
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { Theme = theme.ToString() }));
            File.Move(temporary, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _report($"Theme preference could not be saved: {ex.Message}");
            return false;
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _report(ex.Message); }
        }
    }
}
