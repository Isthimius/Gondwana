namespace Gondwana.CoordinateTest;

public partial class GameWindow : Form
{
    private Game? _game;

    public GameWindow()
    {
        InitializeComponent();

        // Fill the whole form
        renderSurface.Dock = DockStyle.Fill;

        // Borderless + maximized, but still respect the taskbar
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual; // we’ll set bounds ourselves in Shown
        this.MaximizeBox = true;

        // Optional: allow ESC to close (handy for borderless windows while testing)
        this.KeyPreview = true;
        this.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) this.Close();
        };
    }

    // Create the Game (and thereby start the engine) once the form & controls are ready
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _game = new Game(renderSurface);   // this calls Engine.Initialize + Start(SynchronizationContext.Current!)
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // Respect the desktop working area (won’t cover taskbar)
        var screen = Screen.FromHandle(this.Handle);

        // Fill the entire display, ignoring the taskbar
        this.Bounds = screen.Bounds;

        // Optional but good practice for a “real” full screen feel
        this.FormBorderStyle = FormBorderStyle.None;
        this.WindowState = FormWindowState.Normal; // force apply bounds first
        this.WindowState = FormWindowState.Maximized;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Clean shutdown
        _game?.Dispose();
        _game = null;

        base.OnFormClosed(e);
    }
}
