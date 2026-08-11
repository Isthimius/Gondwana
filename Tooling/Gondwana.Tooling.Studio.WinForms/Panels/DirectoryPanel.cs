using System.Collections.Specialized;
using Gondwana.Tooling.Studio.ViewModels;

namespace Gondwana.Tooling.Studio.WinForms.Panels;

/// <summary>
/// Panel that shows the directory tree of loaded project resources.
/// When WeifenLuo.WinFormsUI.DockPanel is added, this can be made a DockContent.
/// </summary>
public sealed class DirectoryPanel : UserControl
{
    private readonly DirectoryPanelViewModel _vm;
    private readonly TreeView _treeView;

    /// <summary>
    /// Raised when the user double-clicks (activates) a node in the directory tree.
    /// </summary>
    public event EventHandler<DirectoryNodeViewModel>? NodeActivated;

    /// <summary>
    /// DirectoryPanel.
    /// </summary>
    /// <param name="vm">ViewModel.</param>
    public DirectoryPanel(DirectoryPanelViewModel vm)
    {
        _vm = vm;

        _treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
            ShowLines = true,
            BackColor = System.Drawing.Color.FromArgb(30, 30, 30),
            ForeColor = System.Drawing.Color.FromArgb(220, 220, 220)
        };
        Controls.Add(_treeView);

        _vm.RootNodes.CollectionChanged += OnRootNodesChanged;
        _treeView.NodeMouseDoubleClick += OnNodeDoubleClick;

        RebuildTree();
    }

    private void RebuildTree()
    {
        _treeView.BeginUpdate();
        _treeView.Nodes.Clear();
        foreach (var node in _vm.RootNodes)
        {
            var treeNode = CreateTreeNode(node);
            _treeView.Nodes.Add(treeNode);
        }
        _treeView.ExpandAll();
        _treeView.EndUpdate();
    }

    private static TreeNode CreateTreeNode(DirectoryNodeViewModel nodeVm)
    {
        var treeNode = new TreeNode(nodeVm.DisplayName) { Tag = nodeVm };
        foreach (var child in nodeVm.Children)
            treeNode.Nodes.Add(CreateTreeNode(child));
        nodeVm.Children.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
            {
                foreach (DirectoryNodeViewModel added in e.NewItems)
                    treeNode.Nodes.Add(CreateTreeNode(added));
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is not null)
            {
                foreach (DirectoryNodeViewModel removed in e.OldItems)
                {
                    var toRemove = treeNode.Nodes
                        .Cast<TreeNode>()
                        .FirstOrDefault(n => n.Tag == removed);
                    if (toRemove is not null)
                        treeNode.Nodes.Remove(toRemove);
                }
            }
        };
        return treeNode;
    }

    private void OnRootNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_treeView.InvokeRequired)
        {
            _treeView.BeginInvoke(RebuildTree);
            return;
        }
        RebuildTree();
    }

    private void OnNodeDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node?.Tag is DirectoryNodeViewModel vm)
            NodeActivated?.Invoke(this, vm);
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    /// <param name="disposing">disposing.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _vm.RootNodes.CollectionChanged -= OnRootNodesChanged;
        base.Dispose(disposing);
    }
}
