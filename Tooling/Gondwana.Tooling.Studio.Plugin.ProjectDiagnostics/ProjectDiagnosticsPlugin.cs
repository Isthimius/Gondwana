using Gondwana.Tooling.Studio.WinForms.Extensibility;

namespace Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics;

/// <summary>Reference Studio plugin: lifecycle orchestration, panel and menu contributions.</summary>
public sealed class ProjectDiagnosticsPlugin : IStudioPlugin
{
    private readonly ProjectDiagnosticsScanner _scanner = new();
    private ProjectDiagnosticsPanel? _panel;
    private CancellationTokenSource? _cancellation;
    private Task<ProjectScanResult>? _scan;
    private string? _projectPath;

    public string Name => "Project Diagnostics";

    public void OnProjectOpened(string projectPath)
    {
        OnProjectClosed();
        _projectPath = projectPath;
        Rescan();
    }

    public void OnProjectClosed()
    {
        CancelScan();
        _projectPath = null;
        _panel?.Clear("No working directory.");
    }

    public Control CreatePanel()
    {
        if (_panel is not null) return _panel;
        _panel = new ProjectDiagnosticsPanel(Rescan, PollScan);
        _panel.Disposed += (_, _) => OnProjectClosed();
        return _panel;
    }

    public ToolStripMenuItem CreateMenuItem()
    {
        var root = new ToolStripMenuItem(Name);
        root.DropDownItems.Add("Rescan", null, (_, _) => Rescan());
        return root;
    }

    /// <summary>Replaces the current scan. Called on Studio's UI thread, like lifecycle callbacks.</summary>
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
