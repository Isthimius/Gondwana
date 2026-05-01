using Gondwana.Movement.Scripted;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Gondwana.Demos.SpotAvalonia.Game;

internal class SpotGame : IDisposable
{
    #region events

    internal event Action<SpotGame>? GameStarted;
    internal event Action<Player>? PlayerTurnEnded;
    internal event Action<Player>? PlayerTurnStarted;
    internal event Action<SpotGameField.Cell>? SpotSelected;
    internal event Action<SpotGameField.Cell>? SpotDeselected;
    internal event Action<SpotGameField.Cell>? InvalidSelectionAttempted;
    internal event Action<SpotGameField.Cell>? InvalidMoveAttempted;
    internal event Action<Player>? NoValidMovesAvailable;
    internal event Action<PlayerMovement>? PlayerMoveStarted;
    internal event Action<PlayerMovement>? PlayerMoveStopped;
    internal event Action<List<SpotGameField.Cell>>? CellsCaptured;
    internal event Action? GameOver;

    #endregion

    private int _currentPlayerIndex = 0;

    internal SpotGameField BackgroundGameField { get; set; } = null!;
    internal SpotGameField SpotGameField { get; set; } = null!;
    internal Player[] Players { get; set; } = Array.Empty<Player>();
    internal SpotGameField.Cell? SelectedCell { get; private set; } = null;

    internal SpotGame() { }

    internal (SpotGameField Field, SpotGameField BackgroundField) NewGame(int columns, int rows, Player[] players)
    {
        var newGameResult = SpotGameField.Create(columns, rows, players);

        SpotGameField = newGameResult.Field;
        BackgroundGameField = newGameResult.BackgroundField;
        Players = players;
        _currentPlayerIndex = 0;

        // shift the Origin of the game field to the center of the scene
        var horizShift = (12 - columns) * 32;
        var vertShift = (12 - rows) * 32;
        SpotGameField.OriginPx = new Point(-horizShift, -vertShift);
        BackgroundGameField.OriginPx = new Point(-horizShift, -vertShift);

        GameStarted?.Invoke(this);
        PlayerTurnStarted?.Invoke(CurrentPlayer);

        return (SpotGameField, BackgroundGameField);
    }

    internal Player CurrentPlayer => Players[_currentPlayerIndex];

    internal int GetPlayerScore(Player player)
    {
        int score = 0;

        for (int x = 0; x < SpotGameField.GridColumnCount; x++)
        {
            for (int y = 0; y < SpotGameField.GridRowCount; y++)
            {
                var cell = SpotGameField.GetCell(x, y);
                if (cell.OccupiedBy != null && cell.OccupiedBy.Equals(player))
                    score++;
            }
        }

        return score;
    }

    internal Dictionary<Player, int> GetAllPlayerScores()
    {
        var scores = new Dictionary<Player, int>();
        foreach (var player in Players)
        {
            scores[player] = GetPlayerScore(player);
        }
        return scores;
    }

    internal bool IsGameOver
    {
        get
        {
            // no more valid moves means board is full
            if (!SpotGameField.GetAllValidMoves().Any())
                return true;

            // if only one player remains, game is over
            var allScores = GetAllPlayerScores();
            if (allScores.Count(score => score.Value > 0) <= 1)
                return true;

            return false;
        }
    }

    #region player turn logic

    internal bool AttemptSelectCell(SpotGameField.Cell cell, out PlayerMovement? playerMovement)
    {
        if (cell.OccupiedBy == CurrentPlayer)
        {
            // if clicking the already selected cell, deselect it
            if (cell.X == SelectedCell?.X && cell.Y == SelectedCell?.Y)
            {
                SpotDeselected?.Invoke(cell);
                SelectedCell = null;
                playerMovement = null;
                return true;
            }

            // deselect existing selection if there is one
            if (SelectedCell != null)
                SpotDeselected?.Invoke(SelectedCell);

            SelectedCell = cell;
            SpotSelected?.Invoke(cell);
            playerMovement = null;
            return true;
        }
        else
        {
            // if no current selection, then this is an invalid selection attempt
            if (SelectedCell == null)
            {
                InvalidSelectionAttempted?.Invoke(cell);
                playerMovement = null;
                return false;
            }

            // there is a current selection, so this is an attempt to move; validate the move
            playerMovement = SpotGameField.GetMovementType(SelectedCell.X, SelectedCell.Y, cell.X, cell.Y);

            switch (playerMovement.Value.MovementType)
            {
                case MovementType.Illegal:
                    InvalidMoveAttempted?.Invoke(cell);
                    return false;

                default:
                    // valid move requested
                    return true;
            }
        }
    }

    internal void ExecuteMove(PlayerMovement playerMovement)
    {
        if (playerMovement.MovementType == MovementType.Illegal)
            return;

        var sprite = playerMovement.FromCell.Sprite;
        var fromCell = playerMovement.FromCell;
        var toCell = SpotGameField.GetCell(playerMovement.DestX, playerMovement.DestY);

        Action<ScriptedMovement>? startHandler = null;
        startHandler = (ScriptedMovement scriptedMovement) =>
        {
            sprite.Movement.ScriptedMovementStarted -= startHandler;
            PlayerMoveStarted?.Invoke(playerMovement);
        };

        Action<ScriptedMovement>? stopHandler = null;
        stopHandler = (ScriptedMovement scriptedMovement) =>
        {
            sprite.Movement.ScriptedMovementStopped -= stopHandler;
            sprite.CurrentFrame = playerMovement.Player.DefaultFrame;

            var capturedCells = SpotGameField.CaptureAdjacentCells(
                playerMovement.DestX,
                playerMovement.DestY,
                playerMovement.Player);

            if (capturedCells.Count > 0)
                CellsCaptured?.Invoke(capturedCells);

            if (IsGameOver)
                GameOver?.Invoke();
            else
                PlayerMoveStopped?.Invoke(playerMovement);
        };

        switch (playerMovement.MovementType)
        {
            case MovementType.Clone:
                sprite.StopPulse();
                var clonedSprite = Engine.Instance.Managers.Sprites.CloneSprite(sprite);
                clonedSprite.ZOrder++;
                sprite.CurrentFrame = playerMovement.Player.DefaultFrame;

                sprite = clonedSprite;
                sprite.Movement.ScriptedMovementStarted += startHandler;
                sprite.Movement.ScriptedMovementStopped += stopHandler;
                sprite.Movement.MoveTo(new(playerMovement.DestX, playerMovement.DestY),
                                           0.4f,
                                           Gondwana.Movement.Easing.EasingKind.SmootherStep,
                                           0.1f);

                toCell.OccupiedBy = playerMovement.Player;
                toCell.Sprite = sprite;

                SelectedCell = null;
                break;

            case MovementType.Jump:
                sprite.StopPulse();
                sprite.Movement.ScriptedMovementStarted += startHandler;
                sprite.Movement.ScriptedMovementStopped += stopHandler;
                sprite.Movement.MoveTo(new(playerMovement.DestX, playerMovement.DestY),
                                       0.4f,
                                       Gondwana.Movement.Easing.EasingKind.EaseInCubic,
                                       0.1f);

                fromCell.OccupiedBy = null;
                fromCell.Sprite = null;

                toCell.OccupiedBy = playerMovement.Player;
                toCell.Sprite = sprite;

                SelectedCell = null;
                break;

            default:
                return;
        }
    }

    internal Player NextPlayer()
    {
        PlayerTurnEnded?.Invoke(CurrentPlayer);
        _currentPlayerIndex = (_currentPlayerIndex + 1) % Players.Length;

        if (SpotGameField.GetAllValidMoves(CurrentPlayer).Count == 0)
        {
            NoValidMovesAvailable?.Invoke(CurrentPlayer);
        }
        else
        {
            PlayerTurnStarted?.Invoke(CurrentPlayer);
        }

        return CurrentPlayer;
    }

    #endregion player turn logic

    public void Dispose()
    {
        GameStarted = null;
        PlayerTurnEnded = null;
        PlayerTurnStarted = null;
        SpotSelected = null;
        SpotDeselected = null;
        InvalidSelectionAttempted = null;
        InvalidMoveAttempted = null;
        PlayerMoveStopped = null;
        CellsCaptured = null;
        NoValidMovesAvailable = null;
        GameOver = null;
    }
}
