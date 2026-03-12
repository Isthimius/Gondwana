using Gondwana;
using Gondwana.Scenes;

namespace HWG.Spot;

internal class SpotGameField : SceneLayer
{
    internal static class SpotFieldKeys
    {
        public static readonly ValueKey<SpotGameField.Cell> Cell = new("Spot.Cell");
    }

    private Player[] _players;

    private SpotGameField(int width, int height) : base(width, height, 64, 64) { }

    internal static SpotGameField Create(int width, int height, Player[] players)
    {
        var field = new SpotGameField(width, height);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                field[x, y].ValueBag.Set(SpotFieldKeys.Cell, new Cell());
            }
        }

        field._players = players;

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

        return field;
    }

    public int GetPlayerScore(Player player)
    {
        int score = 0;

        for (int x = 0; x < this.GridColumnCount; x++)
        {
            for (int y = 0; y < this.GridRowCount; y++)
            {
                var cell = GetCell(x, y);
                if (cell.OccupiedBy.HasValue)
                {
                    if (cell.OccupiedBy.Value.Equals(player))
                        score++;
                }
            }
        }

        return score;
    }

    public Cell GetCell(int x, int y)
    {
        return this[x, y].ValueBag.Get(SpotFieldKeys.Cell);
    }

    internal class Cell
    {
        internal Player? OccupiedBy { get; set; } = null;
    }
}
