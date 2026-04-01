using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Gondwana;
using Gondwana.Drawing.Sprites;
using Gondwana.Scenes;
using Newtonsoft.Json;

namespace HWG.Spot.Game;

internal partial class SpotGameField : SceneLayer
{
    internal static class SpotFieldKeys
    {
        internal static readonly ValueKey<Cell> Cell = new ValueKey<Cell>("Spot.Cell");
    }

    internal class Cell
    {
        internal int X { get; set; }
        internal int Y { get; set; }
        internal Player OccupiedBy { get; set; } = null;
        internal Sprite Sprite { get; set; } = null;
    }

    private SpotGameField(int columns, int rows) : base(columns, rows, 64, 64) { }

    internal static SpotGameField Create(int columns, int rows, Player[] players)
    {
        var field = new SpotGameField(columns, rows);

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                var cell = new Cell { X = x, Y = y, OccupiedBy = null };
                field[x, y].ValueBag.Set(SpotFieldKeys.Cell, cell);
            }
        }

        // upper left
        if (players.Length >= 1)
        { 
            var cell = field.GetCell(0, 0);
            cell.OccupiedBy = players[0];
            var sprite = SpriteManager.Instance.CreateSprite(field, players[0].DefaultFrame);
            sprite.SetPosition(new(0, 0));
            sprite.RenderSize = new Size(56, 56);
            sprite.VertAlign = VerticalAlignment.Middle;
            sprite.Visible = true;
            cell.Sprite = sprite;
        }

        // lower right
        if (players.Length >= 2)
        {
            var cell = field.GetCell(columns - 1, rows - 1);
            cell.OccupiedBy = players[1];
            var sprite = SpriteManager.Instance.CreateSprite(field, players[1].DefaultFrame);
            sprite.SetPosition(new(columns - 1, rows - 1));
            sprite.RenderSize = new Size(56, 56);
            sprite.VertAlign = VerticalAlignment.Middle;
            sprite.Visible = true;
            cell.Sprite = sprite;
        }

        // upper right
        if (players.Length >= 3)
        {
            var cell = field.GetCell(columns - 1, 0);
            cell.OccupiedBy = players[2];
            var sprite = SpriteManager.Instance.CreateSprite(field, players[2].DefaultFrame);
            sprite.SetPosition(new(columns - 1, 0));
            sprite.RenderSize = new Size(56, 56);
            sprite.VertAlign = VerticalAlignment.Middle;
            sprite.Visible = true;
            cell.Sprite = sprite;
        }

        // lower left
        if (players.Length >= 4)
        {
            var cell = field.GetCell(0, rows - 1);
            cell.OccupiedBy = players[3];
            var sprite = SpriteManager.Instance.CreateSprite(field, players[3].DefaultFrame);
            sprite.SetPosition(new(0, rows - 1));
            sprite.RenderSize = new Size(56, 56);
            sprite.VertAlign = VerticalAlignment.Middle;
            sprite.Visible = true;
            cell.Sprite = sprite;
        }

        field.ShowGridLines = true;

        return field;
    }

    internal Cell GetCell(int x, int y) => this[x, y].ValueBag.Get(SpotFieldKeys.Cell);

    internal void SetCell(int x, int y, Cell cell) => this[x, y].ValueBag.Set(SpotFieldKeys.Cell, cell);

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

    internal PlayerMovement GetMovementType(int fromX, int fromY, int destX, int destY)
    {
        MovementType movementType;

        var player = GetCell(fromX, fromY).OccupiedBy;

        // if there's no player at the source cell, it's illegal
        // out of bounds from is illegal
        // out of bounds destination is illegal
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

    internal List<PlayerMovement> GetAllValidMoves()
    {
        var validMoves = new List<PlayerMovement>();

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

    internal List<PlayerMovement> GetAllValidMoves(Player player)
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

    internal List<PlayerMovement> GetBestMovesForPlayer(Player player)
    {
        var validMoves = GetAllValidMoves(player);
        int bestNetSquaresGained = int.MinValue;
        var bestMoves = new List<PlayerMovement>();

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

    internal List<Cell> CaptureAdjacentCells(int x, int y, Player player)
    {
        var capturedCells = new List<Cell>();

        var adjacentCells = GetAdjacentCells(x, y);
        foreach (var cell in adjacentCells)
        {
            if (cell.OccupiedBy != null && cell.OccupiedBy != player)
            {
                cell.OccupiedBy = player;
                capturedCells.Add(cell);
            }
        }

        return capturedCells;
    }

    internal bool IsGameOver
    {
        get
        {
            if (GetAllValidMoves().Any())
                return false;

            return true;
        }
    }

    #endregion game logic
}
