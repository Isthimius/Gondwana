using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Gondwana.Demos.Spot.Game;
using Gondwana.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Direct.Particles;
using Gondwana.Drawing.Tilesheets;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameHost
{
    private bool _startupPresentationShown;

    internal void BeginPostSplashStartup()
    {
        if (_startupPresentationShown)
            return;

        _startupPresentationShown = true;

        if (TilesheetRegistry.Instance.TryGet("splash", out Tilesheet? tilesheet) &&
            tilesheet is not null)
        {
            var directImage = new DirectImage(
                tilesheet.SkBitmap,
                SurfaceHost,
                ActiveScene[0],
                new Rectangle(0, 0, 769, 769));

            directImage.ZOrder = 100;
            directImage.SetScaleMode(DirectImage.ScaleMode.Fit);
        }

        var particleSurface = new ParticleSurface(
            SurfaceHost,
            ActiveScene[0],
            new Rectangle(0, 0, 769, 769));

        particleSurface.CullingMarginX = 1300f;
        particleSurface.ZOrder = 50;
        particleSurface.Emitters.Add(GetSpots(769, 769));

        if (MusicEnabled)
        {
            _music.Volume = 0.2f;
            if (!_music.IsPlaying)
                _music.Play();
        }
    }

    private void ClearGamePresentation()
    {
        // Preserve persistent view-space UI such as the MenuBarWidget. Only Spot's
        // scene-layer presentation and tracked game HUD drawings belong to a game reset.
        _particleSurface = null;

        var sceneDrawings = Engine.Managers.DirectDrawings.DirectDrawings
            .Where(drawing => drawing.Mode == DirectDrawingMode.SceneLayer)
            .ToArray();

        foreach (var drawing in sceneDrawings)
            drawing.Dispose();

        _player1Text?.Dispose();
        _player1Text = null;
        _player1Rectangle?.Dispose();
        _player1Rectangle = null;

        _player2Text?.Dispose();
        _player2Text = null;
        _player2Rectangle?.Dispose();
        _player2Rectangle = null;

        _player3Text?.Dispose();
        _player3Text = null;
        _player3Rectangle?.Dispose();
        _player3Rectangle = null;

        _player4Text?.Dispose();
        _player4Text = null;
        _player4Rectangle?.Dispose();
        _player4Rectangle = null;

        _gameMessageText?.Dispose();
        _gameMessageText = null;
        _gameMessageRectangle?.Dispose();
        _gameMessageRectangle = null;
    }

    private void SetPlayerFrames(List<Player> players)
    {
        foreach (var player in players)
        {
            switch (player.ColorItem.Name)
            {
                case "Blue":
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 0);
                    player.ActiveFrame = new Frame(_spotSheetSelected, 0, 0);
                    break;
                case "Green":
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 1);
                    player.ActiveFrame = new Frame(_spotSheetSelected, 1, 0);
                    break;
                case "Violet":
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 2);
                    player.ActiveFrame = new Frame(_spotSheetSelected, 2, 0);
                    break;
                case "Red":
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 3);
                    player.ActiveFrame = new Frame(_spotSheetSelected, 3, 0);
                    break;
                case "Yellow":
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 4);
                    player.ActiveFrame = new Frame(_spotSheetSelected, 4, 0);
                    break;
                default:
                    break;
            }
        }
    }

    private void StartPlayerJiggle(Player player)
    {
        if (JiggleEnabled)
        {
            foreach (var cell in SpotGame.SpotGameField.GetAllCellsForPlayer(player))
            {
                cell.Sprite?.StartJiggle(loop: true);
            }
        }
    }

    private void StopPlayerJiggle(Player player)
    {
        foreach (var cell in SpotGame.SpotGameField.GetAllCellsForPlayer(player))
        {
            cell.Sprite?.StopJiggle();
        }
    }

    private void JiggleAllPlayers()
    {
        foreach (var player in SpotGame.Players)
        {
            StartPlayerJiggle(player);
        }
    }
}
