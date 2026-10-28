using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Studio.WinForms.Documents;
using Gondwana.Tooling.Studio.WinForms.Panels;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Studio.WinForms;

public sealed partial class MainForm : IResourceNavigator
{
    private readonly DependencyExplorerPanel _dependencies = new();
    private readonly SynchronizationContext _dependencyUi = new WindowsFormsSynchronizationContext();
    private DockContent? _dependencyTool;
    private CancellationTokenSource? _dependencyCancellation;
    private long _dependencyGeneration;
    private DefinitionReferenceDiscovery? _dependencyScan;
    private ResourceAddress? _dependencyTarget;
    private bool _followDependencySelection;
    internal DefinitionReferenceDiscovery? DependencyScan => _dependencyScan;
    internal DependencyExplorerPanel DependencyPanel => _dependencies;
    internal Task DependencyRefresh { get; private set; } = Task.CompletedTask;
    internal Func<string, Func<string, string?>, CancellationToken, Task<DefinitionReferenceDiscovery>> InspectDependencies { get; set; } =
        (scope, credentials, token) => Task.Run(() => DefinitionReferenceDiscovery.Inspect(scope, credentials, token));

    /// <summary>Uses the existing document registry and editor factory without reloading an open document.</summary>
    public ResourceNavigationResult Navigate(ResourceAddress address)
    {
        if (InvokeRequired) throw new InvalidOperationException("Studio navigation must run on its UI thread.");
        try
        {
            var format = StudioDocument.FormatFor(address.Location.AssetType) ??
                (address.Location.AssetType == Gondwana.Assets.AssetTypes.Misc && address.Location.AssetEntryName is null &&
                AssetArchiveFormat.IsArchive(address.Location.Path) ? "gaf" : null);
            if (format is null) return new(false, false, $"No Studio definition editor supports {address.Location}.");
            if (address.Location.AssetEntryName is null && StudioDocument.FormatFor(address.Location.Path) != format)
                return new(false, false, $"Expected {format} document: {address.Location}.");
            var document = OpenDocument(address.Location);
            if (address.Member is not null && !document.Document.SelectMember(address.Member))
                return new(true, false, $"Opened document; member '{address.Member}' is missing or ambiguous in the current editor: {address.Location}.");
            document.Activate();
            document.Document.Editor.Focus();
            return address.FieldPath is null ? new(true, true, $"Opened {address}.")
                : new(true, false, $"Selected nearest supported member. Reference field: {address.FieldPath}");
        }
        catch (OperationCanceledException) { return new(false, false, "Navigation cancelled; archive authentication may be required."); }
        catch (Exception ex) { return new(false, false, $"Cannot navigate to {address}: {ex.Message}"); }
    }

    private void InitializeDependencies()
    {
        _dependencyTool = AddTool("shell.dependencies", "Dependency Explorer", _dependencies, DockState.DockRight);
        _dependencies.RefreshRequested += () => { InvalidateDependencies(); _ = RefreshDependenciesAsync(); };
        _dependencies.PhysicalRequested += () =>
        {
            if (_dependencyTarget is null) return;
            _followDependencySelection = false;
            _dependencyTarget = new(_dependencyTarget.Location);
            _ = RefreshDependenciesAsync();
        };
        _dependencies.NavigateRequested += address =>
        {
            var result = Navigate(address);
            _output.Log(result.Message);
            if (!result.Exact) _dependencies.SetStatus(result.Message);
        };
        Browser.ReferencesRequested += location => ShowDependencies(new(location), follow: false);
        _dependencies.InspectRequested += (address, dependents) => ShowDependencies(address, follow: false, dependents);
    }

    internal void ShowDependencies(ResourceAddress? address = null, bool follow = true, bool? dependents = null)
    {
        _followDependencySelection = follow;
        _dependencyTarget = address ?? ActiveAddress();
        _dependencies.RequestedSection = dependents;
        _dependencyTool?.Show(Workspace);
        DependencyRefresh = RefreshDependenciesAsync();
    }

    private ResourceAddress? ActiveAddress() => ActiveDocument?.Document is { Location: { } location } document
        ? new(location, document.SelectedMember) : null;

    private void DependencySelectionChanged(object? sender = null, EventArgs? e = null)
    {
        if (!_followDependencySelection || _disposed) return;
        _dependencyTarget = ActiveAddress();
        _ = RefreshDependenciesAsync();
    }

    private void InvalidateDependencies()
    {
        _dependencyGeneration++;
        _dependencyCancellation?.Cancel();
        _dependencyCancellation?.Dispose();
        _dependencyCancellation = null;
        _dependencyScan = null;
        if (!_disposed) _dependencies.SetStatus("Saved content changed. Refresh / Rescan to update dependencies.");
    }

    internal async Task RefreshDependenciesAsync()
    {
        if (_disposed) return;
        if (_dependencyTarget is null) { _dependencies.SetStatus("Select a saved document or use Find References in the working directory."); return; }
        if (_dependencyScan is null)
        {
            if (_dependencyCancellation is not null) return;
            var cancellation = _dependencyCancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var generation = _dependencyGeneration;
            var scope = Browser.WorkingDirectory;
            // Never prompt or access the UI-owned credential cache from the scanner thread.
            var credentials = new Dictionary<string, string?>(_archivePasswords, StringComparer.OrdinalIgnoreCase);
            _dependencies.SetStatus($"Scanning saved content in {scope}…");
            try
            {
                var scan = await InspectDependencies(scope, path => credentials.GetValueOrDefault(path), token).ConfigureAwait(false);
                await OnDependencyUiAsync(() =>
                {
                    if (_disposed || generation != _dependencyGeneration || token.IsCancellationRequested) return;
                    _dependencyScan = scan;
                    _dependencyCancellation = null;
                    PresentDependencies();
                }, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                try
                {
                    await OnDependencyUiAsync(() =>
                    {
                        if (!_disposed && generation == _dependencyGeneration)
                        {
                            _dependencyCancellation = null;
                            _dependencies.SetStatus($"Scan failed: {ex.Message}");
                        }
                    }, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            }
            finally { credentials.Clear(); cancellation.Dispose(); }
            return;
        }
        PresentDependencies();
    }

    private void PresentDependencies()
    {
        if (_dependencyTarget is not null && _dependencyScan is not null && !_disposed)
            _dependencies.ShowResults(_dependencyTarget, _dependencyScan, _documents.Any(d => d.Document.Dirty()));
    }

    private async Task OnDependencyUiAsync(Action action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = token.Register(() => completion.TrySetCanceled(token));
        _dependencyUi.Post(_ =>
        {
            if (token.IsCancellationRequested) return;
            try { action(); completion.TrySetResult(); }
            catch (Exception ex) { completion.TrySetException(ex); }
        }, null);
        await completion.Task.ConfigureAwait(false);
    }
}
