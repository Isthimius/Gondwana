using Gondwana.Tooling.Studio.WinForms.Extensibility;

namespace Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics;

/// <summary>Coordinates background working-directory diagnostics and contributes a result pane and Rescan command.</summary>
public sealed class ProjectDiagnosticsPlugin : IStudioPlugin
{
    private readonly ProjectDiagnosticsScanner _scanner = new();
    private ProjectDiagnosticsPanel? _panel;
    private CancellationTokenSource? _cancellation;
    private Task<ProjectScanResult>? _scan;
    private string? _projectPath;

    /// <summary>Gets the display label used for the diagnostics pane and its Plugins menu entry.</summary>
    public string Name => "Project Diagnostics";

    /// <summary>Cancels the previous scan and starts a scan of the selected working directory.</summary>
    /// <param name="projectPath">The authoring directory to scan recursively.</param>
    public void OnProjectOpened(string projectPath)
    {
        OnProjectClosed();
        _projectPath = projectPath;
        Rescan();
    }

    /// <summary>Requests scan cancellation, discards pending results, and clears the working directory and displayed diagnostics.</summary>
    public void OnProjectClosed()
    {
        CancelScan();
        _projectPath = null;
        _panel?.Clear("No working directory.");
    }

    /// <summary>Creates the diagnostics panel once and connects its timer and Rescan button to this plugin.</summary>
    /// <returns>The cached panel, owned by Studio after hosting; disposing it cancels the current scan.</returns>
    public Control CreatePanel()
    {
        if (_panel is not null) return _panel;
        _panel = new ProjectDiagnosticsPanel(Rescan, PollScan);
        _panel.Disposed += (_, _) => OnProjectClosed();
        return _panel;
    }

    /// <summary>Creates a Plugins menu entry containing a Rescan command for the current working directory.</summary>
    /// <returns>A new menu item for the caller to own and dispose.</returns>
    public ToolStripMenuItem CreateMenuItem()
    {
        var root = new ToolStripMenuItem(Name);
        root.DropDownItems.Add("Rescan", null, (_, _) => Rescan());
        return root;
    }

    /// <summary>Cancels the previous scan, clears displayed results, and starts a replacement worker scan.</summary>
    /// <remarks>Call on the UI thread. No scan starts without a working directory or after the panel is disposed; cancellation does not wait for the previous worker.</remarks>
    public void Rescan()
    {
        CancelScan();
        if (_projectPath is null || _panel?.IsDisposed == true) return;
        var path = _projectPath;
        _panel?.Clear($"Scanning {path}…");
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        // The task captures no controls. Cancellation is cooperative between files/references.
        _scan = Task.Run(() => _scanner.Scan(path, token), token);
    }

    private void CancelScan()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        if (_scan is { } previous)
        {
            // Observe unexpected faults even when the project closes before the UI polls.
            _ = previous.ContinueWith(task => System.Diagnostics.Trace.TraceError(task.Exception!.ToString()),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
        _scan = null;
    }

    private void PollScan()
    {
        // A WinForms timer runs on the UI thread, including for a hidden dock panel.
        // No worker continuation can invoke a disposed control or repopulate stale results.
        if (_scan is not { IsCompleted: true } completed) return;
        _scan = null;
        _cancellation?.Dispose();
        _cancellation = null;
        try { _panel?.ShowResult(completed.GetAwaiter().GetResult()); }
        catch (OperationCanceledException) { _panel?.Clear("Scan cancelled."); }
        catch (Exception ex) { _panel?.Clear($"Scan failed: {ex.GetBaseException().Message}"); }
    }
}
