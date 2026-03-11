using System;
using System.Drawing;
using System.Windows.Forms;
using Gondwana;

namespace HWG.Spot;

public partial class GameWindow : Form
{
    private SpotGameHost? _gameHost;
    private static readonly Size DefaultWindowSize = new(769, 769);

    public GameWindow()
    {
        InitializeComponent();

        renderSurface.Dock = DockStyle.Fill;

        // Normal window, centered
        this.FormBorderStyle = FormBorderStyle.Sizable; // or FixedSingle
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ClientSize = DefaultWindowSize;

        this.KeyPreview = true;
        this.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Engine.Instance.Stop();
                this.Close();
            }
        };
    }

    // create the Game (and thereby start the engine) once the form & controls are ready
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _gameHost = new SpotGameHost(renderSurface);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ClientSize = DefaultWindowSize;

        _gameHost!.Initialize();    // this calls Engine.Initialize + Start(SynchronizationContext.Current!)
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Clean shutdown
        _gameHost?.Dispose();
        _gameHost = null;

        base.OnFormClosed(e);
    }
}
