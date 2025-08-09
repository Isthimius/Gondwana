using Gondwana.Audio;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Sprites;
using Gondwana.Grid;
using Gondwana.Resource;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gondwana.State;

public class EngineState
{
    [JsonInclude]
    public Dictionary<string, string> ValueBag { get; set; } = new();

    [JsonInclude]
    public IEnumerable<EngineResourceFile> ResourceFiles => EngineResourceFile.AllResourceFiles;

    [JsonInclude]
    public Dictionary<string, Tilesheet> Tilesheets => Tilesheet._tilesheets;

    [JsonInclude]
    public Dictionary<string, Cycle> Cycles => Cycle._cycles;

    [JsonInclude]
    public List<SceneLayer> Grids => SceneLayer._allSceneLayer;

    [JsonInclude]
    public List<Scene> GridsDisplay => Scene._allSceneLayeres;

    [JsonInclude]
    public List<Sprite> Sprites => Drawing.Sprites.Sprites._spriteList;

    [JsonInclude]
    public Dictionary<string, SoundResource> SoundResources => SoundResourceManager.Instance.GetAll();

    internal void Clear()
    {
        ValueBag.Clear();
        EngineResourceFile.ClearAll();
        Tilesheet.ClearAllTilesheets();
        Cycle.ClearAllAnimationCycles();
        Scene.ClearAllSceneLayeres();
        SceneLayer.ClearAllSceneLayer();
        Drawing.Sprites.Sprites.Clear();
        SoundResourceManager.Instance.Dispose();
    }

    public void SaveToFile(string path, bool compress = false)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        if (compress)
        {
            using var file = File.Create(path);
            using var zip = new GZipStream(file, CompressionMode.Compress);
            using var writer = new StreamWriter(zip);
            writer.Write(json);
        }
        else
        {
            File.WriteAllText(path, json);
        }
    }

    public static EngineState LoadFromFile(string path, bool compressed = false)
    {
        string json;

        if (compressed)
        {
            using var file = File.OpenRead(path);
            using var zip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(zip);
            json = reader.ReadToEnd();
        }
        else
        {
            json = File.ReadAllText(path);
        }

        var result = JsonSerializer.Deserialize<EngineState>(json) ?? new EngineState();
        var engineState = new EngineState();
        engineState.ValueBag = result.ValueBag ?? new();

        // TODO: step through and load all the things...!!!
        // TODO: load audio files not in Resource file
        LoadResourceFiles(result.ResourceFiles);
        //
        //
        //
        //

        return engineState;
    }

    private static void LoadResourceFiles(IEnumerable<EngineResourceFile> resourceFiles)
    {
        // Replace raw deserialized resource files with proper loaded instances
        if (resourceFiles.Any())
        {
            foreach (var raw in resourceFiles)
            {
                try
                {
                    EngineResourceFile.LoadOrCreate(raw.FilePath, raw.Password, raw.UseEncryption);
                }
                catch (Exception ex)
                {
                    Engine.Logger.LogError(ex, "Failed to load resource file '{FilePath}'", raw.FilePath);
                    throw;
                }
            }
        }
    }
}
