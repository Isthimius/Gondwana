using System.Text.Json;

namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>Per-user requested activation, separate from the immutable choices used at this startup.</summary>
public sealed class PluginPreferences
{
    private readonly Action<string> _log;
    private readonly HashSet<string> _startupDisabled;
    private HashSet<string> _disabled;
    private string? _readError;

    public string FilePath { get; }
    public bool RestartRequired => !_disabled.SetEquals(_startupDisabled);
    public string? ReadError => _readError;

    /// <summary>Loads a versioned preference file. Missing/invalid settings default to enabled and log diagnostics.</summary>
    public PluginPreferences(string filePath, Action<string> log)
    {
        FilePath = filePath;
        _log = log;
        _disabled = Read();
        _startupDisabled = new(_disabled, StringComparer.Ordinal);
    }

    public bool IsEnabled(string id) => !_disabled.Contains(id);
    public bool WasEnabledAtStartup(string id) => !_startupDisabled.Contains(id);
    public bool IsPending(string id) => IsEnabled(id) != WasEnabledAtStartup(id);

    /// <summary>Saves atomically before changing requested state. Failure leaves both the file and in-memory choice unchanged.</summary>
    public bool TrySetEnabled(string id, bool enabled, out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        error = null;
        // Never silently overwrite an unreadable or newer settings file; it may contain
        // preferences for absent plugins. A repaired file can be loaded after restarting.
        if (_readError is not null)
        {
            error = $"Preferences could not be read. Repair or remove '{FilePath}', then restart Studio. {_readError}";
            _log($"Error: [Plugin preferences] {error}");
            return false;
        }
        if (IsEnabled(id) == enabled) return true;
        var next = new HashSet<string>(_disabled, StringComparer.Ordinal);
        if (enabled) next.Remove(id); else next.Add(id);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
            // Keep the lock file in place: deleting it could let another process lock
            // a different file while a waiting writer still holds the original handle.
            using var saveLock = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (!ReadSettings().SetEquals(_disabled))
                throw new IOException("Plugin preferences changed in another Studio instance or outside Studio. Restart Studio to load the latest choices before editing them.");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new Settings
            {
                SchemaVersion = 1,
                DisabledPluginIds = next.Order(StringComparer.Ordinal).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true });
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, FilePath, overwrite: true);
            _disabled = next;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Could not save plugin preferences to '{FilePath}'. Close other Studio instances, then retry; check folder permissions and available disk space. {ex.Message}";
            _log($"Error: [Plugin preferences] {error}");
            return false;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) { _log($"Warning: [Plugin preferences] Could not remove temporary settings file: {ex.Message}"); }
        }
    }

    private HashSet<string> Read()
    {
        try
        {
            return ReadSettings();
        }
        catch (Exception ex)
        {
            _readError = ex.Message;
            _log($"Warning: [Plugin preferences] Could not read '{FilePath}'; plugins default to enabled. {ex.Message}");
            return new(StringComparer.Ordinal);
        }
    }

    private HashSet<string> ReadSettings()
    {
        try
        {
            using var stream = File.OpenRead(FilePath);
            var settings = JsonSerializer.Deserialize<Settings>(stream);
            if (settings?.SchemaVersion != 1 || settings.DisabledPluginIds is null || settings.DisabledPluginIds.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("Expected schema version 1 and a disabledPluginIds array of nonblank IDs.");
            return new(settings.DisabledPluginIds, StringComparer.Ordinal);
        }
        catch (FileNotFoundException) { return new(StringComparer.Ordinal); }
        catch (DirectoryNotFoundException) { return new(StringComparer.Ordinal); }
    }

    private sealed class Settings
    {
        [System.Text.Json.Serialization.JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("disabledPluginIds")]
        public string[]? DisabledPluginIds { get; set; }
    }
}
