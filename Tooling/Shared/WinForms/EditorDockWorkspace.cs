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
            DocumentStyle = DocumentStyle.DockingWindow
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
            DockAreas = DockAreas.Document | DockAreas.DockLeft | DockAreas.DockRight |
                DockAreas.DockTop | DockAreas.DockBottom
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
