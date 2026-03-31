using System;
using System.Collections.Generic;
using System.Drawing;

namespace HWG.Spot.Game;

internal class SpotGame : IDisposable
{
    #region events

    internal event Action<SpotGame> GameStarted;
    internal event Action<Player> PlayerTurnEnded;
    internal event Action<Player> PlayerTurnStarted;
    internal event Action<SpotGameField.Cell> SpotSelected;
    internal event Action<SpotGameField.Cell> SpotDeselected;
    internal event Action<SpotGameField.Cell> InvalidSelectionAttempted;
    internal event Action<SpotGameField.Cell> InvalidMoveAttempted;
    internal event Action<Player> NoValidMovesAvailable;

    internal event Action<PlayerMovement> PlayerMoved;
    internal event Action<List<SpotGameField.Cell>> CellsCaptured;
    internal event Action GameOver;

    #endregion

    private int _currentPlayerIndex = 0;

    internal SpotGameField SpotGameField { get; set; } = SpotGameField.Create(12, 12, Array.Empty<Player>());
    internal Player[] Players { get; set; } = Array.Empty<Player>();
    internal bool IsGameOver { get; set; } = false;
    internal SpotGameField.Cell SelectedCell { get; private set; } = null;

    internal SpotGame() { }

    internal SpotGameField NewGame(int columns, int rows, Player[] players)
    {
        if (players.Length < 2)
            throw new ArgumentException("At least two players are required to start a game.", nameof(players));

        if (players.Length > 4)
            throw new ArgumentException("No more than four players can play at the same time.", nameof(players));

        if (columns < 3 || rows < 3)
            throw new ArgumentException("The game field must be at least 3x3 in size.", nameof(columns));

        if (columns > 12 || rows > 12)
            throw new ArgumentException("The game field cannot be larger than 12x12 in size.", nameof(columns));

        SpotGameField = SpotGameField.Create(columns, rows, players);
        Players = players;
        IsGameOver = false;
        _currentPlayerIndex = 0;

        // shift the Origin of the game field to the center of the scene
        var horizShift = (12 - columns) * 32;
        var vertShift = (12 - rows) * 32;
        SpotGameField.OriginPx = new Point(-horizShift, -vertShift);

        GameStarted?.Invoke(this);
        PlayerTurnStarted?.Invoke(CurrentPlayer);

        return SpotGameField;
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

    #region player turn logic

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
        PlayerMoved = null;
        CellsCaptured = null;
        NoValidMovesAvailable = null;
        GameOver = null;
    }
}
