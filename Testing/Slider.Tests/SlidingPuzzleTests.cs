namespace Gondwana.Demos.Slider.Tests;

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

        Assert.Equal(
            Enumerable.Range(0, 19).Append(-1).OrderBy(x => x),
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

            Assert.Equal(
                1,
                Math.Abs(move.FromIndex % board.Columns - move.DestinationIndex % board.Columns)
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

        int[] before = board.Cells.ToArray();
        int[] afterFirstMove;

        using (IEnumerator<PuzzleMove> steps = board.ShuffleSteps(50, new Random(15)).GetEnumerator())
        {
            Assert.True(steps.MoveNext());
            PuzzleMove firstMove = steps.Current;
            Assert.Equal(firstMove.PieceId, board.Cells[firstMove.DestinationIndex]);
            Assert.Equal(-1, board.Cells[firstMove.FromIndex]);
            Assert.Equal(0, board.MoveCount);
            Assert.Equal(2, board.Cells.Where((piece, index) => piece != before[index]).Count());
            afterFirstMove = board.Cells.ToArray();
        }

        // Disposing the sequence must not instantly apply the remaining shuffle.
        Assert.Equal(afterFirstMove, board.Cells);

        Assert.Equal(
            Enumerable.Range(0, 8).Append(-1).OrderBy(x => x),
            board.Cells.OrderBy(x => x));
    }

    [Fact]
    public void RejectsUnsupportedBoardSizes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingPuzzle(2, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingPuzzle(3, 21));
    }
}

public sealed class SliderPersistenceTests
{
    [Fact]
    public void PieceHomeCoordinatesRemainStableAfterShuffling()
    {
        var board = new SlidingPuzzle(4, 3);
        board.Shuffle(40, new Random(13));
        Assert.Equal((2, 0), board.HomeOf(2));
        Assert.Equal((1, 2), board.HomeOf(9));
    }

    [Fact]
    public void SaveStateRoundTripsBoardAndElapsedTime()
    {
        var board = new SlidingPuzzle(4, 3);
        board.Shuffle(30, new Random(7));

        var save = new SliderSave
        {
            Columns = 4,
            Rows = 3,
            Cells = board.Cells.ToArray(),
            MoveCount = 12,
            Started = true,
            ElapsedTicks = TimeSpan.FromMinutes(1.5).Ticks
        };

        SliderPersistence.Validate(save);

        var restored = SlidingPuzzle.Restore(
            save.Columns,
            save.Rows,
            save.Cells,
            save.MoveCount);

        Assert.Equal(board.Cells, restored.Cells);
        Assert.Equal(12, restored.MoveCount);
        Assert.Equal(TimeSpan.FromMinutes(1.5).Ticks, save.ElapsedTicks);
    }

    [Fact]
    public void GameJsonRoundTripsPictureTilesMovesAndTimer()
    {
        var board = new SlidingPuzzle(3, 3);
        board.Shuffle(12, new Random(3));

        var save = new SliderSave
        {
            Columns = board.Columns,
            Rows = board.Rows,
            Cells = board.Cells.ToArray(),
            MoveCount = 7,
            Started = true,
            ElapsedTicks = TimeSpan.FromMinutes(2).Ticks,
            ImagePngBase64 = Convert.ToBase64String([1, 2, 3, 4])
        };

        string json = SliderPersistence.Serialize(save);
        SliderSave loaded = SliderPersistence.Deserialize(json);
        Assert.Equal(save.Cells, loaded.Cells);
        Assert.Equal(save.MoveCount, loaded.MoveCount);
        Assert.Equal(save.ElapsedTicks, loaded.ElapsedTicks);
        Assert.Equal(save.ImagePngBase64, loaded.ImagePngBase64);
    }

    [Fact]
    public void RejectsCorruptOrDuplicateSavedPieces()
    {
        var save = new SliderSave
        {
            Columns = 3,
            Rows = 3,
            Cells = [0, 1, 2, 3, 4, 5, 6, 6, -1]
        };

        Assert.Throws<InvalidDataException>(() => SliderPersistence.Validate(save));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(2, 3)]
    [InlineData(3, 21)]
    [InlineData(22, 3)]
    public void InvalidSavedDimensionsAreReportedAsInvalidData(int columns, int rows)
    {
        var save = new SliderSave { Columns = columns, Rows = rows };
        Assert.Throws<InvalidDataException>(() => SliderPersistence.Validate(save));
    }

    [Fact]
    public void HighScoreDurationsIncludeHoursAndDays()
    {
        Assert.Equal(
            "01:05:00",
            SliderPersistence.FormatElapsed(TimeSpan.FromMinutes(65)));

        Assert.Equal(
            "1.02:05:00",
            SliderPersistence.FormatElapsed(TimeSpan.FromDays(1) + TimeSpan.FromHours(2) + TimeSpan.FromMinutes(5)));
    }

    [Theory]
    [InlineData(
        20000,
        20,
        900,
        20)]
    [InlineData(
        20,
        20000,
        20,
        650)]
    [InlineData(
        1200,
        800,
        900,
        650)]
    public void ImageFitDoesNotDistortExtremeAspectRatios(
        int imageWidth,
        int imageHeight,
        int atlasWidth,
        int atlasHeight)
    {
        var bounds = PuzzleImageLayout.FitImage(
            imageWidth,
            imageHeight,
            atlasWidth,
            atlasHeight);

        Assert.True(bounds.Left >= 0f);
        Assert.True(bounds.Top >= 0f);
        Assert.True(bounds.Right <= atlasWidth);
        Assert.True(bounds.Bottom <= atlasHeight);

        Assert.InRange(
            bounds.Width / bounds.Height / (imageWidth / (double)imageHeight),
            0.999,
            1.001);
    }

    [Fact]
    public void HighScoresKeepTenFastestPerBoardSize()
    {
        var scores = Enumerable.Range(1, 15)
            .Select(i => new SliderScore(
                3,
                3,
                i,
                TimeSpan.FromSeconds(i).Ticks,
                DateTime.UtcNow))
            .ToArray();

        var results = SliderPersistence.AddScore(
            scores,
            new SliderScore(
                4,
                4,
                23,
                40,
                DateTime.UtcNow));

        Assert.Equal(10, results.Count(s => s.Columns == 3));
        Assert.Single(results.Where(s => s.Columns == 4));
    }
}

public sealed class SliderArrowKeysTests
{
    // Windows Forms Key enum: Left=37, Up=38, Right=39, Down=40.
    private static readonly SliderArrowKeys WinFormsKeys = new(
        37,
        39,
        38,
        40);

    [Theory]
    [InlineData("37", 1, 0)]
    [InlineData("39", -1, 0)]
    [InlineData("38", 0, 1)]
    [InlineData("40", 0, -1)]
    [InlineData("ArrowLeft", 1, 0)]
    [InlineData("ArrowRight", -1, 0)]
    [InlineData("ArrowUp", 0, 1)]
    [InlineData("ArrowDown", 0, -1)]
    [InlineData("Left", 1, 0)]
    [InlineData("Right", -1, 0)]
    public void TranslatesNumericAndNamedArrowKeys(string key, int expectedColumn, int expectedRow)
    {
        Assert.True(WinFormsKeys.TryGetOffset(key, out int column, out int row));
        Assert.Equal(expectedColumn, column);
        Assert.Equal(expectedRow, row);
    }

    [Fact]
    public void RespectsHostSpecificNumericKeyCodes()
    {
        // BlazorKey uses 37=ArrowLeft, 38=ArrowRight, 39=ArrowUp, 40=ArrowDown.
        var blazorKeys = new SliderArrowKeys(
            37,
            38,
            39,
            40);

        Assert.True(blazorKeys.TryGetOffset("38", out int column, out int row));
        Assert.Equal(-1, column);
        Assert.Equal(0, row);
    }

    [Theory]
    [InlineData("16")]
    [InlineData("Space")]
    [InlineData("")]
    [InlineData(null)]
    public void IgnoresNonArrowKeys(string? key)
    {
        Assert.False(WinFormsKeys.TryGetOffset(key, out int column, out int row));
        Assert.Equal(0, column);
        Assert.Equal(0, row);
    }
}
