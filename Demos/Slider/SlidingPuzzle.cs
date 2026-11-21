namespace Gondwana.Demos.Slider;

/// <summary>
/// Rules for an N-by-M sliding picture puzzle. This model has no rendering or UI dependencies.
/// Piece IDs correspond to their solved row-major positions; -1 is the empty cell.
/// </summary>
internal sealed class SlidingPuzzle
{
    private readonly int[] _cells;
    private int _blank;

    internal SlidingPuzzle(int columns, int rows)
    {
        if (columns is < 3 or > 20)
            throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows is < 3 or > 20)
            throw new ArgumentOutOfRangeException(nameof(rows));

        Columns = columns;
        Rows = rows;
        _cells = Enumerable.Range(0, columns * rows).ToArray();
        _blank = _cells.Length - 1;
        _cells[_blank] = -1;
    }

    internal int Columns { get; }
    internal int Rows { get; }
    internal int MoveCount { get; private set; }
    internal int BlankColumn => _blank % Columns;
    internal int BlankRow => _blank / Columns;
    internal int TotalPieces => _cells.Length - 1;
    internal int CorrectPieces => Enumerable.Range(0, _cells.Length - 1).Count(i => _cells[i] == i);
    internal bool IsSolved => CorrectPieces == TotalPieces;
    internal IReadOnlyList<int> Cells => Array.AsReadOnly(_cells);

    internal int PieceAt(int column, int row)
        => column >= 0 && column < Columns && row >= 0 && row < Rows
            ? _cells[row * Columns + column]
            : -1;

    internal bool TryMove(int column, int row, out PuzzleMove move)
    {
        move = default;
        if (column < 0 || column >= Columns || row < 0 || row >= Rows)
            return false;

        int from = row * Columns + column;
        if (!IsAdjacent(from, _blank))
            return false;

        move = SwapWithBlank(from);
        MoveCount++;
        return true;
    }

    /// <summary>
    /// Shuffling uses legal moves, so the resulting arrangement is always solvable.
    /// Avoid immediate reversals, and don't count shuffle operations as player moves.
    /// </summary>
    internal bool TryMovePiece(int piece, out PuzzleMove move)
    {
        int index = Array.IndexOf(_cells, piece);
        if (index >= 0)
            return TryMove(index % Columns, index / Columns, out move);

        move = default;
        return false;
    }

    internal void Shuffle(int moves, Random? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(moves);
        random ??= Random.Shared;
        int previousBlank = -1;

        for (int i = 0; i < moves; i++)
        {
            var candidates = Adjacent(_blank).Where(index => index != previousBlank).ToArray();
            if (candidates.Length == 0)
                candidates = Adjacent(_blank).ToArray();

            int oldBlank = _blank;
            SwapWithBlank(candidates[random.Next(candidates.Length)]);
            previousBlank = oldBlank;
        }

        // A puzzle accidentally returned to the solved layout is not a useful shuffle.
        if (moves > 0 && IsSolved)
        {
            int candidate = Adjacent(_blank).First();
            SwapWithBlank(candidate);
        }

        MoveCount = 0;
    }

    private PuzzleMove SwapWithBlank(int from)
    {
        int destination = _blank;
        int piece = _cells[from];
        _cells[destination] = piece;
        _cells[from] = -1;
        _blank = from;
        return new PuzzleMove(piece, from, destination);
    }

    private bool IsAdjacent(int a, int b)
        => Math.Abs(a % Columns - b % Columns) + Math.Abs(a / Columns - b / Columns) == 1;

    private IEnumerable<int> Adjacent(int index)
    {
        int col = index % Columns;
        int row = index / Columns;
        if (col > 0) yield return index - 1;
        if (col + 1 < Columns) yield return index + 1;
        if (row > 0) yield return index - Columns;
        if (row + 1 < Rows) yield return index + Columns;
    }
}

internal readonly record struct PuzzleMove(int PieceId, int FromIndex, int DestinationIndex);
