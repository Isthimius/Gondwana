using Gondwana;
using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace HWG.Spot;

internal partial class GameWindow : Form
{
    private SpotGameHost _gameHost;
    private static readonly Size DefaultWindowSize = new(769, 769);
    private MenuStrip _menuStrip;

    internal GameWindow()
    {
        InitializeComponent();
        CreateMenu();

        renderSurface.Dock = DockStyle.Fill;

        // Normal window, centered
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ClientSize = DefaultWindowSize;

        this.MinimizeBox = false;
        this.MaximizeBox = false;
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

        // resize client area to include the menu strip
        this.ClientSize = new Size(DefaultWindowSize.Width, DefaultWindowSize.Height + _menuStrip.Height);

        _gameHost!.Initialize(logLevel: LogLevel.Warning);    // this calls Engine.Initialize + Start(SynchronizationContext.Current!)

        _gameHost.Engine.CPSCalculated += (cps) =>
        {
            Engine.Logger.LogTrace("{CPS}", cps.ToString());
        };
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Clean shutdown
        _gameHost?.Dispose();
        _gameHost = null;

        base.OnFormClosed(e);
    }

    private ToolStripMenuItem? _musicMenuItem;
    private ToolStripMenuItem? _soundEffectsMenuItem;
    private ToolStripMenuItem? _jiggleMenuItem;
    private ToolStripMenuItem? _cloudsMenuItem;

    private void CreateMenu()
    {
        _menuStrip = new MenuStrip();

        var gameMenu = new ToolStripMenuItem("Game");
        var newGameMenuItem = new ToolStripMenuItem("New Game", null, (s, e) => OpenNewGameDialog());
        var exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) => Close());

        gameMenu.DropDownItems.Add(newGameMenuItem);
        gameMenu.DropDownItems.Add(new ToolStripSeparator());
        gameMenu.DropDownItems.Add(exitMenuItem);

        var optionsMenu = new ToolStripMenuItem("Options");

        _musicMenuItem = new ToolStripMenuItem("Music")
        {
            CheckOnClick = true,
            Checked = true
        };
        _musicMenuItem.CheckedChanged += MusicMenuItem_CheckedChanged;

        _soundEffectsMenuItem = new ToolStripMenuItem("Sound Effects")
        {
            CheckOnClick = true,
            Checked = true
        };
        _soundEffectsMenuItem.CheckedChanged += SoundEffectsMenuItem_CheckedChanged;

        _jiggleMenuItem = new ToolStripMenuItem("Jiggle")
        {
            CheckOnClick = true,
            Checked = true
        };
        _jiggleMenuItem.CheckedChanged += JiggleMenuItem_CheckedChanged;

        _cloudsMenuItem = new ToolStripMenuItem("Clouds")
        {
            CheckOnClick = true,
            Checked = true
        };
        _cloudsMenuItem.CheckedChanged += CloudsMenuItem_CheckedChanged;

        optionsMenu.DropDownItems.Add(_musicMenuItem);
        optionsMenu.DropDownItems.Add(_soundEffectsMenuItem);
        optionsMenu.DropDownItems.Add(_jiggleMenuItem);
        optionsMenu.DropDownItems.Add(_cloudsMenuItem);

        _menuStrip.Items.Add(gameMenu);
        _menuStrip.Items.Add(optionsMenu);

        MainMenuStrip = _menuStrip;
        Controls.Add(_menuStrip);
    }

    private void MusicMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        _gameHost.Engine.EngineDispatcher.Post(() => _gameHost.SetMusicEnabled(_musicMenuItem!.Checked));
    }

    private void SoundEffectsMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        _gameHost.Engine.EngineDispatcher.Post(() => _gameHost.SetSoundEffectsEnabled(_soundEffectsMenuItem!.Checked));
    }

    private void JiggleMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        _gameHost.Engine.EngineDispatcher.Post(() => _gameHost.SetJiggleEnabled(_jiggleMenuItem!.Checked));
    }

    private void CloudsMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        _gameHost.Engine.EngineDispatcher.Post(() => _gameHost.SetCloudsEnabled(_cloudsMenuItem!.Checked));
    }

    private void OpenNewGameDialog()
    {
        using var dialog = new NewGameDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var options = dialog.Options;
            _gameHost.Engine.EngineDispatcher.Post(() => _gameHost.StartNewGame(options));
        }
    }
}
