namespace HWG.Spot;

internal class Field
{
    private Cell[,] _cells;
    private Player[] _players;

    private Field() { }

    internal static Field Create(int width, int height, Player[] players)
    {
        var field = new Field
        {
            _cells = new Cell[width, height],
        };

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                field._cells[x, y] = new Cell();
            }
        }

        field._players = players;

        // upper left
        if (players.Length >= 1)
            field._cells[0, 0].OccupiedBy = players[0];

        // lower right
        if (players.Length >= 2)
            field._cells[width - 1, height - 1].OccupiedBy = players[1];

        // upper right
        if (players.Length >= 3)
            field._cells[width - 1, 0].OccupiedBy = players[2];

        // lower left
        if (players.Length >= 4)
            field._cells[0, height - 1].OccupiedBy = players[3];

        return field;
    }

    internal class Cell
    {
        public Player? OccupiedBy { get; set; } = null;
    }
}
