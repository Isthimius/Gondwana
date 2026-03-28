using System;
using System.Collections.Generic;
using System.Linq;
using Gondwana;
using Gondwana.Scenes;

namespace HWG.Spot.Game;

internal partial class SpotGameField : SceneLayer
{
    internal static class SpotFieldKeys
    {
        public static readonly ValueKey<Cell> Cell = new ValueKey<Cell>("Spot.Cell");
    }

    internal class Cell
    {
        internal int X { get; set; }
        internal int Y { get; set; }
        internal Player? OccupiedBy { get; set; } = null;
    }

    private SpotGameField(int width, int height) : base(width, height, 64, 64) { }

    internal static SpotGameField Create(int width, int height, Player[] players)
    {
        var field = new SpotGameField(width, height);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var cell = new Cell { X = x, Y = y, OccupiedBy = null };
                field[x, y].ValueBag.Set(SpotFieldKeys.Cell, cell);
            }
        }

        // upper left
        if (players.Length >= 1)
            field.GetCell(0, 0).OccupiedBy = players[0];

        // lower right
        if (players.Length >= 2)
            field.GetCell(width - 1, height - 1).OccupiedBy = players[1];

        // upper right
        if (players.Length >= 3)
            field.GetCell(width - 1, 0).OccupiedBy = players[2];

        // lower left
        if (players.Length >= 4)
            field.GetCell(0, height - 1).OccupiedBy = players[3];

        field.ShowGridLines = true;

        return field;
    }

    internal Cell GetCell(int x, int y) => this[x, y].ValueBag.Get(SpotFieldKeys.Cell);

    #region game logic

    /// <summary>
    /// get a list of points on current field that are adjacent to the given point (x, y)
    /// </summary>
    internal List<Cell> GetAdjacentCells(int x, int y)
    {
        var adjacentCells = new List<Cell>();

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                // exclude self from "adjacent"
                if (dx == 0 && dy == 0)
                    continue;

                int adjX = x + dx;
                int adjY = y + dy;

                // check bounds
                if (adjX >= 0 && adjX < GridColumnCount && adjY >= 0 && adjY < GridRowCount)
                {
                    adjacentCells.Add(GetCell(adjX, adjY));
                }
            }
        }
        
        return adjacentCells;
    }

    internal void CaptureAdjacentCells(int x, int y, Player player)
    {
        var adjacentCells = GetAdjacentCells(x, y);
        foreach (var cell in adjacentCells)
        {
            if (cell.OccupiedBy != null && cell.OccupiedBy != player)
            {
                cell.OccupiedBy = player;
            }
        }
    }

    internal PlayerMovementType GetMovementType(int fromX, int fromY, int destX, int destY)
    {
        MovementType movementType;

        var player = GetCell(fromX, fromY).OccupiedBy;

        // if there's no player at the source cell, it's illegal;
        // out of bounds from is illegal;
        // out of bounds destination is illegal;
        // if destination cell is occupied, it's illegal
        if ((player == null) ||
            (fromX < 0 || fromX >= GridColumnCount || fromY < 0 || fromY >= GridRowCount) ||
            (destX < 0 || destX >= GridColumnCount || destY < 0 || destY >= GridRowCount) ||
            (GetCell(destX, destY).OccupiedBy != null))
        {
            movementType = MovementType.Illegal;
        }
        else
        {
            int dx = Math.Abs(destX - fromX);
            int dy = Math.Abs(destY - fromY);

            if (dx <= 1 && dy <= 1)
                movementType = MovementType.Clone;
            else if (dx <= 2 && dy <= 2)
                movementType = MovementType.Jump;
            else
                movementType = MovementType.Illegal;
        }
         
        return new(player, movementType, fromX, fromY, destX, destY);
    }

    internal List<PlayerMovementType> GetAllValidMoves()
    {
        var validMoves = new List<PlayerMovementType>();

        for (int fromX = 0; fromX < GridColumnCount; fromX++)
        {
            for (int fromY = 0; fromY < GridRowCount; fromY++)
            {
                if (GetCell(fromX, fromY).OccupiedBy == null)
                    continue;

                for (int destX = fromX - 1; destX < fromX + 1; destX++)
                {
                    for (int destY = fromY - 1; destY < fromY + 1; destY++)
                    {
                        var playerMoveType = GetMovementType(fromX, fromY, destX, destY);
                        if (playerMoveType.MovementType != MovementType.Illegal)
                        {
                            validMoves.Add(playerMoveType);
                        }
                    }
                }
            }
        }

        return validMoves;
    }

    internal List<PlayerMovementType> GetAllValidMoves(Player player)
    {
        return GetAllValidMoves().Where(move => move.Player == player).ToList();
    }

    internal int SquaresTakenIfJumpTo(Player movingPlayer, int destX, int destY)
    {
        var adjacentCells = GetAdjacentCells(destX, destY);
        return adjacentCells.Count(cell => cell.OccupiedBy != null && cell.OccupiedBy != movingPlayer);
    }

    internal int SquaresOpenIfJumpFrom(Player movingPlayer, int fromX, int fromY)
    {
        var adjacentCells = GetAdjacentCells(fromX, fromY);
        return adjacentCells.Count(cell => cell.OccupiedBy == movingPlayer);
    }

    internal List<PlayerMovementType> GetBestMovesForPlayer(Player player)
    {
        var validMoves = GetAllValidMoves(player);
        int bestNetSquaresGained = int.MinValue;
        var bestMoves = new List<PlayerMovementType>();
     
        foreach (var move in validMoves)
        {
            int squaresTaken = SquaresTakenIfJumpTo(player, move.DestX, move.DestY);
            int squaresOpen = SquaresOpenIfJumpFrom(player, move.FromX, move.FromY);
            int netSquaresGained = squaresTaken - squaresOpen;
        
            if (netSquaresGained > bestNetSquaresGained)
            {
                bestNetSquaresGained = netSquaresGained;
                bestMoves.Clear();
                bestMoves.Add(move);
            }
            else if (netSquaresGained == bestNetSquaresGained)
            {
                bestMoves.Add(move);
            }
        }

        return bestMoves;
    }

    #endregion game logic
}
