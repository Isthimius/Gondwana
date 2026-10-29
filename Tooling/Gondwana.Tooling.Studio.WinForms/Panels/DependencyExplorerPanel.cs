using Gondwana.Tooling.Assets;

namespace Gondwana.Tooling.Studio.WinForms.Panels;

internal sealed class DependencyExplorerPanel : UserControl
{
    private readonly Label _status = new() { Dock = DockStyle.Top };
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false, ShowNodeToolTips = true };
    private readonly ToolTip _tips = new();
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripButton _memberScope = new("Selected Member")
    {
        Name = "DependencyScopeMember",
        ToolTipText = "Show references for the selected logical definition."
    };
    private readonly ToolStripButton _documentScope = new("Whole Document")
    {
        Name = "DependencyScopeDocument",
        ToolTipText = "Show references for every logical definition in this document."
    };
    internal event Action? RefreshRequested;
    internal event Action<bool>? ScopeRequested;
    internal event Action<ResourceAddress>? NavigateRequested;
    internal event Action<ResourceAddress, bool>? InspectRequested;
    internal bool? RequestedSection { get; set; }
    internal string Status => _status.Text;

    internal DependencyExplorerPanel()
    {
        var bar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        bar.Items.Add("Refresh / Rescan", null, (_, _) => RefreshRequested?.Invoke());
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add(new ToolStripLabel("Query:"));
        bar.Items.Add(_memberScope);
        bar.Items.Add(_documentScope);
        _memberScope.Click += (_, _) => ScopeRequested?.Invoke(false);
        _documentScope.Click += (_, _) => ScopeRequested?.Invoke(true);
        SetScope(wholeDocument: true, memberAvailable: false, hasTarget: false);
        _menu.Items.Add("Open Referenced Definition", null, (_, _) => OpenSelection());
        foreach (var title in new[] { "Find References", "Show Dependencies", "Show Dependents" })
            _menu.Items.Add(title, null, (_, _) =>
            {
                if (_tree.SelectedNode?.Tag is ResourceAddress address)
                    InspectRequested?.Invoke(address with { FieldPath = null }, title != "Show Dependencies");
            });
        _menu.Opening += (_, e) => e.Cancel = _tree.SelectedNode?.Tag is not ResourceAddress;
        _tree.ContextMenuStrip = _menu;
        _tree.NodeMouseClick += (_, e) => { if (e.Button == MouseButtons.Right) _tree.SelectedNode = e.Node; };
        _tree.NodeMouseDoubleClick += (_, _) => OpenSelection();
        _tree.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { OpenSelection(); e.Handled = true; } };
        Controls.Add(_tree);
        Controls.Add(_status);
        Controls.Add(bar);
        // The scan summary can span more lines as the pane narrows or DPI/font size grows.
        _status.SizeChanged += (_, _) => UpdateStatusHeight();
        _status.FontChanged += (_, _) => UpdateStatusHeight();
        ToolingAppearance.Apply(this);
        SetStatus("Select a saved document or use Find References in the working directory.");
    }

    /// <summary>Updates the mutually exclusive query choices without issuing another query.</summary>
    internal void SetScope(bool wholeDocument, bool memberAvailable, bool hasTarget)
    {
        _memberScope.Enabled = hasTarget && memberAvailable;
        _documentScope.Enabled = hasTarget;
        _memberScope.Checked = hasTarget && memberAvailable && !wholeDocument;
        _documentScope.Checked = hasTarget && (wholeDocument || !memberAvailable);
    }

    private void OpenSelection()
    {
        if (_tree.SelectedNode?.Tag is ResourceAddress address) NavigateRequested?.Invoke(address);
    }

    internal void SetStatus(string text)
    {
        _status.Text = text;
        _tips.SetToolTip(_status, text);
        UpdateStatusHeight();
    }

    private void UpdateStatusHeight()
    {
        if (_status.ClientSize.Width <= 0) return;

        // Measure at the actual docked width so wrapped lines stay above the results tree.
        var height = _status.GetPreferredSize(new Size(_status.ClientSize.Width, 0)).Height + 2;
        if (_status.Height != height) _status.Height = height;
    }

    internal void ShowResults(ResourceAddress target, DefinitionReferenceDiscovery scan, bool dirty)
    {
        var selected = _tree.SelectedNode?.Tag as ResourceAddress;
        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            var query = new DefinitionReferenceQuery(scan);
            Add("Dependencies / Used By This Resource", query.Dependencies(target), outbound: true);
            Add("Referenced By / Dependents", query.Dependents(target), outbound: false);
            var unresolvedDependencies = query.UnresolvedDependencies(target);
            var unresolvedDependents = query.UnresolvedDependents(target);
            if (unresolvedDependencies.Count > 0) Add("Unresolved dependencies — see scan diagnostics", unresolvedDependencies, outbound: true);
            if (unresolvedDependents.Count > 0) Add("Unresolved inbound references — see scan diagnostics", unresolvedDependents, outbound: false);
            var unknown = query.UnspecifiedDependents(target);
            if (unknown.Count > 0) Add("Physical references — logical member unspecified", unknown, outbound: false);
            var remote = _tree.Nodes.Add("External URI references (not fetched or navigable)");
            foreach (var item in query.ExternalDependencies(target))
                remote.Nodes.Add($"{item.Uri} — {item.Source}");
            var problems = _tree.Nodes.Add($"Scan diagnostics ({scan.Problems.Count})");
            foreach (var problem in scan.Problems)
            {
                var node = problems.Nodes.Add($"{problem.Kind}: {problem.Source}: {problem.Message}");
                node.ToolTipText = node.Text;
                if (problem.Reference is { } reference)
                    node.Tag = new ResourceAddress(reference.Document, reference.Member, reference.ReferencePath);
            }
            _tree.ExpandAll();
            if (RequestedSection is { } dependents)
            {
                _tree.SelectedNode = _tree.Nodes[dependents ? 1 : 0];
                _tree.SelectedNode.EnsureVisible();
                RequestedSection = null;
            }
            SetStatus($"Query: {(target.Member is null ? "Whole document" : $"Selected member '{target.Member}'")}\n" +
                $"Target: {target.Location}\nScan scope: {scan.Scope}\n" +
                (scan.IsCancelled ? "Cancelled — partial results." : scan.IsComplete ? "Completed scan." : "Incomplete scan — see diagnostics; absence is not proof of no references.") +
                " Saved content only; arbitrary C# references are not discovered. Remote URIs are not fetched." +
                (!scan.IsWithinScope(target.Location) ? " Query target is outside this scope; its outbound references were not inspected." : "") +
                (dirty ? " Open editors have unsaved changes not represented here." : ""));
        }
        finally { _tree.EndUpdate(); }

        void Add(string title, IReadOnlyList<DefinitionReference> references, bool outbound)
        {
            var root = _tree.Nodes.Add($"{title} ({references.Count})");
            foreach (var group in references.GroupBy(r => new ResourceAddress(outbound ? r.Target : r.Document)))
            {
                var parent = root.Nodes.Add(group.Key.ToString());
                parent.ToolTipText = parent.Text;
                foreach (var reference in group)
                {
                    var address = outbound ? new ResourceAddress(reference.Target, reference.Selector)
                        : new ResourceAddress(reference.Document, reference.Member, reference.ReferencePath);
                    var text = $"{address} — {reference.ReferencePath}" +
                        (outbound && !scan.IsWithinScope(reference.Target) ? " [outside inspected scope]" : "");
                    var node = parent.Nodes.Add(text);
                    node.Tag = address;
                    node.ToolTipText = text;
                    if (address == selected) _tree.SelectedNode = node;
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _menu.Dispose(); _tips.Dispose(); }
        base.Dispose(disposing);
    }
}
