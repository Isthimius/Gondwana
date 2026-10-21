using System.Diagnostics;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.WinForms;

/// <summary>Small shell bridge; hosts retain ownership of opening, validation, and close approval.</summary>
internal sealed class WorkspaceSessionController
{
    private readonly List<string> _startupMessages = [];
    private readonly WorkspaceSessionStore _store;
    private readonly bool _hadSession;
    private WorkspaceSession _session;
    private Action<string>? _reportPersistenceError;
    private bool _restored;
    internal bool IsRestoring { get; private set; }
    internal bool StartEmpty { get; }

    internal WorkspaceSessionController(bool? startEmpty = null)
    {
        _store = new(ReportPersistenceError);
        _hadSession = File.Exists(_store.FilePath);
        _session = _store.Read();
        StartEmpty = (startEmpty ?? (Environment.GetCommandLineArgs().Contains("--empty-workspace", StringComparer.OrdinalIgnoreCase)
            || (Control.ModifierKeys & Keys.Shift) != 0)) || _session.StartEmptyOnce;
        if (_session.StartEmptyOnce)
        {
            _session = _session with { StartEmptyOnce = false };
            _store.Write(_session);
        }
    }

    internal string InitialWorkingDirectory(string? legacyDirectory = null)
    {
        if (StartEmpty || !_session.RestoreOnStartup) return Environment.CurrentDirectory;
        string? directory = _hadSession ? _session.WorkingDirectory : legacyDirectory;
        return directory is not null && Directory.Exists(directory) ? directory : Environment.CurrentDirectory;
    }

    internal void AddMenu(ToolStripMenuItem file)
    {
        file.DropDownItems.Add(new ToolStripSeparator());
        var restore = new ToolStripMenuItem("Restore workspace on startup") { Checked = _session.RestoreOnStartup, CheckOnClick = true };
        restore.Click += (_, _) =>
        {
            var updated = _session with { RestoreOnStartup = restore.Checked };
            if (_store.Write(updated)) _session = updated;
            else restore.Checked = _session.RestoreOnStartup;
        };
        var empty = new ToolStripMenuItem("Start empty on next launch") { CheckOnClick = true };
        empty.Click += (_, _) =>
        {
            var updated = _session with { StartEmptyOnce = empty.Checked };
            if (_store.Write(updated)) _session = updated;
            else empty.Checked = _session.StartEmptyOnce;
        };
        file.DropDownItems.AddRange([restore, empty]);
    }

    internal void Restore(IReadOnlyCollection<string> formats, Action<string> setDirectory,
        Action<WorkspaceSessionDocument> open, Action<string> activate, Action<string> reportStartup, Action<string> reportPersistenceError,
        Action<WorkspaceSessionDocument>? activatePacked = null)
    {
        if (_restored) return;
        _restored = true;
        _reportPersistenceError = reportPersistenceError;
        foreach (string message in _startupMessages) reportStartup(message);
        _startupMessages.Clear();
        IsRestoring = true;
        try
        {
            WorkspaceSessionRestore.Restore(_session, formats, setDirectory, open, activate, message =>
            {
                Trace.TraceWarning(message);
                reportStartup(message);
            }, StartEmpty, activatePacked);
        }
        finally { IsRestoring = false; }
    }

    /// <summary>Call after close approval, before any document is removed or disposed.</summary>
    internal void Save(string directory, IEnumerable<string?> paths, string? activePath)
    {
        var documents = paths.Select(WorkspaceSessionDocument.FromPath).OfType<WorkspaceSessionDocument>()
            .DistinctBy(document => document.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        SaveDocuments(directory, documents, documents.FirstOrDefault(document => string.Equals(document.Path, activePath, StringComparison.OrdinalIgnoreCase)));
    }

    internal void SaveDocuments(string directory, IEnumerable<WorkspaceSessionDocument?> identities, WorkspaceSessionDocument? active)
    {
        var documents = identities.OfType<WorkspaceSessionDocument>().ToArray();
        _session = _session with
        {
            WorkingDirectory = directory,
            Documents = documents,
            ActiveDocument = active?.EntryName is null ? active?.Path : null,
            ActivePackedDocument = active?.EntryName is not null ? active : null
        };
        _store.Write(_session);
    }

    internal static IEnumerable<T> DocumentOrder<T>(DockPanel panel, IEnumerable<T> documents) where T : DockContent
    {
        var owned = documents.ToHashSet();
        return panel.Panes.SelectMany(pane => pane.Contents.Cast<IDockContent>()).OfType<T>()
            .Where(owned.Contains).Concat(documents).Distinct().ToArray();
    }

    internal static void ShowStatus(Form owner, IEnumerable<string> messages)
    {
        string[] items = messages.ToArray();
        if (items.Length > 0)
            MessageBox.Show(owner, string.Join(Environment.NewLine, items), "Workspace restoration", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    internal static void ShowPersistenceError(Form owner, string message) =>
        MessageBox.Show(owner, message, "Workspace session preferences", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private void ReportPersistenceError(string message)
    {
        Trace.TraceWarning(message);
        if (_reportPersistenceError is { } report) report(message);
        else _startupMessages.Add(message);
    }
}
