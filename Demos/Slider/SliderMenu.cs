using System.Diagnostics;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Dialogs;
using Gondwana.Widgets.Menus;
using SkiaSharp;
using View = Gondwana.Rendering.Views.View;

namespace Gondwana.Demos.Slider;

/// <summary>Persistent in-engine Game / Scores / Help menus and modal Widgets dialogs.</summary>
internal sealed class SliderMenu : IDisposable
{
    private readonly RenderSurfaceHostBase _host;
    private readonly View _gameView;
    private readonly MenuBarWidget _menu;
    private readonly SKImage? _gondwanaLogo;
    private DialogBox? _dialog;

    internal bool HasOpenDialog => _dialog is { IsClosed: false };
    internal bool HasOpenMenu => _menu.OpenMenuIndex >= 0;

    internal SliderMenu(RenderSurfaceHostBase host, View uiView, View gameView,
        Action newPuzzle, Action openPicture, Action shuffle,
        Action save, Action load, Action highScores, Action exit)
    {
        _host = host;
        _gameView = gameView;
        string logoPath = Path.Combine(AppContext.BaseDirectory, "assets", "gondwana-logo-text.png");
        using (SKBitmap? logoBitmap = File.Exists(logoPath) ? SKBitmap.Decode(logoPath) : null)
            _gondwanaLogo = logoBitmap is null ? null : SKImage.FromBitmap(logoBitmap);

        _menu = new MenuBarWidget(host, uiView, new Rectangle(0, 0, SliderSidebar.Width, 32));
        _menu
            .AddMenu("Game", game => game
                .AddItem("New Puzzle", newPuzzle, mnemonic: 'N')
                .AddItem("Open Picture...", openPicture, mnemonic: 'O')
                .AddItem("Shuffle", shuffle, mnemonic: 'U')
                .AddSeparator()
                .AddItem("Save Puzzle...", save, mnemonic: 'S')
                .AddItem("Load Puzzle...", load, mnemonic: 'L')
                .AddSeparator()
                .AddItem("Exit", exit, mnemonic: 'X'), mnemonic: 'G')
            .AddMenu("Scores", scores => scores
                .AddItem("High Scores", highScores, mnemonic: 'H'), mnemonic: 'S')
            .AddMenu("Help", help => help
                .AddItem("How to Play", ShowHowToPlay, mnemonic: 'P')
                .AddSeparator()
                .AddItem("About", ShowAbout, mnemonic: 'A'), mnemonic: 'H');
        _menu.Show();
    }

    internal void ShowHighScores(IReadOnlyList<SliderScore> scores)
        => ShowText("High Scores", SliderPersistence.FormatScores(scores));

    private void ShowHowToPlay() => ShowText("How to Play", """
        SLIDER — HOW TO PLAY

        Make the picture whole again by moving tiles into the blank square.

        Click or tap a tile next to the empty square, or use the arrow keys.
        You may slide only one adjacent tile at a time.

        GAME MENU
        New Puzzle — reset using the selected picture and dimensions.
        Open Picture — choose your own picture.
        Shuffle — animate legal moves to produce a solvable board.
        Save Puzzle — save the current board, picture, time, and moves.
        Load Puzzle — resume a previously saved game.

        SCORES MENU
        High Scores — best completed times and move counts, by board size.
        A new puzzle or shuffle resets the current attempt's timer and moves.

        HOLD SHIFT
        Show each tile's correct solved column,row (starting from 0,0).
        Labels follow the pieces as they move and vanish when Shift is released.

        SOLVING
        Finish every tile in its original location to record a high score.
        The sidebar shows elapsed time and moves as you play.
        """);

    private Rectangle DialogBounds(int width, int height)
    {
        Rectangle area = _gameView.Viewport.TargetRectPx;
        width = Math.Min(width, Math.Max(240, area.Width - 16));
        height = Math.Min(height, Math.Max(180, area.Height - 16));
        return new Rectangle(area.Left + (area.Width - width) / 2,
            area.Top + (area.Height - height) / 2, width, height);
    }

    private void ShowText(string title, string content)
    {
        if (HasOpenDialog)
        {
            _dialog!.Activate();
            return;
        }

        Rectangle bounds = DialogBounds(530, 530);
        var dialog = new DialogBox(_host, _gameView, bounds, title,
            showCloseButton: true, nickname: "slider.dialog");
        dialog.Panel.SetColor(Color.FromArgb(255, 24, 30, 42))
            .SetBorderColor(Color.FromArgb(255, 95, 110, 135));
        dialog.TitleBar.SetColor(Color.FromArgb(255, 46, 63, 85));

        var labelBounds = new Rectangle(bounds.X + 18, bounds.Y + 48,
            bounds.Width - 36, bounds.Height - 68);
        var text = new LabelWidget(_host, _gameView, labelBounds, content,
                "slider.dialog.content")
            .SetFont(SKTypeface.Default, 16f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Top)
            .EnableWrapping()
            .SetPadding(5f, 5f)
            .SetLabelZOrder(10_020);
        text.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        text.MouseWheelScrollPixels = 52;
        dialog.Add(text, keepCurrentOffset: false, explicitLocalOffsetPx: new Vector2(18, 48));
        OpenDialog(dialog);
    }

    private void ShowAbout()
    {
        if (HasOpenDialog)
        {
            _dialog!.Activate();
            return;
        }

        var about = new AboutBox(_host, _gameView,
            applicationName: "Slider",
            version: "Gondwana Demo",
            description: "A sliding-picture puzzle built with the Gondwana Game Engine.",
            logo: _gondwanaLogo,
            bounds: DialogBounds(480, 360),
            nickname: "slider.about",
            uriLauncher: new SliderUriLauncher(),
            hyperlinkUri: new Uri("https://github.com/Isthimius/Gondwana"),
            hyperlinkText: "View Gondwana on GitHub");
        about.Panel.SetColor(Color.FromArgb(255, 23, 30, 42))
            .SetBorderColor(Color.FromArgb(255, 95, 110, 135));
        about.TitleBar.SetColor(Color.FromArgb(255, 46, 63, 85));
        OpenDialog(about);
    }

    private void OpenDialog(DialogBox dialog)
    {
        _dialog = dialog;
        dialog.Closed += _ => _dialog = null;
        dialog.Show();
        dialog.Activate();
    }

    private sealed class SliderUriLauncher : IExternalUriLauncher
    {
        public ValueTask OpenAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
            return ValueTask.CompletedTask;
        }
    }

    public void Dispose()
    {
        _dialog?.Dispose();
        _dialog = null;
        _menu.Dispose();
        _gondwanaLogo?.Dispose();
    }
}
