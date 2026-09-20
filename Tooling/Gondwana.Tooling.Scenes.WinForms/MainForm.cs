using Gondwana.Tooling.Scenes.Editing;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace Gondwana.Tooling.Scenes.WinForms;

internal sealed class MainForm : Form
{
    private readonly VS2015DarkTheme _theme = new();
    private readonly DockPanel _dock;

    public MainForm()
    {
        Text = "Gondwana Scenes — GSCN editor";
        Size = new Size(1400, 900);
        StartPosition = FormStartPosition.CenterScreen;

        _dock = new DockPanel
        {
            Dock = DockStyle.Fill,
            Theme = _theme,
            DocumentStyle = DocumentStyle.DockingWindow
        };
        Controls.Add(_dock);

        Shown += (_, _) =>
        {
            var document = SceneDocument.Create(Environment.CurrentDirectory);
            var content = new DockContent { Text = "Untitled.gscn *" };
            content.Controls.Add(new SceneEditorControl(document));
            content.Show(_dock, DockState.Document);
        };

        DarkTheme.Apply(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dock.Dispose();
            _theme.Dispose();
        }
        base.Dispose(disposing);
    }
}
