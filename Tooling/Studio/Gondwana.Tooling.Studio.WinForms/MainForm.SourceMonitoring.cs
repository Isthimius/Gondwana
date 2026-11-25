using System.ComponentModel;
using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Studio.Shared.WinForms.Theming;
using Gondwana.Tooling.Studio.WinForms.Documents;

namespace Gondwana.Tooling.Studio.WinForms;

public sealed partial class MainForm
{
    private readonly DocumentSourceMonitor _sourceMonitor = new();
    private bool _sourceBrowserRefreshQueued;
    internal int SourceRegistrationCount => _documents.Sum(d => d.Document.Sources.Count) + _dependencySources.Count;

    private void SourcesChanged(object? sender, EventArgs e)
    {
        if (sender is not StudioDocument model || model.SourcesQueued || _disposed)
            return;
        model.SourcesQueued = true;
        _dependencyUi.Post(_ =>
        {
            model.SourcesQueued = false;
            if (_disposed)
                return;
            var tab = _documents.FirstOrDefault(d => d.Document == model);
            if (tab is not null && !tab.IsDisposed)
                RefreshSourceRegistrations(tab);
        }, null);
    }

    // Reuse the existing format/reference walker, including every logical member. This is a
    // subscription inventory, not a second graph or workspace scan. Remote URIs are omitted by References.
    private IEnumerable<AssetLocation> DocumentDependencies(StudioDocument model,
        Dictionary<AssetLocation, DocumentSourceVersion> discovered, HashSet<AssetLocation> observed)
    {
        var owner = model.Location ?? new AssetLocation(StudioDocument.TypeFor(model.Extension),
            Path.Combine(model.ReferenceDirectory, "Untitled." + model.Extension));
        var roots = model.SerializePacked is not null
            ? new[] { (owner, model.SerializePacked()) }
            : model.Editor is Gondwana.Tooling.Studio.Assets.WinForms.AssetEditorControl assets
                ? DocumentSourceDependencies.ArchiveRoots(assets.AssetsFile)
                : Enumerable.Empty<(AssetLocation, byte[])>();
        return DocumentSourceDependencies.Discover(owner, roots, model.Kind == "asset",
            path => _archivePasswords.GetValueOrDefault(path), discovered, observed);
    }

    private void RefreshSourceRegistrations(StudioDockDocument tab,
        IReadOnlyDictionary<AssetLocation, DocumentSourceVersion>? refreshedPreviews = null)
    {
        if (_disposed || tab.IsDisposed)
            return;
        var model = tab.Document;
        var previous = model.Sources.ToDictionary(p => p.Key, p => p.Value.Accepted, DocumentLocationComparer.Instance);
        var discovered = new Dictionary<AssetLocation, DocumentSourceVersion>(DocumentLocationComparer.Instance);
        var observed = new HashSet<AssetLocation>(DocumentLocationComparer.Instance);
        AssetLocation[] dependencies;
        try
        {
            dependencies = DocumentDependencies(model, discovered, observed).ToArray();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or InvalidOperationException or
            NullReferenceException or Newtonsoft.Json.JsonException)
        {
            // Preserve both previously monitored dependencies and valid references yielded
            // before an invalid member interrupted the walk (including on the first open).
            observed.UnionWith(model.Sources.Where(p => p.Value.Role == DocumentSourceRole.Dependency).Select(p => p.Key));
            dependencies = observed.ToArray();
        }

        var generation = ++model.SourceGeneration;
        foreach (var registration in model.Sources.Values)
            registration.Dispose();
        model.Sources.Clear();
        var primary = model.Location;
        if (primary is not null && model.AcceptedSource is { } accepted)
            Register(primary, accepted, DocumentSourceRole.Primary);
        model.DependencyArchives.Clear();
        foreach (var dependency in dependencies)
        {
            if (model.Sources.ContainsKey(dependency))
                continue;
            if (dependency.AssetEntryName is not null)
                model.DependencyArchives.Add(dependency.Path);
            var version = refreshedPreviews?.GetValueOrDefault(dependency) ?? previous.GetValueOrDefault(dependency)
                ?? discovered.GetValueOrDefault(dependency) ?? DocumentSourceVersion.Read(dependency, _archivePasswords.GetValueOrDefault(dependency.Path));
            Register(dependency, version, DocumentSourceRole.Dependency);
        }

        // A registration recreated after a file reverted starts at the accepted bytes;
        // no change notification will fire for that recovery. Clear an obsolete conflict
        // only after verifying the primary, without adopting an external revision.
        if (primary is not null && model.ContentStale && model.AcceptedSource is { } baseline &&
            DocumentSourceVersion.Read(primary, model.ArchivePassword) == baseline)
        {
            model.ContentStale = false;
            model.SourceProblem = null;
            model.PromptedConflict = null;
            tab.UpdateCaption();
        }

        void Register(AssetLocation location, DocumentSourceVersion version, DocumentSourceRole role)
        {
            // Missing parent directories cannot be watched by FileSystemWatcher. Retry on edits/Refresh.
            if (!Directory.Exists(Path.GetDirectoryName(location.Path)))
                return;
            var password = role == DocumentSourceRole.Primary ? model.ArchivePassword : _archivePasswords.GetValueOrDefault(location.Path);
            model.Sources.Add(location, _sourceMonitor.Register(location, version, role, change =>
            {
                // Under the shared monitor lock: only enqueue. Never Invoke, inspect controls, or reload here.
                _dependencyUi.Post(_ =>
                {
                    if (_disposed || tab.IsDisposed || !_documents.Contains(tab) || model.SourceGeneration != generation)
                        return;
                    ApplySourceChange(tab, change);
                }, null);
            }, password));
        }
    }

    private void ApplySourceChange(StudioDockDocument tab, DocumentSourceChange change)
    {
        var model = tab.Document;
        if (change.Role == DocumentSourceRole.Primary)
        {
            var accepted = model.AcceptedSource;
            if (change.Current == accepted)
            {
                model.ContentStale = false;
                model.SourceProblem = null;
            }
            else if (change.Kind == DocumentSourceChangeKind.ContainerChanged && model.PackedLocation() is not null &&
                change.Current.ContentHash == accepted?.ContentHash)
            {
                // Another entry changed. Retain this entry's model and history, but use the verified
                // physical baseline when coordinating its next archive save.
                model.ArchiveHash = Convert.FromHexString(change.Current.PhysicalHash!);
                model.Sources[change.Location].Accept(change.Current);
                model.ContentStale = false;
                model.SourceProblem = null;
            }
            else
            {
                model.ContentStale = true;
                model.SourceProblem = change.Kind switch
                {
                    DocumentSourceChangeKind.Renamed => $"Source renamed to {change.RenamedTo}. The document still targets its original location.",
                    DocumentSourceChangeKind.Deleted => "Source or typed entry is missing. Keep edits, retry Reload, or Save As.",
                    DocumentSourceChangeKind.Inaccessible => "Source is temporarily inaccessible. Retrying verification; edits are retained.",
                    _ => "Saved content changed outside this editor. Reload or Save As; Keep My Edits does not authorize overwrite."
                };
            }
        }
        else if (change.Kind != DocumentSourceChangeKind.ContainerChanged && change.Current != change.Accepted)
            model.PreviewStale = true;

        _packages.Invalidate(change.Location.Path);
        InvalidateDependencies(); // Cancels and generation-invalidates outstanding scans; never starts one.
        tab.UpdateCaption();
        QueueSourceBrowserRefresh();
    }

    private void QueueSourceBrowserRefresh()
    {
        if (_sourceBrowserRefreshQueued)
            return;
        _sourceBrowserRefreshQueued = true;
        _dependencyUi.Post(_ =>
        {
            _sourceBrowserRefreshQueued = false;
            if (!_disposed)
            {
                Browser.RefreshDirectory();
                SyncActiveDocumentSelection();
            }
        }, null);
    }

    /// <summary>Uses real verification but deterministic UI dispatch in integration tests and explicit refresh.</summary>
    internal void VerifyDocumentSources()
    {
        foreach (var registration in _dependencySources.ToArray())
            registration.Verify();
        foreach (var tab in _documents.ToArray())
            foreach (var registration in tab.Document.Sources.Values.ToArray())
                registration.Verify();
    }

    internal enum SourceConflictChoice { KeepEdits, Reload, SaveAs }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<StudioDocument, SourceConflictChoice> AskSourceConflict { get; set; } = model =>
        ThemedMessageBox.Show($"{model.Caption} changed on disk. Your edits have been retained.\n\n" +
            "Yes: Reload from Disk (confirm discard next)\nNo: Save As a different file\nCancel: Keep My Edits (save remains protected)",
            "External content conflict", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning) switch
        {
            DialogResult.Yes => SourceConflictChoice.Reload,
            DialogResult.No => SourceConflictChoice.SaveAs,
            _ => SourceConflictChoice.KeepEdits
        };

    internal async Task<bool> ResolveSourceConflict(StudioDockDocument tab)
    {
        if (tab.IsDisposed || !tab.Document.ContentStale)
            return false;
        return AskSourceConflict(tab.Document) switch
        {
            SourceConflictChoice.Reload => ReloadDocument(tab),
            SourceConflictChoice.SaveAs => await SaveDocument(tab, saveAs: true),
            _ => false
        };
    }

    private bool HandleSourceSaveFailure(StudioDockDocument tab)
    {
        var model = tab.Document;
        if (model.Location is not { } location || model.AcceptedSource is not { } accepted)
            return false;
        var current = DocumentSourceVersion.Read(location, model.ArchivePassword);
        if (current == accepted)
            return false;
        ApplySourceChange(tab, new(location, DocumentSourceRole.Primary,
            current.Status == DocumentSourceStatus.Inaccessible ? DocumentSourceChangeKind.Inaccessible :
            current.Status == DocumentSourceStatus.Missing ? DocumentSourceChangeKind.Deleted : DocumentSourceChangeKind.ContentChanged,
            accepted, current));
        if (model.PromptedConflict != current)
        {
            model.PromptedConflict = current;
            // The current save must finish before Reload/Save As can run through the normal command gates.
            _dependencyUi.Post(async _ =>
            {
                if (!_disposed && !tab.IsDisposed && _documents.Contains(tab) && model.ContentStale)
                    await ResolveSourceConflict(tab);
            }, null);
        }

        return true;
    }
}
