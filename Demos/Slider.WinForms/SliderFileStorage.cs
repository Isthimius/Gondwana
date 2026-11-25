using System.Text.Json;

namespace Gondwana.Demos.Slider;

/// <summary>Desktop file persistence and user-profile storage for Slider.</summary>
internal sealed class SliderFileStorage : ISliderScoreStore
{
    private static string ScorePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Gondwana", "Slider", "high-scores.json");

    internal static void SaveGame(string path, SliderSave save)
        => File.WriteAllText(path, SliderPersistence.Serialize(save));

    internal static SliderSave LoadGame(string path)
        => SliderPersistence.Deserialize(File.ReadAllText(path));

    public IReadOnlyList<SliderScore> LoadScores()
    {
        if (!File.Exists(ScorePath))
            return [];

        try
        { return SliderPersistence.DeserializeScores(File.ReadAllText(ScorePath)); }
        catch (IOException) { return []; }
        catch (JsonException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    public void SaveScores(IReadOnlyList<SliderScore> scores)
    {
        string path = ScorePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, SliderPersistence.SerializeScores(scores));
        File.Move(temp, path, overwrite: true);
    }
}
