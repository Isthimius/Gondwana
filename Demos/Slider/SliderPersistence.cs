using System.Text.Json;

namespace Gondwana.Demos.Slider;

// The saved game includes its image, so loading does not depend on the original image file.
internal sealed class SliderSave
{
    public int Version { get; set; } = 1;
    public int Columns { get; set; }
    public int Rows { get; set; }
    public int[] Cells { get; set; } = [];
    public int MoveCount { get; set; }
    public long ElapsedTicks { get; set; }
    public bool Started { get; set; }
    public bool Won { get; set; }
    public string? ImagePngBase64 { get; set; }
}

internal sealed record SliderScore(int Columns, int Rows, int Moves, long ElapsedTicks, DateTime CompletedUtc);

internal static class SliderPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal static void Save(string path, SliderSave save)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Validate(save);
        string json = JsonSerializer.Serialize(save, JsonOptions);
        File.WriteAllText(path, json);
    }

    internal static SliderSave Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        SliderSave save = JsonSerializer.Deserialize<SliderSave>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The file contains no Slider game.");
        Validate(save);
        return save;
    }

    internal static void Validate(SliderSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save.Version != 1)
            throw new InvalidDataException("Unsupported Slider save-file version.");
        if (save.ElapsedTicks < 0 || save.ElapsedTicks > TimeSpan.MaxValue.Ticks)
            throw new InvalidDataException("Invalid saved elapsed time.");
        if (save.Won && !save.Started)
            throw new InvalidDataException("Completed games must have been started.");
        SlidingPuzzle.Restore(save.Columns, save.Rows, save.Cells, save.MoveCount);
        if (!string.IsNullOrEmpty(save.ImagePngBase64))
        {
            try { _ = Convert.FromBase64String(save.ImagePngBase64); }
            catch (FormatException ex) { throw new InvalidDataException("Invalid embedded puzzle image.", ex); }
        }
    }

    private static string ScorePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Gondwana", "Slider", "high-scores.json");

    internal static IReadOnlyList<SliderScore> LoadScores()
    {
        if (!File.Exists(ScorePath))
            return [];
        try
        {
            return (JsonSerializer.Deserialize<List<SliderScore>>(File.ReadAllText(ScorePath))
                ?? []).Where(s => s.Columns is >= 3 and <= 20 &&
                    s.Rows is >= 3 and <= 20 && s.Moves >= 0 &&
                    s.ElapsedTicks >= 0 && s.ElapsedTicks <= TimeSpan.MaxValue.Ticks)
                .OrderBy(s => s.ElapsedTicks).ThenBy(s => s.Moves).ToArray();
        }
        catch (IOException) { return []; }
        catch (JsonException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    internal static IReadOnlyList<SliderScore> AddScore(
        IReadOnlyList<SliderScore> existing, SliderScore score)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(score);
        return existing.Append(score)
            .GroupBy(s => (s.Columns, s.Rows))
            .SelectMany(g => g.OrderBy(s => s.ElapsedTicks).ThenBy(s => s.Moves).Take(10))
            .OrderBy(s => s.Columns).ThenBy(s => s.Rows).ThenBy(s => s.ElapsedTicks)
            .ToArray();
    }

    internal static void SaveScores(IReadOnlyList<SliderScore> scores)
    {
        string path = ScorePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(scores, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    internal static string FormatScores(IReadOnlyList<SliderScore> scores)
    {
        if (scores.Count == 0)
            return "No completed puzzles yet. Finish a shuffled puzzle to set a record.";

        return string.Join(Environment.NewLine, scores
            .GroupBy(s => (s.Columns, s.Rows))
            .OrderBy(g => g.Key.Columns).ThenBy(g => g.Key.Rows)
            .Select(g => $"{g.Key.Columns} x {g.Key.Rows}{Environment.NewLine}" +
                string.Join(Environment.NewLine, g.OrderBy(s => s.ElapsedTicks)
                    .ThenBy(s => s.Moves).Take(10).Select((s, index) =>
                        $"  {index + 1,2}. {TimeSpan.FromTicks(s.ElapsedTicks):mm\\:ss}  " +
                        $"{s.Moves} moves  ({s.CompletedUtc:yyyy-MM-dd})"))));
    }
}
