using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Gondwana.Demos.Spot.Game;
using Gondwana.Drawing.Direct;
using Gondwana.SkiaSharp;
using SkiaSharp;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameHost
{
    private bool _showScores = true;

    private TextBlock? _player1Text;
    private DirectRectangle? _player1Rectangle;
    private TextBlock? _player2Text;
    private DirectRectangle? _player2Rectangle;
    private TextBlock? _player3Text;
    private DirectRectangle? _player3Rectangle;
    private TextBlock? _player4Text;
    private DirectRectangle? _player4Rectangle;
    private TextBlock? _gameMessageText;
    private DirectRectangle? _gameMessageRectangle;

    private void CreateTextBlockFields()
    {
        // upper left
        _player1Text = new TextBlock(SurfaceHost,
                                     SurfaceHost.ViewManager.Views[0],
                                     new Rectangle(10, PersistentMenuHeight + 10, 200, 50));
        _player1Text.SetFont(_font, 24, 12)
                    .SetColors(SpotGame.Players[0].ColorItem.TextColor, SKColors.Transparent)
                    .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                    .SetText(SpotGame.Players[0].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[0]))
                    .SetMaxLines(1)
                    .UseShadow()
                    .SetShadow(3, 3, 200, 3.0f);
        _player1Text.ZOrder = 20;

        _player1Rectangle = new DirectRectangle(SpotGame.Players[0].ColorItem.Color.ToColor(),
                                                SurfaceHost,
                                                SurfaceHost.ViewManager.Views[0],
                                                _player1Text.ScreenBounds);
        _player1Rectangle.SetCornerRadius(30)
                         .SetFilled(true);

        // bottom right
        _player2Text = new TextBlock(SurfaceHost,
                                     SurfaceHost.ViewManager.Views[0],
                                     new Rectangle(SurfaceWidth - 210, SurfaceHeight - 60, 200, 50));
        _player2Text.SetFont(_font, 24, 12)
                    .SetColors(SpotGame.Players[1].ColorItem.TextColor, SKColors.Transparent)
                    .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                    .SetText(SpotGame.Players[1].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[1]))
                    .SetMaxLines(1)
                    .UseShadow()
                    .SetShadow(3, 3, 200, 3.0f);
        _player2Text.ZOrder = 20;

        _player2Rectangle = new DirectRectangle(SpotGame.Players[1].ColorItem.Color.ToColor(),
                                                SurfaceHost,
                                                SurfaceHost.ViewManager.Views[0],
                                                _player2Text.ScreenBounds);
        _player2Rectangle.SetCornerRadius(30)
                         .SetFilled(true);

        if (SpotGame.Players.Length >= 3)
        {
            // upper right
            _player3Text = new TextBlock(SurfaceHost,
                                         SurfaceHost.ViewManager.Views[0],
                                         new Rectangle(SurfaceWidth - 210, PersistentMenuHeight + 10, 200, 50));
            _player3Text.SetFont(_font, 24, 12)
                        .SetColors(SpotGame.Players[2].ColorItem.TextColor, SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .SetText(SpotGame.Players[2].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[2]))
                        .SetMaxLines(1)
                        .UseShadow()
                        .SetShadow(3, 3, 200, 3.0f);
            _player3Text.ZOrder = 20;

            _player3Rectangle = new DirectRectangle(SpotGame.Players[2].ColorItem.Color.ToColor(),
                                                    SurfaceHost,
                                                    SurfaceHost.ViewManager.Views[0],
                                                    _player3Text.ScreenBounds);
            _player3Rectangle.SetCornerRadius(30)
                             .SetFilled(true);
        }

        if (SpotGame.Players.Length >= 4)
        {
            // bottom left
            _player4Text = new TextBlock(SurfaceHost,
                                         SurfaceHost.ViewManager.Views[0],
                                         new Rectangle(10, SurfaceHeight - 60, 200, 50));
            _player4Text.SetFont(_font, 24, 12)
                        .SetColors(SpotGame.Players[3].ColorItem.TextColor, SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .SetText(SpotGame.Players[3].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[3]))
                        .SetMaxLines(1)
                        .UseShadow()
                        .SetShadow(3, 3, 200, 3.0f);

            _player4Text.ZOrder = 20;

            _player4Rectangle = new DirectRectangle(SpotGame.Players[3].ColorItem.Color.ToColor(),
                                                    SurfaceHost,
                                                    SurfaceHost.ViewManager.Views[0],
                                                    _player4Text.ScreenBounds);
            _player4Rectangle.SetCornerRadius(30)
                             .SetFilled(true);
        }

        if (SpotGame.SpotGameField.GridRowCount > 10)
        {
            SetScoreVisible(false);
        }
    }

    private void SetScoreVisible(bool visible)
    {
        _showScores = visible;

        if (_player1Text is not null)
        {
            _player1Text.Visible = visible;
            _player1Rectangle!.Visible = visible;
        }

        if (_player2Text is not null)
        {
            _player2Text.Visible = visible;
            _player2Rectangle!.Visible = visible;
        }

        if (_player3Text is not null)
        {
            _player3Text.Visible = visible;
            _player3Rectangle!.Visible = visible;
        }

        if (_player4Text is not null)
        {
            _player4Text.Visible = visible;
            _player4Rectangle!.Visible = visible;
        }

        if (visible)
            SetPlayerScores();
    }

    private void SetPlayerScores()
    {
        if (_player1Text is not null)
            _player1Text.SetText(SpotGame.Players[0].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[0]));

        if (_player2Text is not null)
            _player2Text.SetText(SpotGame.Players[1].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[1]));

        if (SpotGame.Players.Length >= 3)
            _player3Text?.SetText(SpotGame.Players[2].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[2]));

        if (SpotGame.Players.Length >= 4)
            _player4Text?.SetText(SpotGame.Players[3].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[3]));
    }

    private void CreateGameOverText(List<Player> winningPlayers)
    {
        bool multipleWinners;
        string message;

        Color primaryTextColor = winningPlayers[0].ColorItem.TextColor.ToColor();
        Color? secondaryTextColor = null;
        Color primaryFillColor = winningPlayers[0].ColorItem.Color.ToColor();
        Color? secondaryFillColor = null;

        if (winningPlayers.Count == 1)
        {
            multipleWinners = false;
            message = winningPlayers[0].Name + " wins!";
        }
        else
        {
            multipleWinners = true;

            var names = winningPlayers.Select(p => p.Name).ToList();
            var formatted = names.Count == 2
                ? string.Join(" and ", names)
                : string.Join(", ", names.Take(names.Count - 1)) + $", and {names.Last()}";

            message = $"{formatted} tie!";

            secondaryTextColor = winningPlayers[1].ColorItem.TextColor.ToColor();
            secondaryFillColor = winningPlayers[1].ColorItem.Color.ToColor();
        }

        _gameMessageText = new TextBlock(SurfaceHost,
                                         SurfaceHost.ViewManager.Views[0],
                                         new Rectangle(
                                             SurfaceWidth / 2 - 180,
                                             PersistentMenuHeight + ((SurfaceHeight - PersistentMenuHeight) / 2) - 40,
                                             360,
                                             80));
        _gameMessageText.SetFont(_font, 48, 16)
                        .SetColors(primaryTextColor.ToSKColor(), SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .SetText(message)
                        .UseShadow()
                        .SetShadow(5, 5, 200, 3.0f)
                        .EnableWrapping();

        _gameMessageText.ZOrder = 20;

        _gameMessageRectangle = new DirectRectangle(primaryFillColor,
                                                    SurfaceHost,
                                                    SurfaceHost.ViewManager.Views[0],
                                                    _gameMessageText.ScreenBounds);
        _gameMessageRectangle.SetCornerRadius(40)
                             .SetFilled(true)
                             .SetColor(primaryFillColor)
                             .SetBorderColor(primaryTextColor)
                             .SetStrokeWidth(2f)
                             .SetStrokeAlign(DirectRectangle.StrokeAlign.Outside);

        if (multipleWinners)
        {
            _gameMessageText.PulseColor(primaryTextColor, secondaryTextColor!.Value, 1.75f);
            _gameMessageRectangle.PulseFill(primaryFillColor, secondaryFillColor!.Value, 1.25f);
            _gameMessageRectangle.PulseBorder(primaryTextColor, secondaryTextColor.Value, 0.75f);
        }
    }
}
