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

        Assert.True(board.TryMove(2, 1, out PuzzleMove move));
        Assert.Equal(new PuzzleMove(5, 5, 8), move);
        Assert.Equal(-1, board.PieceAt(2, 1));
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
    public void RejectsUnsupportedBoardSizes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingPuzzle(2, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingPuzzle(3, 21));
    }
}
