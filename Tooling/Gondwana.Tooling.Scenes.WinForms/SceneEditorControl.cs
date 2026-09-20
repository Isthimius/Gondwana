using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Scenes.WinForms;

/// <summary>
/// Hostable WinForms authoring surface for one GSCN scene definition.
/// </summary>
public sealed class SceneEditorControl : UserControl
{
    private readonly TreeView _structure = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill, ToolbarVisible = false, HelpVisible = true };
    private readonly TextBox _validation = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

    public SceneDocument Document { get; }
    public SceneDefinition Definition => Document.Definition;

    public SceneEditorControl(SceneDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Size = new Size(1200, 800);
        Dock = DockStyle.Fill;

        _structure.AfterSelect += (_, _) => BindSelection();
        _properties.PropertyValueChanged += (_, _) =>
        {
            Document.MarkChanged();
            RefreshStructure();
            UpdateValidation();
        };

        var workspace = new EditorDockWorkspace();
        Controls.Add(workspace);
        var dock = workspace.DockPanel;
        var structure = workspace.AddPane("Scene structure", _structure, BuildStructureToolbar());
        var preview = workspace.AddPane("Scene preview", new Label
        {
            Text = "GSCN preview scaffold",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        });
        var properties = workspace.AddPane("Properties", _properties);
        var validation = workspace.AddPane("Validation", _validation);

        preview.Show(dock, DockState.Document);
        structure.Show(preview.Pane, DockAlignment.Left, .24);
        properties.Show(preview.Pane, DockAlignment.Right, .28);
        validation.Show(properties.Pane, DockAlignment.Bottom, .28);

        DarkTheme.Apply(this);
        RefreshStructure();
        UpdateValidation();
    }

    public IReadOnlyList<string> UpdateValidation()
    {
        var errors = Document.Validate();
        _validation.Text = errors.Count == 0
            ? "VALID: GSCN structural validation passed."
            : string.Join(Environment.NewLine, errors.Select(error => "ERROR: " + error));
        return errors;
    }

    public bool CommitEdits()
    {
        _validation.Focus();
        return !_properties.ContainsFocus && ValidateChildren();
    }

    private ToolStrip BuildStructureToolbar()
    {
        var bar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        bar.Items.Add("+ Layer", null, (_, _) =>
        {
            var layer = Document.AddLayer();
            RefreshStructure();
            SelectTag(layer);
        });
        bar.Items.Add("− Layer", null, (_, _) =>
        {
            if (_structure.SelectedNode?.Tag is SceneLayerDefinition layer &&
                Document.RemoveLayer(layer))
            {
                RefreshStructure();
                UpdateValidation();
            }
        });
        return bar;
    }

    private void RefreshStructure()
    {
        object? selected = _structure.SelectedNode?.Tag;
        _structure.BeginUpdate();
        try
        {
            _structure.Nodes.Clear();
            var root = new TreeNode(string.IsNullOrWhiteSpace(Definition.ID) ? "(scene)" : Definition.ID)
            {
                Tag = Definition
            };
            foreach (var layer in Definition.Layers)
            {
                root.Nodes.Add(new TreeNode(
                    $"{(string.IsNullOrWhiteSpace(layer.ID) ? "(layer)" : layer.ID)}  [{layer.Columns}×{layer.Rows}]")
                {
                    Tag = layer
                });
            }
            _structure.Nodes.Add(root);
            root.Expand();
        }
        finally
        {
            _structure.EndUpdate();
        }

        if (selected is not null)
            SelectTag(selected);
        else
            _structure.SelectedNode = _structure.Nodes.Count == 0 ? null : _structure.Nodes[0];
    }

    private void BindSelection() =>
        _properties.SelectedObject = _structure.SelectedNode?.Tag;

    private void SelectTag(object tag)
    {
        foreach (TreeNode root in _structure.Nodes)
        {
            foreach (var node in Enumerate(root))
            {
                if (!ReferenceEquals(node.Tag, tag))
                    continue;
                _structure.SelectedNode = node;
                node.EnsureVisible();
                return;
            }
        }
    }

    private static IEnumerable<TreeNode> Enumerate(TreeNode node)
    {
        yield return node;
        foreach (TreeNode child in node.Nodes)
            foreach (var descendant in Enumerate(child))
                yield return descendant;
    }
}
