using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Studio.Shared.WinForms;

/// <summary>Standalone host integration over the shared source monitor. All public operations run on the UI thread.</summary>
public sealed class EditorSourceMonitorController : IDisposable
{
    private readonly DocumentSourceMonitor _monitor = new();
    private readonly SynchronizationContext _ui = new WindowsFormsSynchronizationContext();
    private readonly HashSet<Document> _documents = [];
    private bool _disposed;
    private bool _enabled = true;
    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; foreach (var document in _documents.ToArray()) document.Reconcile(); }
    }
    public int RegistrationCount => _documents.Sum(d => d._sources.Count);
    public Func<bool> ConfirmDiscard { get; set; } = () => ThemedMessageBox.Show(
        "Discard unsaved edits and reload from disk?", "Reload document", MessageBoxButtons.YesNo,
        MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    public Action<Exception> ReportError { get; set; } = ex => ThemedMessageBox.Show(ex.Message,
        "Document source", MessageBoxButtons.OK, MessageBoxIcon.Error);
    public Func<DialogResult> ChooseConflict { get; set; } = () => ThemedMessageBox.Show(
        "Your edits are retained.\n\nYes: Reload from Disk (confirm discard next)\nNo: Save As\nCancel: Keep My Edits (save remains protected)",
        "External content conflict", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

    /// <summary>Construct/show the replacement before closing the original, preserving docking and activation.</summary>
    public static void Replace(DockContent original, Func<DockContent> create, Action approveClose)
    {
        var active = original.DockPanel?.ActiveDocument as DockContent;
        var focusPath = new List<int>();
        Control focused = original;
        while (focused.Controls.Cast<Control>().FirstOrDefault(c => c.ContainsFocus) is { } child)
        {
            focusPath.Add(focused.Controls.IndexOf(child));
            focused = child;
        }
        DockContent? replacement = null;
        try
        {
            replacement = create();
            if (original.Pane is { } pane)
                replacement.Show(pane, original);
            approveClose();
            original.Close();
            if (active is not null && active != original && !active.IsDisposed)
                active.Activate();
            else
            {
                replacement.Activate();
                Control target = replacement;
                foreach (var index in focusPath)
                {
                    if (index >= target.Controls.Count)
                        break;
                    target = target.Controls[index];
                }
                if (target.CanFocus)
                    target.Focus();
            }
        }
        catch { replacement?.Dispose(); throw; }
    }

    /// <summary>Format callbacks retain ownership of their existing models, serializers, previews, and save workflows.</summary>
    public sealed class Adapter
    {
        public required Func<AssetLocation?> Location { get; init; }
        public required Func<DocumentSourceVersion?> Accepted { get; init; }
        public required Func<bool> Dirty { get; init; }
        public required Func<bool> Commit { get; init; }
        public required Func<IEnumerable<(AssetLocation Location, byte[] Bytes)>> Roots { get; init; }
        public required Func<AssetLocation> ReferenceOwner { get; init; }
        public required Action Reload { get; init; }
        public required Action SaveAs { get; init; }
        public required Func<bool> RefreshPreviews { get; init; }
        public Action<string> Invalidate { get; init; } = _ => { };
        public Func<string, string?> Password { get; init; } = _ => null;
        public bool Archive { get; init; }
    }

    public Document Register(DockContent tab, Adapter adapter)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var document = new Document(this, tab, adapter);
        _documents.Add(document);
        document.Reconcile();
        return document;
    }

    public void Verify()
    {
        foreach (var document in _documents.ToArray())
            document.Verify();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var document in _documents.ToArray())
            document.Dispose();
        _monitor.Dispose();
    }

    /// <summary>One dock's source subscriptions and conflict state; disposal does not dispose the author's document.</summary>
    public sealed class Document : IDisposable
    {
        private readonly EditorSourceMonitorController _owner;
        private readonly DockContent _tab;
        private readonly Adapter _adapter;
        internal readonly Dictionary<AssetLocation, DocumentSourceMonitor.Registration> _sources = new(DocumentSourceDependencies.Comparer);
        private readonly Dictionary<AssetLocation, DocumentSourceVersion> _dependencies = new(DocumentSourceDependencies.Comparer);
        private readonly Dictionary<string, string> _passwords = new(StringComparer.OrdinalIgnoreCase);
        private readonly ContextMenuStrip _menu = new();
        private long _generation;
        private bool _disposed, _queued, _caption;
        private DocumentSourceVersion? _prompted;
        public bool ContentStale { get; private set; }
        public bool PreviewStale { get; private set; }
        public string? Problem { get; private set; }
        private string? Password(string path)
        {
            if (_adapter.Password(path) is { } password)
                _passwords[path] = password;
            return _passwords.GetValueOrDefault(path);
        }

        internal Document(EditorSourceMonitorController owner, DockContent tab, Adapter adapter)
        {
            _owner = owner;
            _tab = tab;
            _adapter = adapter;
            _menu.Items.Add("Reload from Disk", null, (_, _) => Reload());
            _menu.Items.Add("Keep My Edits", null, (_, _) => UpdateCaption());
            _menu.Items.Add("Save As...", null, (_, _) => _adapter.SaveAs());
            _menu.Items.Add("Reload dependency previews", null, (_, _) => RefreshPreviews());
            _menu.Items.Add("Check sources now", null, (_, _) => _owner.Verify());
            var automatic = new ToolStripMenuItem("Monitor external changes") { Checked = owner.Enabled, CheckOnClick = true };
            automatic.CheckedChanged += (_, _) => owner.Enabled = automatic.Checked;
            _menu.Items.Add(automatic);
            _menu.Opening += (_, _) => automatic.Checked = owner.Enabled;
            tab.TabPageContextMenuStrip = _menu;
            tab.TextChanged += CaptionChanged;
            tab.Disposed += Closed;
        }

        public void Changed(object? sender, EventArgs e)
        {
            UpdateCaption();
            if (_queued || _disposed)
                return;
            _queued = true;
            _owner._ui.Post(_ => { _queued = false; if (!_disposed) Reconcile(); }, null);
        }

        public void Saved()
        {
            ContentStale = false;
            Problem = null;
            _prompted = null;
            Reconcile(); // The adapter supplies the serializer's exact installed version, never a fresh disk read.
            UpdateCaption();
        }

        internal void Verify()
        {
            Reconcile();
            if (_owner.Enabled)
            {
                foreach (var source in _sources.Values.ToArray())
                    source.Verify();
                return;
            }
            // Opt-out releases watchers; an explicit check remains available and never accepts disk versions.
            if (_adapter.Location() is { } primary && _adapter.Accepted() is { } accepted)
                Check(primary, accepted, DocumentSourceRole.Primary);
            foreach (var (location, baseline) in _dependencies)
                Check(location, baseline, DocumentSourceRole.Dependency);
            void Check(AssetLocation location, DocumentSourceVersion baseline, DocumentSourceRole role)
            {
                var current = DocumentSourceVersion.Read(location, Password(location.Path));
                var kind = current.Status == DocumentSourceStatus.Missing ? DocumentSourceChangeKind.Deleted :
                    current.Status == DocumentSourceStatus.Inaccessible ? DocumentSourceChangeKind.Inaccessible :
                    current.ContentHash != baseline.ContentHash ? DocumentSourceChangeKind.ContentChanged :
                    current.PhysicalHash != baseline.PhysicalHash ? DocumentSourceChangeKind.ContainerChanged : DocumentSourceChangeKind.Unchanged;
                Observe(new(location, role, kind, baseline, current));
            }
        }

        internal void Reconcile()
        {
            if (_disposed)
                return;
            var found = new HashSet<AssetLocation>(DocumentSourceDependencies.Comparer);
            var versions = new Dictionary<AssetLocation, DocumentSourceVersion>(DocumentSourceDependencies.Comparer);
            try
            {
                _ = DocumentSourceDependencies.Discover(_adapter.ReferenceOwner(), _adapter.Roots(), _adapter.Archive,
                    Password, versions, found).ToArray();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or InvalidOperationException or
                NullReferenceException or Newtonsoft.Json.JsonException)
            { found.UnionWith(_dependencies.Keys); }
            foreach (var obsolete in _dependencies.Keys.Except(found, DocumentSourceDependencies.Comparer).ToArray())
                _dependencies.Remove(obsolete);
            foreach (var source in found)
                if (!_dependencies.ContainsKey(source))
                    _dependencies.Add(source, versions.GetValueOrDefault(source) ?? DocumentSourceVersion.Read(source, Password(source.Path)));
            var generation = ++_generation;
            foreach (var source in _sources.Values)
                source.Dispose();
            _sources.Clear();
            if (!_owner.Enabled)
                return;
            if (_adapter.Location() is { } primary && _adapter.Accepted() is { } accepted)
                Register(primary, accepted, DocumentSourceRole.Primary);
            foreach (var (location, version) in _dependencies)
                if (!_sources.ContainsKey(location))
                    Register(location, version, DocumentSourceRole.Dependency);
            if (ContentStale && _adapter.Location() is { } currentLocation && _adapter.Accepted() is { } baseline &&
                DocumentSourceVersion.Read(currentLocation, Password(currentLocation.Path)) == baseline)
            {
                ContentStale = false;
                Problem = null;
                _prompted = null;
                UpdateCaption();
            }

            void Register(AssetLocation location, DocumentSourceVersion version, DocumentSourceRole role)
            {
                try
                {
                    if (!Directory.Exists(Path.GetDirectoryName(location.Path)))
                        return;
                    _sources.Add(location, _owner._monitor.Register(location, version, role, change =>
                        _owner._ui.Post(_ =>
                        {
                            if (!_disposed && !_tab.IsDisposed && generation == _generation)
                                Observe(change);
                        }, null), Password(location.Path)));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                { Problem = "Automatic monitoring unavailable: " + ex.Message; UpdateCaption(); }
            }
        }

        private void Observe(DocumentSourceChange change)
        {
            if (change.Role == DocumentSourceRole.Primary)
            {
                ContentStale = change.Current != _adapter.Accepted();
                Problem = ContentStale ? change.Kind switch
                {
                    DocumentSourceChangeKind.Deleted => "Source is missing. Reload when restored, or Save As.",
                    DocumentSourceChangeKind.Renamed => "Source was renamed; the editor still targets the original path.",
                    DocumentSourceChangeKind.Inaccessible => "Source is temporarily inaccessible. Edits are retained.",
                    _ => "Source changed externally. Keep edits, Reload from Disk, or Save As."
                } : null;
                if (!ContentStale)
                    _prompted = null;
            }
            else if (change.Kind != DocumentSourceChangeKind.ContainerChanged && change.Current != change.Accepted)
                PreviewStale = true;
            _adapter.Invalidate(change.Location.Path);
            UpdateCaption();
        }

        /// <summary>Shows conflict actions once per external version after an explicit failed save, never from a watcher.</summary>
        public bool SaveFailed(Exception exception)
        {
            if (exception is not IOException || _adapter.Location() is not { } location || _adapter.Accepted() is not { } accepted)
                return false;
            if (exception is DocumentSourceConflictException conflict && !DocumentSourceDependencies.Comparer.Equals(conflict.Location, location))
                return false;
            var current = DocumentSourceVersion.Read(location, Password(location.Path));
            if (current == accepted)
                return false;
            Observe(new(location, DocumentSourceRole.Primary, current.Status == DocumentSourceStatus.Available ?
                DocumentSourceChangeKind.ContentChanged : current.Status == DocumentSourceStatus.Missing ?
                DocumentSourceChangeKind.Deleted : DocumentSourceChangeKind.Inaccessible, accepted, current));
            if (_prompted == current)
                return true;
            _prompted = current;
            _owner._ui.Post(_ =>
            {
                if (_disposed || !ContentStale || !DocumentSourceDependencies.Comparer.Equals(location, _adapter.Location()) ||
                    _adapter.Accepted() != accepted)
                    return;
                var choice = _owner.ChooseConflict();
                if (choice == DialogResult.Yes)
                    Reload();
                else if (choice == DialogResult.No)
                    _adapter.SaveAs();
            }, null);
            return true;
        }

        public void Reload()
        {
            if (_disposed || !_adapter.Commit() || (_adapter.Dirty() && !_owner.ConfirmDiscard()))
                return;
            try
            { _adapter.Reload(); }
            catch (Exception ex) { _owner.ReportError(ex); }
        }

        public void RefreshPreviews()
        {
            if (_disposed)
                return;
            Reconcile();
            var versions = _dependencies.Keys.ToDictionary(p => p, p => DocumentSourceVersion.Read(p, Password(p.Path)), DocumentSourceDependencies.Comparer);
            try
            {
                foreach (var path in versions.Keys)
                    _adapter.Invalidate(path.Path);
                bool loaded = _adapter.RefreshPreviews();
                var newlyUnavailable = versions.Any(p => p.Value.Status != DocumentSourceStatus.Available && p.Value != _dependencies[p.Key]);
                PreviewStale = !loaded || newlyUnavailable;
                if (!PreviewStale)
                {
                    foreach (var pair in versions.Where(p => p.Value.Status == DocumentSourceStatus.Available))
                        _dependencies[pair.Key] = pair.Value;
                    Reconcile();
                }
                UpdateCaption();
            }
            catch (Exception ex) { PreviewStale = true; UpdateCaption(); _owner.ReportError(ex); }
        }

        private void CaptionChanged(object? sender, EventArgs e) => UpdateCaption();
        private void UpdateCaption()
        {
            if (_disposed || _caption)
                return;
            _caption = true;
            try
            {
                var text = _tab.Text.Replace(" [reload]", "").Replace(" [conflict]", "").Replace(" [preview]", "");
                _tab.Text = text + (ContentStale ? _adapter.Dirty() ? " [conflict]" : " [reload]" : "") + (PreviewStale ? " [preview]" : "");
                _tab.TabText = _tab.Text;
                _tab.ToolTipText = (_adapter.Location()?.ToString() ?? "Unsaved document") + (Problem is null ? "" : "\n" + Problem);
            }
            finally { _caption = false; }
        }
        private void Closed(object? sender, EventArgs e) => Dispose();
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _generation++;
            _tab.TextChanged -= CaptionChanged;
            _tab.Disposed -= Closed;
            foreach (var source in _sources.Values)
                source.Dispose();
            _sources.Clear();
            _dependencies.Clear();
            _passwords.Clear();
            _menu.Dispose();
            _owner._documents.Remove(this);
        }
    }
}
