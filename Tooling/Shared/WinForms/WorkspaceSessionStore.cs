using System.Text.Json;

namespace Gondwana.Tooling.WinForms;

/// <summary>Only saved document identities belong here, never editor models or content.</summary>
internal sealed record WorkspaceSessionDocument(string Path, string Format)
{
    internal static WorkspaceSessionDocument? FromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string? format = System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".gaf" or ".zip" => "gaf",
            ".gts" => "gts",
            ".gani" => "gani",
            ".gsnd" => "gsnd",
            ".gscn" => "gscn",
            ".gspr" => "gspr",
            _ => null
        };
        return format is null ? null : new(System.IO.Path.GetFullPath(path), format);
    }
}

internal sealed record WorkspaceSession
{
    public int Version { get; init; } = 1;
    public bool RestoreOnStartup { get; init; } = true;
    public bool StartEmptyOnce { get; init; }
    public string? WorkingDirectory { get; init; }
    public WorkspaceSessionDocument[] Documents { get; init; } = [];
    public string? ActiveDocument { get; init; }
}

/// <summary>Recoverable, atomic per-application preferences independent of docking and content files.</summary>
internal sealed class WorkspaceSessionStore
{
    private readonly Action<string> _report;
    internal string FilePath { get; }

    internal WorkspaceSessionStore(Action<string> report, string? applicationId = null, string? settingsRoot = null)
    {
        _report = report;
        FilePath = Path.Combine(DockLayoutStore.ApplicationDirectory(applicationId, settingsRoot), "workspace-session.json");
    }

    internal WorkspaceSession Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            if (new FileInfo(FilePath).Length > 1024 * 1024) throw new InvalidDataException("Session metadata is too large.");
            var session = JsonSerializer.Deserialize<WorkspaceSession>(File.ReadAllText(FilePath))
                ?? throw new InvalidDataException("Session metadata is empty.");
            if (session.Version != 1) throw new InvalidDataException("Unsupported session version.");
            return session;
        }
        catch (Exception ex)
        {
            _report($"Workspace session ignored: {ex.Message} Start with --empty-workspace to bypass restoration.");
            return new();
        }
    }

    internal bool Write(WorkspaceSession session)
    {
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(session));
            File.Move(temporary, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) { _report($"Workspace session could not be saved: {ex.Message}"); return false; }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) { _report($"Session temporary file could not be removed: {ex.Message}"); }
        }
    }
}

/// <summary>Reopens current files through host adapters; failures are isolated to one document.</summary>
internal static class WorkspaceSessionRestore
{
    internal static void Restore(WorkspaceSession session, IReadOnlyCollection<string> supportedFormats,
        Action<string> setDirectory, Action<WorkspaceSessionDocument> open,
        Action<string> activate, Action<string> report, bool startEmpty = false)
    {
        if (startEmpty || !session.RestoreOnStartup) return;
        if (session.WorkingDirectory is { } directory)
        {
            try
            {
                if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
                setDirectory(Path.GetFullPath(directory));
            }
            catch (Exception ex) { report($"Working directory was not restored: {ex.Message} Choose File > Open working directory."); }
        }

        var opened = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in (session.Documents ?? []).Take(1024))
        {
            try
            {
                if (document is null) continue;
                if (!Path.IsPathFullyQualified(document.Path)) throw new InvalidDataException("Expected an absolute document path.");
                var identity = WorkspaceSessionDocument.FromPath(document.Path);
                if (identity is null || identity.Format != document.Format || !supportedFormats.Contains(identity.Format))
                    throw new NotSupportedException($"Editor unavailable for {document.Path}.");
                if (!attempted.Add(identity.Path)) continue;
                if (!File.Exists(identity.Path)) throw new FileNotFoundException("File was moved or removed.", identity.Path);
                open(identity);
                opened.Add(identity.Path);
            }
            catch (Exception ex) { report($"Could not restore {document?.Path}: {ex.Message} Reopen it with File > Open."); }
        }
        if (session.ActiveDocument is { } active)
        {
            try
            {
                active = Path.GetFullPath(active);
                if (opened.Contains(active)) activate(active);
            }
            catch (Exception ex) { report($"Active document was not restored: {ex.Message}"); }
        }
    }
}
