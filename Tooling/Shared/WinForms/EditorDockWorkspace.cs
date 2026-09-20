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
    internal DockPanel DockPanel { get; }

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
        return pane;
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
        }

        base.Dispose(disposing);
        if (disposing)
            _theme.Dispose();
    }
}
