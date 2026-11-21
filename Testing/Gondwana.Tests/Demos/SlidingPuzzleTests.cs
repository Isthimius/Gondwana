using Gondwana.Demos.Slider;

namespace Gondwana.Tests.Demos;

public sealed class SlidingPuzzleTests
{
    [Fact]
    public void StartsSolved_WithLastCellEmpty()
    {
        var board = new SlidingPuzzle(4, 3);

        Assert.Equal(11, board.TotalPieces);
        Assert.Equal(11, board.CorrectPieces);
        Assert.Equal(-1, board.PieceAt(3, 2));
        Assert.True(board.IsSolved);
        Assert.Equal(0, board.MoveCount);
    }

    [Fact]
    public void MovesOnlyAdjacentPieces_AndRestoresSolvedState()
    {
        var board = new SlidingPuzzle(3, 3);
        Assert.False(board.TryMove(0, 0, out _));
        Assert.False(board.TryMove(3, 2, out _));
        Assert.False(board.TryMove(2, 2, out _));

        Assert.True(board.TryMovePiece(5, out PuzzleMove move));
        Assert.Equal(new PuzzleMove(5, 5, 8), move);
        Assert.Equal(-1, board.PieceAt(2, 1));
        Assert.False(board.TryMovePiece(0, out _));
        Assert.False(board.TryMove(0, 2, out _)); // Never wrap across row boundaries.
        Assert.False(board.IsSolved);
        Assert.Equal(1, board.MoveCount);

        Assert.True(board.TryMove(2, 2, out _));
        Assert.True(board.IsSolved);
        Assert.Equal(2, board.MoveCount);
    }

    [Fact]
    public void ShufflingPreservesPieces_AndResetsMoveCount()
    {
        var board = new SlidingPuzzle(5, 4);
        Assert.True(board.TryMove(4, 2, out _));

        board.Shuffle(71, new Random(1234));

        Assert.False(board.IsSolved);
        Assert.Equal(0, board.MoveCount);
        Assert.Equal(Enumerable.Range(0, 19).Append(-1).OrderBy(x => x),
            board.Cells.OrderBy(x => x));
    }

    [Fact]
    public void ShuffleStepsExposeEachLegalMove_WithoutBacktracking()
    {
        var board = new SlidingPuzzle(4, 3);
        int previousBlank = board.Columns * board.Rows - 1;
        int previousPiece = -1;
        int stepCount = 0;

        foreach (PuzzleMove move in board.ShuffleSteps(36, new Random(1234)))
        {
            Assert.Equal(previousBlank, move.DestinationIndex);
            Assert.Equal(move.PieceId, board.Cells[move.DestinationIndex]);
            Assert.Equal(-1, board.Cells[move.FromIndex]);
            Assert.Equal(1, Math.Abs(move.FromIndex % board.Columns - move.DestinationIndex % board.Columns)
                + Math.Abs(move.FromIndex / board.Columns - move.DestinationIndex / board.Columns));
            Assert.NotEqual(previousPiece, move.PieceId);
            Assert.Equal(0, board.MoveCount);

            previousBlank = move.FromIndex;
            previousPiece = move.PieceId;
            stepCount++;
        }

        Assert.InRange(stepCount, 36, 37); // An extra move prevents a solved shuffle.
        Assert.False(board.IsSolved);
    }

    [Fact]
    public void ShuffleStepsCanBeCancelled_WithoutApplyingRemainingMoves()
    {
        var board = new SlidingPuzzle(3, 3);
        Assert.True(board.TryMovePiece(7, out _));
        Assert.Equal(1, board.MoveCount);

        using (IEnumerator<PuzzleMove> steps = board.ShuffleSteps(50, new Random(15)).GetEnumerator())
        {
            Assert.True(steps.MoveNext());
            PuzzleMove firstMove = steps.Current;
            Assert.Equal(firstMove.PieceId, board.Cells[firstMove.DestinationIndex]);
            Assert.Equal(-1, board.Cells[firstMove.FromIndex]);
            Assert.Equal(0, board.MoveCount);
        }

        // Disposing the sequence must not instantly apply the remaining shuffle.
        Assert.Equal(Enumerable.Range(0, 8).Append(-1).OrderBy(x => x),
            board.Cells.OrderBy(x => x));
        Assert.True(board.Cells.Count(piece => piece == -1) == 1);
    }

    [Fact]
    public void RejectsUnsupportedBoardSizes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingPuzzle(2, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingPuzzle(3, 21));
    }
}
