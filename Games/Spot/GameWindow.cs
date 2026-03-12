using System;
using System.Drawing;
using System.Windows.Forms;

namespace HWG.Spot;

public partial class GameWindow : Form
{
    private SpotGameHost? _gameHost;
    private static readonly Size DefaultWindowSize = new(769, 769);

    public GameWindow()
    {
        InitializeComponent();
        CreateMenu();

        renderSurface.Dock = DockStyle.Fill;

        // Normal window, centered
        this.FormBorderStyle = FormBorderStyle.Sizable; // or FixedSingle
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ClientSize = DefaultWindowSize;

        this.MinimizeBox = false;
        this.MaximizeBox = false;

        this.KeyPreview = true;
        this.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                _gameHost.Engine.Stop();
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

        this.FormBorderStyle = FormBorderStyle.FixedSingle;
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

    private void CreateMenu()
    {
        var menuStrip = new MenuStrip();
        var gameMenu = new ToolStripMenuItem("Game");
        var newGameMenuItem = new ToolStripMenuItem("New Game", null, (s, e) => OpenNewGameDialog());
        var exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) => this.Close());
        
        gameMenu.DropDownItems.Add(newGameMenuItem);
        gameMenu.DropDownItems.Add(exitMenuItem);
        
        menuStrip.Items.Add(gameMenu);
        
        this.MainMenuStrip = menuStrip;
        this.Controls.Add(menuStrip);
    }

    private void OpenNewGameDialog()
    {
        using (var dialog = new NewGameDialog())
        {
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var options = dialog.Options;
                //_gameHost?.SpotGame.NewGame(options.BoardWidth, options.BoardHeight, options.Players);
            }
        }
    }
}
