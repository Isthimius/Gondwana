using Gondwana.Assets;

namespace Gondwana.Tooling.Assets;

public enum DocumentSourceRole { Primary, Dependency }
public enum DocumentSourceChangeKind { Unchanged, ContentChanged, ContainerChanged, Deleted, Renamed, Inaccessible }

/// <summary>A verified observation. Role distinguishes document content from referenced media or definitions.</summary>
public sealed record DocumentSourceChange(AssetLocation Location, DocumentSourceRole Role,
    DocumentSourceChangeKind Kind, DocumentSourceVersion Accepted, DocumentSourceVersion Current, string? RenamedTo = null);

/// <summary>Scoped, nonrecursive watchers. Events request content verification; they are never evidence of a content change.</summary>
/// <remarks>Notifications run under the monitor's serialization gate, on a worker thread or the explicit Verify caller. Handlers must be brief;
/// hosts marshal UI work themselves. Dispose a registration to unregister it; dispose/register to change its path.</remarks>
public sealed class DocumentSourceMonitor : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly List<Registration> _registrations = [];
    private readonly TimeSpan _debounce;
    private readonly Timer _timer;
    private bool _disposed;
    private bool _timerRunning;
    private readonly bool _enableWatchers;

    public DocumentSourceMonitor(TimeSpan? debounce = null) : this(debounce, true) { }

    internal DocumentSourceMonitor(TimeSpan? debounce, bool enableWatchers)
    {
        _enableWatchers = enableWatchers;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(150);
        if (_debounce <= TimeSpan.Zero || _debounce.TotalMilliseconds > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(debounce));
        _timer = new Timer(Tick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Registers one source using the version captured when the host read its document or dependency.
    /// The containing directory must exist. Register each explicit local dependency separately.</summary>
    public Registration Register(AssetLocation location, DocumentSourceVersion accepted, DocumentSourceRole role,
        Action<DocumentSourceChange> changed, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(changed);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var directory = Path.GetDirectoryName(location.Path)!;
            if (!_watchers.ContainsKey(directory))
            {
                var watcher = new FileSystemWatcher(directory)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
                };
                watcher.Changed += OnChanged;
                watcher.Created += OnChanged;
                watcher.Deleted += OnChanged;
                watcher.Renamed += OnRenamed;
                watcher.Error += OnError;
                try
                {
                    watcher.EnableRaisingEvents = _enableWatchers;
                    _watchers.Add(directory, watcher);
                }
                catch { watcher.Dispose(); throw; }
            }
            var registration = new Registration(this, location, accepted, role, changed, password);
            _registrations.Add(registration);
            Schedule(registration); // Covers changes between the opening read and registration.
            return registration;
        }
    }

    private static bool SamePath(string a, string b) => string.Equals(a, b,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private void OnChanged(object sender, FileSystemEventArgs args) => Signal(args.FullPath);
    private void OnRenamed(object sender, RenamedEventArgs args)
    {
        Signal(args.OldFullPath, args.FullPath);
        Signal(args.FullPath);
    }
    private void OnError(object sender, ErrorEventArgs args)
    {
        lock (_gate)
            foreach (var registration in _registrations) Schedule(registration);
    }
    internal void Signal(string path, string? renamedTo = null)
    {
        lock (_gate)
        {
            if (_disposed) return;
            foreach (var registration in _registrations.Where(r => SamePath(r.Location.Path, path)))
            {
                registration.RenamedTo = renamedTo ?? registration.RenamedTo;
                registration.Candidate = null;
                Schedule(registration);
            }
        }
    }

    private void Schedule(Registration registration)
    {
        registration.Pending = true;
        if (!_disposed && !_timerRunning)
        {
            _timerRunning = true;
            _timer.Change(_debounce, _debounce);
        }
    }

    private void Tick(object? state)
    {
        lock (_gate)
        {
            if (_disposed) return;
            foreach (var registration in _registrations.ToArray())
            {
                if (!registration.Pending || registration.Closed) continue;
                var current = DocumentSourceVersion.Read(registration.Location, registration.Password);
                // Require two equal observations separated by the debounce interval, including missing files.
                if (registration.Candidate != current)
                {
                    registration.Candidate = current;
                    registration.CandidateAt = System.Diagnostics.Stopwatch.GetTimestamp();
                    Schedule(registration);
                    continue;
                }
                if (state is not true && System.Diagnostics.Stopwatch.GetElapsedTime(registration.CandidateAt) < _debounce)
                    continue;
                registration.Pending = false;
                registration.Observe(current);
                // No new filesystem event is guaranteed when a sharing lock is released.
                if (!registration.Closed && current.Status == DocumentSourceStatus.Inaccessible) Schedule(registration);
            }
            if (!_disposed && !_registrations.Any(r => r.Pending))
            {
                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _timerRunning = false;
            }
        }
    }

    internal void VerifyPending() => Tick(true);

    internal int DirectoryCount { get { lock (_gate) return _watchers.Count; } }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Dispose();
            foreach (var watcher in _watchers.Values) watcher.Dispose();
            _watchers.Clear();
            foreach (var registration in _registrations) registration.Closed = true;
            _registrations.Clear();
        }
    }

    /// <summary>One registered primary or dependency source. Own saves and accepted reloads retain exact versions, never time windows.</summary>
    public sealed class Registration : IDisposable
    {
        private readonly DocumentSourceMonitor _owner;
        private readonly Action<DocumentSourceChange> _changed;
        internal readonly string? Password;
        internal bool Pending, Closed;
        internal string? RenamedTo;
        internal DocumentSourceVersion? Candidate;
        internal long CandidateAt;
        private DocumentSourceVersion _accepted, _observed;
        public AssetLocation Location { get; }
        public DocumentSourceRole Role { get; }
        public DocumentSourceVersion Accepted { get { lock (_owner._gate) return _accepted; } }

        internal Registration(DocumentSourceMonitor owner, AssetLocation location, DocumentSourceVersion accepted,
            DocumentSourceRole role, Action<DocumentSourceChange> changed, string? password)
        { _owner = owner; Location = location; _accepted = _observed = accepted; Role = role; _changed = changed; Password = password; }

        /// <summary>Accept only the exact version successfully loaded or saved, not a fresh post-save disk read.</summary>
        public void Accept(DocumentSourceVersion version)
        {
            ArgumentNullException.ThrowIfNull(version);
            if (version.Status != DocumentSourceStatus.Available) throw new ArgumentException("Only readable content can be accepted.", nameof(version));
            lock (_owner._gate)
            {
                ObjectDisposedException.ThrowIf(Closed, this);
                _accepted = _observed = version;
                Candidate = null;
                _owner.Schedule(this);
            }
        }

        /// <summary>Serializes a loose native document and accepts its exact saved hash before watcher callbacks can run.</summary>
        public DocumentSourceVersion Save(Action<Action<string>> serialize)
        {
            lock (_owner._gate)
            {
                ObjectDisposedException.ThrowIf(Closed, this);
                var installed = DocumentSourceSave.Save(Location, _accepted, serialize);
                Accept(installed);
                return installed;
            }
        }

        /// <summary>Explicit verification for focus/refresh and deterministic callers. Unchanged is returned but not notified.</summary>
        public DocumentSourceChange Verify()
        {
            lock (_owner._gate)
            {
                ObjectDisposedException.ThrowIf(Closed, this);
                return Observe(DocumentSourceVersion.Read(Location, Password));
            }
        }

        internal DocumentSourceChange Observe(DocumentSourceVersion current)
        {
            var kind = current.Status switch
            {
                DocumentSourceStatus.Inaccessible => DocumentSourceChangeKind.Inaccessible,
                DocumentSourceStatus.Missing => RenamedTo is null ? DocumentSourceChangeKind.Deleted : DocumentSourceChangeKind.Renamed,
                _ when current.ContentHash != _accepted.ContentHash => DocumentSourceChangeKind.ContentChanged,
                _ when current.PhysicalHash != _accepted.PhysicalHash => DocumentSourceChangeKind.ContainerChanged,
                _ => DocumentSourceChangeKind.Unchanged
            };
            var change = new DocumentSourceChange(Location, Role, kind, _accepted, current,
                kind == DocumentSourceChangeKind.Renamed ? RenamedTo : null);
            RenamedTo = null;
            if (current != _observed)
            {
                _observed = current;
                // Recovery to accepted content is meaningful after a missing/inaccessible observation.
                try { _changed(change); }
                catch (Exception ex) { System.Diagnostics.Trace.TraceError("Document source notification failed: {0}", ex); }
            }
            return change;
        }

        public void Dispose()
        {
            lock (_owner._gate)
            {
                if (Closed) return;
                Closed = true;
                _owner._registrations.Remove(this);
                var directory = Path.GetDirectoryName(Location.Path)!;
                if (!_owner._registrations.Any(r => SamePath(Path.GetDirectoryName(r.Location.Path)!, directory)) &&
                    _owner._watchers.Remove(directory, out var watcher)) watcher.Dispose();
            }
        }
    }
}
