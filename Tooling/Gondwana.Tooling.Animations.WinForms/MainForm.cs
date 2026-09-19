using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace Gondwana.Tooling.Animations.WinForms;

internal sealed class MainForm : Form
{
    private readonly VS2015DarkTheme _theme = new();
    private readonly DockPanel _dock;

    public MainForm()
    {
        Text = "Gondwana Animations — GANI editor";
        Size = new Size(1450, 900);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        _dock = new DockPanel
        {
            Dock = DockStyle.Fill,
            Theme = _theme,
            DocumentStyle = DocumentStyle.DockingWindow
        };

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        Add(file, "&New", Keys.Control | Keys.N, NewDocument);
        Add(file, "E&xit", Keys.Alt | Keys.F4, Close);
        menu.Items.Add(file);

        MainMenuStrip = menu;
        Controls.Add(_dock);
        Controls.Add(menu);

        DarkTheme.Apply(this);

        Shown += (_, _) => NewDocument();
    }

    private static void Add(
        ToolStripMenuItem menu,
        string label,
        Keys shortcut,
        Action action)
    {
        var item = new ToolStripMenuItem(label)
        {
            ShortcutKeys = shortcut,
            ForeColor = DarkTheme.Foreground
        };

        item.Click += (_, _) => action();
        menu.DropDownItems.Add(item);
    }

    private void NewDocument()
    {
        var document = new AnimationEditorDocument();
        document.Show(_dock, DockState.Document);
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
