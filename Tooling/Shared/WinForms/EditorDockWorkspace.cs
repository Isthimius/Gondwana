using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace Gondwana.Tooling.WinForms;

/// <summary>
/// Editor-owned docking surface, source-linked by the standalone tooling projects.
/// Content belongs to this workspace even while hidden. No layout is persisted.
/// </summary>
internal sealed class EditorDockWorkspace : UserControl
{
    private readonly VS2015DarkTheme _theme = new();
    private readonly List<DockContent> _contents = [];
    private readonly Dictionary<string, DockContent> _contentsByTitle =
        new(StringComparer.OrdinalIgnoreCase);

    internal DockPanel DockPanel { get; }
    internal IReadOnlyList<string> PaneTitles =>
        _contents.Select(content => content.Text).ToArray();

    internal EditorDockWorkspace()
    {
        Dock = DockStyle.Fill;
        DockPanel = new DockPanel
        {
            Dock = DockStyle.Fill,
            Theme = _theme,
            DocumentStyle = DocumentStyle.DockingWindow,
            DocumentTabStripLocation = DocumentTabStripLocation.Top
        };
        Controls.Add(DockPanel);
    }

    // The controls are passed in WinForms z-order: fill content, then top toolbars.
    internal DockContent AddPane(string title, params Control[] controls)
    {
        var pane = new DockContent
        {
            Text = title,
            HideOnClose = true,
            // Use document-style split/tab groups throughout the inner workspace.
            // Tool-window states move tabs to the bottom and make a fill-drop
            // look like it replaced the original pane. Document-only contents
            // keep top tabs consistently, and cannot float or auto-hide.
            DockAreas = DockAreas.Document
        };
        pane.Controls.AddRange(controls);
        _contents.Add(pane);
        _contentsByTitle.Add(title, pane);
        return pane;
    }

    internal bool ShowPane(string title)
    {
        if (!_contentsByTitle.TryGetValue(title, out var pane) ||
            pane.IsDisposed)
        {
            return false;
        }

        pane.Show();
        pane.Activate();
        return true;
    }

    internal bool IsPaneVisible(string title) =>
        _contentsByTitle.TryGetValue(title, out var pane) &&
        !pane.IsDisposed &&
        !pane.IsHidden;

    internal void ShowAllPanes()
    {
        foreach (var pane in _contents)
        {
            if (!pane.IsDisposed)
                pane.Show();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Hidden contents need explicit ownership too; do not rely on the
            // visible control hierarchy or DockPanel's pane disposal behavior.
            foreach (var content in _contents)
                content.Dispose();
            _contents.Clear();
            _contentsByTitle.Clear();
        }

        base.Dispose(disposing);
        if (disposing)
            _theme.Dispose();
    }
}
