using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Gondwana.Demos.Spot.Game;
using Gondwana.Timers;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameHost
{
    private Gondwana.Timers.Timer? _pendingComputerSelectTimer;
    private Gondwana.Timers.Timer? _pendingComputerMoveTimer;

    internal SpotGame SpotGame { get; private set; } = null!;

    public bool MusicEnabled { get; private set; } = true;
    public bool SoundEffectsEnabled { get; private set; } = true;
    public bool JiggleEnabled { get; private set; } = true;
    public bool CloudsEnabled { get; private set; } = true;

    internal void SetMusicEnabled(bool enabled)
    {
        MusicEnabled = enabled;

        if (enabled)
        {
            if (!_music.IsPlaying)
                _music.Play();
        }
        else
        {
            _music.Stop();
        }
    }

    internal void SetSoundEffectsEnabled(bool enabled)
    {
        SoundEffectsEnabled = enabled;
    }

    internal void SetJiggleEnabled(bool enabled)
    {
        JiggleEnabled = enabled;
        if (!enabled)
        {
            foreach (var player in SpotGame.Players)
            {
                StopPlayerJiggle(player);
            }
        }
    }

    internal void SetCloudsEnabled(bool enabled)
    {
        CloudsEnabled = enabled;

        if (enabled)
        {
            DisposeParticleSurface();
            AddClouds();
        }
        else
        {
            DisposeParticleSurface();
        }
    }

    internal void StartNewGame(NewGameOptions options)
    {
        _pendingComputerSelectTimer?.Dispose();
        _pendingComputerSelectTimer = null;
        _pendingComputerMoveTimer?.Dispose();
        _pendingComputerMoveTimer = null;

        ClearGamePresentation();
        Engine.Managers.Sprites.Clear();
        ActiveScene.RemoveAllLayers();

        SetPlayerFrames(options.Players);

        var newGameResult = SpotGame.NewGame(options.BoardWidth, options.BoardHeight, options.Players.ToArray());

        newGameResult.Field.OriginPx = new Point(
            newGameResult.Field.OriginPx.X,
            newGameResult.Field.OriginPx.Y - PersistentMenuHeight);
        newGameResult.BackgroundField.OriginPx = new Point(
            newGameResult.BackgroundField.OriginPx.X,
            newGameResult.BackgroundField.OriginPx.Y - PersistentMenuHeight);

        ActiveScene.AddLayer(newGameResult.Field);
        ActiveScene.AddLayer(newGameResult.BackgroundField);
        _music.Volume = 0.1f;

        CreateTextBlockFields();
    }

    private void HookSpotGameEvents()
    {
        if (SpotGame is null)
            return;

        SpotGame.GameStarted += OnGameStarted;
        SpotGame.PlayerTurnStarted += OnPlayerTurnStarted;
        SpotGame.PlayerTurnEnded += OnPlayerTurnEnded;
        SpotGame.SpotSelected += OnSpotSelected;
        SpotGame.SpotDeselected += OnSpotDeselected;
        SpotGame.InvalidSelectionAttempted += OnInvalidSelectionAttempted;
        SpotGame.InvalidMoveAttempted += OnInvalidMoveAttempted;
        SpotGame.PlayerMoveStarted += OnPlayerMoveStarted;
        SpotGame.PlayerMoveStopped += OnPlayerMoveStopped;
        SpotGame.CellsCaptured += OnCellsCaptured;
        SpotGame.NoValidMovesAvailable += OnNoValidMovesAvailable;
        SpotGame.GameOver += OnGameOver;
    }

    private void UnhookSpotGameEvents()
    {
        if (SpotGame is null)
            return;

        SpotGame.GameStarted -= OnGameStarted;
        SpotGame.PlayerTurnStarted -= OnPlayerTurnStarted;
        SpotGame.PlayerTurnEnded -= OnPlayerTurnEnded;
        SpotGame.SpotSelected -= OnSpotSelected;
        SpotGame.SpotDeselected -= OnSpotDeselected;
        SpotGame.InvalidSelectionAttempted -= OnInvalidSelectionAttempted;
        SpotGame.InvalidMoveAttempted -= OnInvalidMoveAttempted;
        SpotGame.PlayerMoveStarted -= OnPlayerMoveStarted;
        SpotGame.PlayerMoveStopped -= OnPlayerMoveStopped;
        SpotGame.CellsCaptured -= OnCellsCaptured;
        SpotGame.NoValidMovesAvailable -= OnNoValidMovesAvailable;
        SpotGame.GameOver -= OnGameOver;
    }

    private void OnGameStarted(SpotGame game)
    {
        Engine.Logger.LogDebug("Game started with players: {0}", string.Join(", ", game.Players.Select(p => p.Name)));

        if (MusicEnabled && !_music.IsPlaying)
            _music.Play();

        if (CloudsEnabled)
            AddClouds();
    }

    private void OnPlayerTurnStarted(Player player)
    {
        Engine.Logger.LogDebug("Player {0}'s turn started", player.Name);
        StartPlayerJiggle(player);

        if (player.Type == PlayerType.Human)
        {
            _handleHumanInput = true;
        }
        else
        {
            _handleHumanInput = false;

            // start a short timer before computer moves
            _pendingComputerSelectTimer = Gondwana.Timers.Timer.Add(TimerType.PostCycle, TimerCycles.Once, 0.6);
            _pendingComputerSelectTimer.Tick += () =>
            {
                _pendingComputerSelectTimer = null;

                var moves = SpotGame.SpotGameField.GetBestMovesForPlayer(player);
                if (moves.Count == 0)
                    return;

                var bestMove = moves[Random.Shared.Next(moves.Count)];

                SpotGame.AttemptSelectCell(bestMove.FromCell, out _);

                // small delay before executing move to allow for selection animation
                _pendingComputerMoveTimer = Gondwana.Timers.Timer.Add(TimerType.PostCycle, TimerCycles.Once, 0.6);
                _pendingComputerMoveTimer.Tick += () =>
                {
                    _pendingComputerMoveTimer = null;
                    SpotGame.ExecuteMove(bestMove);
                };
            };
        }
    }

    private void OnPlayerTurnEnded(Player player)
    {
        Engine.Logger.LogDebug("Player {0}'s turn ended", player.Name);
        StopPlayerJiggle(player);
    }

    private void OnSpotSelected(SpotGameField.Cell cell)
    {
        Engine.Logger.LogDebug("Cell at ({0}, {1}) selected by player {2}", cell.X, cell.Y, cell.OccupiedBy!.Name);

        if (SoundEffectsEnabled && SpotGame.CurrentPlayer.Type == PlayerType.Human)
            _spotSelected?.Play();

        var sprite = cell.Sprite!;
        sprite.StopJiggle();
        sprite.CurrentFrame = cell.OccupiedBy.ActiveFrame;
        sprite.PulseBy(1.1f, 0.4f, 0.4f, true);
    }

    private void OnSpotDeselected(SpotGameField.Cell cell)
    {
        Engine.Logger.LogDebug("Cell at ({0}, {1}) deselected", cell.X, cell.Y);

        if (SoundEffectsEnabled)
            _spotDeselected?.Play();

        var sprite = cell.Sprite!;
        sprite.StartJiggle(loop: true);
        sprite.CurrentFrame = cell.OccupiedBy!.DefaultFrame;
        sprite.StopPulse(true, 0.2f);
    }

    private void OnInvalidSelectionAttempted(SpotGameField.Cell cell)
    {
        Engine.Logger.LogDebug("Invalid selection attempted at cell ({0}, {1})", cell.X, cell.Y);

        if (SoundEffectsEnabled)
            _bump?.Play();
    }

    private void OnInvalidMoveAttempted(SpotGameField.Cell cell)
    {
        Engine.Logger.LogDebug("Invalid move attempted to cell ({0}, {1})", cell.X, cell.Y);

        if (SoundEffectsEnabled)
            _knock?.Play();
    }

    private void OnPlayerMoveStarted(PlayerMovement movement)
    {
        if (movement.MovementType == MovementType.Jump && SoundEffectsEnabled)
            _velcro?.Play();
    }

    private void OnPlayerMoveStopped(PlayerMovement movement)
    {
        Engine.Logger.LogDebug("Player {0} performed a {1} move from ({2}, {3}) to ({4}, {5})",
            movement.Player.Name,
            movement.MovementType,
            movement.FromX, movement.FromY,
            movement.DestX, movement.DestY);

        if (SoundEffectsEnabled)
            _drop?.Play();

        if (_showScores)
            SetPlayerScores();

        SpotGame.NextPlayer();
    }

    private void OnCellsCaptured(List<SpotGameField.Cell> cellsCaptured)
    {
        Engine.Logger.LogDebug("{0} cells captured", cellsCaptured.Count);

        foreach (var cell in cellsCaptured)
        {
            var oldSprite = cell.Sprite;
            if (oldSprite == null)
                continue;

            Action? handler = null;
            handler = () =>
            {
                oldSprite.ResizeComplete -= handler;
                oldSprite.CurrentFrame = cell.OccupiedBy!.DefaultFrame;
                oldSprite.ResizeTo(new(56, 56), 0.2f);
            };

            oldSprite.ResizeComplete += handler;
            oldSprite.ResizeTo(new(1, 1), 0.2f);
        }
    }

    private void OnNoValidMovesAvailable(Player player)
    {
        Engine.Logger.LogDebug("No valid moves available for player {0}", player.Name);

        SpotGame.NextPlayer();
    }

    private void OnGameOver()
    {
        Engine.Logger.LogDebug("Game over");

        _handleHumanInput = false;

        SetScoreVisible(true);
        SetPlayerScores();
        StopPlayerJiggle(SpotGame.CurrentPlayer);
        JiggleAllPlayers();

        var allScores = SpotGame.GetAllPlayerScores();
        var maxScore = allScores.Values.Max();
        var winnersWithScores = allScores
            .Where(kvp => kvp.Value == maxScore)
            .Select(kvp => kvp.Key)
            .ToList();

        CreateGameOverText(winnersWithScores);

        if (MusicEnabled)
        {
            _music.Volume = 0.05f;

            var isHumanWinner = winnersWithScores.Any(winner => winner.Type == PlayerType.Human);
            if (isHumanWinner)
                _gameWin?.Play();
            else
                _gameLose?.Play();
        }

        Engine.Instance.State.SaveToFile("savegame.json", false, true);
    }
}
