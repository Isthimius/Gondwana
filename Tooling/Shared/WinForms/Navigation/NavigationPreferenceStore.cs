using System.Diagnostics;

namespace Gondwana.Tooling.WinForms;

/// <summary>One atomic per-user navigation preference for Studio and all standalone editors.</summary>
public sealed class NavigationPreferenceStore
{
    private readonly Action<string> _report;
    private readonly string _legacyPath;
    /// <summary>Gets the preference path, independent of the entry application and its layout.</summary>
    public string FilePath { get; }

    /// <summary>Creates a store, honoring the same settings-root override as tooling layouts and themes.</summary>
    /// <param name="settingsRoot">Optional isolated preference root.</param>
    /// <param name="report">Receives recoverable read, migration, and write failures.</param>
    public NavigationPreferenceStore(string? settingsRoot = null, Action<string>? report = null)
    {
        settingsRoot ??= AppContext.GetData("Gondwana.Tooling.SettingsRoot") as string
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Hidden Worlds Games", "Gondwana", "Tooling");
        FilePath = Path.GetFullPath(Path.Combine(settingsRoot, "navigation-icon-size.txt"));
        string legacyApplication = AppContext.GetData("Gondwana.Tooling.ApplicationId") as string
            ?? "Gondwana.Studio.WinForms"; // Studio's assembly name, not its project name.
        _legacyPath = Path.Combine(settingsRoot, legacyApplication, "Studio", "directory-icon-size.txt");
        _report = report ?? (message => Trace.TraceWarning(message));
    }

    /// <summary>Reads an explicit size, migrating Studio's old preference if needed; null preserves each control's defaults.</summary>
    public NavigationIconSize? Read()
    {
        try
        {
            // An invalid shared preference must not resurrect an older Studio choice.
            if (File.Exists(FilePath)) return ReadFile(FilePath);
            if (!File.Exists(_legacyPath)) return null;
            var size = ReadFile(_legacyPath);
            return size is { } value ? Migrate(value) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _report($"Navigation size preference ignored: {ex.Message}");
            return null;
        }
    }

    // Keep migration publication separate so another tool's explicit save can win the race.
    internal NavigationIconSize? Migrate(NavigationIconSize size)
    {
        if (Write(size, overwrite: false)) return size;
        return File.Exists(FilePath) ? ReadFile(FilePath) : size;
    }

    private static NavigationIconSize? ReadFile(string path)
    {
        if (new FileInfo(path).Length > 4096) return null;
        return File.ReadAllText(path).Trim() switch
        {
            nameof(NavigationIconSize.Small) => NavigationIconSize.Small,
            nameof(NavigationIconSize.Medium) => NavigationIconSize.Medium,
            nameof(NavigationIconSize.Large) => NavigationIconSize.Large,
            _ => null
        };
    }

    /// <summary>Atomically saves a defined size; failures leave the previous preference intact.</summary>
    /// <param name="size">Size to save.</param>
    /// <returns>True if the preference was saved.</returns>
    public bool Write(NavigationIconSize size) => Write(size, overwrite: true);

    private bool Write(NavigationIconSize size, bool overwrite)
    {
        if (!Enum.IsDefined(size)) throw new ArgumentOutOfRangeException(nameof(size));
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(temporary, size.ToString());
            File.Move(temporary, FilePath, overwrite);
            return true;
        }
        catch (IOException) when (!overwrite && File.Exists(FilePath))
        {
            return false; // Expected when a concurrent migration or explicit save publishes first.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _report($"Navigation size preference could not be saved: {ex.Message}");
            return false;
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _report(ex.Message); }
        }
    }
}
