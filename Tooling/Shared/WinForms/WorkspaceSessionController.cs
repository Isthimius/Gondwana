using System.Diagnostics;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.WinForms;

/// <summary>Small shell bridge; hosts retain ownership of opening, validation, and close approval.</summary>
internal sealed class WorkspaceSessionController
{
    private readonly List<string> _messages = [];
    private readonly WorkspaceSessionStore _store;
    private readonly bool _hadSession;
    private WorkspaceSession _session;
    private Action<string>? _report;
    private bool _restored;
    internal bool IsRestoring { get; private set; }
    internal bool StartEmpty { get; }

    internal WorkspaceSessionController(bool? startEmpty = null)
    {
        _store = new(Report);
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
        restore.CheckedChanged += (_, _) => { _session = _session with { RestoreOnStartup = restore.Checked }; _store.Write(_session); };
        var empty = new ToolStripMenuItem("Start empty on next launch") { CheckOnClick = true };
        empty.CheckedChanged += (_, _) => { _session = _session with { StartEmptyOnce = empty.Checked }; _store.Write(_session); };
        file.DropDownItems.AddRange([restore, empty]);
    }

    internal void Restore(IReadOnlyCollection<string> formats, Action<string> setDirectory,
        Action<WorkspaceSessionDocument> open, Action<string> activate, Action<string> report)
    {
        if (_restored) return;
        _restored = true;
        _report = report;
        foreach (string message in _messages) report(message);
        _messages.Clear();
        IsRestoring = true;
        try { WorkspaceSessionRestore.Restore(_session, formats, setDirectory, open, activate, Report, StartEmpty); }
        finally { IsRestoring = false; }
    }

    /// <summary>Call after close approval, before any document is removed or disposed.</summary>
    internal void Save(string directory, IEnumerable<string?> paths, string? activePath)
    {
        var documents = paths.Select(WorkspaceSessionDocument.FromPath).OfType<WorkspaceSessionDocument>()
            .DistinctBy(document => document.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        _session = _session with
        {
            WorkingDirectory = directory,
            Documents = documents,
            ActiveDocument = documents.FirstOrDefault(document => string.Equals(document.Path, activePath, StringComparison.OrdinalIgnoreCase))?.Path
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

    private void Report(string message)
    {
        Trace.TraceWarning(message);
        if (_report is { } report) report(message);
        else _messages.Add(message);
    }
}
