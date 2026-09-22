namespace Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics;

/// <summary>Read-only, host-themed presentation. Owns its UI timer; never scans files itself.</summary>
internal sealed class ProjectDiagnosticsPanel : UserControl
{
    private readonly TreeView _definitions = new() { Dock = DockStyle.Fill, ShowNodeToolTips = true, HideSelection = false };
    private readonly ListBox _problems = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true, IntegralHeight = false };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 42, AutoEllipsis = true };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 100 };

    public ProjectDiagnosticsPanel(Action rescan, Action poll)
    {
        var button = new Button { Text = "Rescan", Dock = DockStyle.Top, Height = 30 };
        button.Click += (_, _) => rescan();
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Panel1.Controls.Add(_definitions);
        split.Panel1.Controls.Add(new Label { Text = "Definitions", Dock = DockStyle.Top });
        split.Panel2.Controls.Add(_problems);
        split.Panel2.Controls.Add(new Label { Text = "Problems (source — property — reason)", Dock = DockStyle.Top });
        Controls.Add(split);
        Controls.Add(button);
        Controls.Add(_status);
        _timer.Tick += (_, _) => poll();
        _timer.Start();
        Clear("No working directory.");
    }

    public void Clear(string status)
    {
        if (IsDisposed) return;
        _definitions.Nodes.Clear();
        _problems.Items.Clear();
        _status.Text = status;
    }

    public void ShowResult(ProjectScanResult result)
    {
        if (IsDisposed) return;
        Clear($"{result.Definitions.Count} definitions; {result.Definitions.Count(d => !d.Loaded)} load failures; {result.Problems.Count} problems.");
        _definitions.BeginUpdate();
        _problems.BeginUpdate();
        try
        {
            foreach (var definition in result.Definitions)
            {
                var node = _definitions.Nodes.Add($"{definition.RelativePath} [{definition.Format}] — {(definition.Loaded ? "Loaded" : "Load failed")}");
                foreach (var reference in definition.References)
                    node.Nodes.Add(new TreeNode($"{(reference.Problem is null ? "→" : "!")} {reference.Property}: {reference.Value}")
                    { ToolTipText = reference.Problem ?? reference.ResolvedPath ?? "External URI; not fetched." });
            }
            foreach (var problem in result.Problems)
                _problems.Items.Add($"{problem.SourcePath} — {problem.Property} — {problem.Reason}");
        }
        finally
        {
            _definitions.EndUpdate();
            _problems.EndUpdate();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
