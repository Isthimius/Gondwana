using System.IO.Compression;
using System.Text.Json;
using Gondwana.Audio;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Resource;
using Gondwana.Scenes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Gondwana;

[JsonObject(IsReference = true)]
public class EngineState
{
    public static JsonSerializerSettings JsonSerializerSettings { get; set; }
        = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
            PreserveReferencesHandling = PreserveReferencesHandling.All
        };

    [JsonProperty]
    public Dictionary<string, string> ValueBag { get; set; } = new();

    [JsonProperty]
    public IEnumerable<EngineResourceFile> ResourceFiles => EngineResourceFile.AllResourceFiles;

    [JsonProperty]
    public IDictionary<string, Tilesheet> Tilesheets => TilesheetRegistry.Instance.GetAll().ToDictionary();

    [JsonProperty]
    public Dictionary<string, Cycle> Cycles => Cycle._cycles;

    [JsonProperty]
    public List<SceneLayer> Grids => SceneLayer._allSceneLayer;

    [JsonProperty]
    public List<Scene> GridsDisplay => Scene._allSceneLayeres;

    [JsonProperty]
    public List<Sprite> Sprites => SpriteManager._spriteList;

    [JsonProperty]
    public Dictionary<string, SoundResource> SoundResources => SoundResourceManager.Instance.GetAll();

    internal void Clear()
    {
        ValueBag.Clear();
        EngineResourceFile.ClearAll();
        TilesheetRegistry.Instance.Clear();
        Cycle.ClearAllAnimationCycles();
        Scene.ClearAllSceneLayers();
        SceneLayer.ClearAllSceneLayer();
        SpriteManager.Clear();
        SoundResourceManager.Instance.Dispose();
    }

    public void SaveToFile(string path, bool compress = false)
    {
        var json = JsonConvert.SerializeObject(this, JsonSerializerSettings);

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

        var result = JsonConvert.DeserializeObject<EngineState>(json, JsonSerializerSettings) ?? new EngineState();
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