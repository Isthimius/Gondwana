using System.IO.Compression;
using System.Text.Json;
using Gondwana.Assets;
using Gondwana.Audio;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Gondwana;

[JsonObject(IsReference = true)]
public sealed class EngineState
{
    public static JsonSerializerSettings JsonSerializerSettings { get; set; }
        = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
            PreserveReferencesHandling = PreserveReferencesHandling.All
        };

    [JsonProperty]
    public IEnumerable<AssetsFile> AssetsFiles => AssetsFile.AllAssetsFiles;

    [JsonProperty]
    public IDictionary<string, Tilesheet> Tilesheets => TilesheetRegistry.Instance.GetAll().ToDictionary();

    [JsonProperty]
    public Dictionary<string, Cycle> Cycles => Cycle._cycles;

    [JsonProperty]
    public List<Scene> Scenes => Scene._allScenes;

    [JsonProperty]
    public List<Sprite> Sprites => SpriteManager._spriteList;

    [JsonProperty]
    public Dictionary<string, AudioResource> SoundResources => AudioResourceManager.Instance.GetAll();

    /// <summary>
    /// Stores extensible, project-specific state data associated with this engine state.
    /// <para>
    /// The value bag allows games or engine extensions to persist arbitrary structured data
    /// (such as NPC state, quest progress, or custom subsystem data) without modifying the
    /// core <see cref="EngineState"/> schema.
    /// </para>
    /// <para>
    /// Values are accessed using strongly-typed <see cref="ValueKey{T}"/> instances to ensure
    /// compile-time safety while preserving a flexible serialized representation.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// // Define keys once (typically in a static class)
    /// static readonly ValueKey&lt;Dictionary&lt;string, int&gt;&gt; NpcHitPoints =
    ///     new("npc.hitpoints");
    ///
    /// // Store values
    /// engineState.ValueBag.Set(NpcHitPoints, new Dictionary&lt;string, int&gt;
    /// {
    ///     ["npc.guard"] = 12,
    ///     ["npc.merchant"] = 8
    /// });
    ///
    /// // Retrieve values
    /// var hp = engineState.ValueBag.Get(NpcHitPoints, new Dictionary&lt;string, int&gt;());
    /// </code>
    /// </example>
    [JsonProperty]
    public TypedValueBag ValueBag { get; set; } = new();

    internal void Clear()
    {
        AssetsFile.ClearAll();
        TilesheetRegistry.Instance.Clear();
        Cycle.ClearAllAnimationCycles();
        Scene.ClearAllScenes();
        SpriteManager.Clear();
        AudioResourceManager.Instance.Dispose();
        ValueBag.Clear();
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

        // Important: EngineState's collections are mostly getter-only proxies over registries,
        // so deserialize into a snapshot DTO with setters.
        var snapshot =
            JsonConvert.DeserializeObject<EngineStateSnapshot>(json, JsonSerializerSettings)
            ?? new EngineStateSnapshot();

        // Create a fresh engine state and clear global registries.
        var engineState = new EngineState();
        engineState.Clear(); // clears scenes/sprites/cycles/tilesheets/sounds

        // 1) Load all asset files first (images/audio may be referenced by identifier).
        LoadResourceFiles(snapshot.AssetsFiles ?? Enumerable.Empty<AssetsFile>());

        // bulk-load audio from asset packs first
        if (snapshot.AssetsFiles is not null)
        {
            foreach (var af in snapshot.AssetsFiles)
                AudioResourceManager.Instance.LoadFromEngineResourceFile(af);
        }

        // rehydrate “saved audio specs” (loose-file + any per-key settings)
        if (snapshot.SoundResources is not null)
        {
            foreach (var spec in snapshot.SoundResources.Values)
                spec.ReloadIntoManager();
        }

        // 2) Restore tilesheets (rehydrate image bytes via AssetsFile or file path).
        RestoreTilesheets(snapshot.Tilesheets);

        // 3) Restore cycles/scenes/sprites.
        RestoreCycles(snapshot.Cycles);
        RestoreScenes(snapshot.Scenes);
        RestoreSprites(snapshot.Sprites);

        // 4) Restore extensible save data
        engineState.ValueBag = snapshot.ValueBag ?? new();

        return engineState;
    }

    #region deserialization helpers

    private sealed class EngineStateSnapshot
    {
        [JsonProperty] public List<AssetsFile>? AssetsFiles { get; set; }
        [JsonProperty] public Dictionary<string, Tilesheet>? Tilesheets { get; set; }
        [JsonProperty] public Dictionary<string, Cycle>? Cycles { get; set; }
        [JsonProperty] public List<Scene>? Scenes { get; set; }
        [JsonProperty] public List<Sprite>? Sprites { get; set; }
        [JsonProperty] public Dictionary<string, AudioResource>? SoundResources { get; set; }
        [JsonProperty] public TypedValueBag? ValueBag { get; set; }
    }

    private static void LoadResourceFiles(IEnumerable<AssetsFile> resourceFiles)
    {
        // Replace raw deserialized resource files with proper loaded instances
        if (resourceFiles.Any())
        {
            foreach (var raw in resourceFiles)
            {
                try
                {
                    AssetsFile.LoadOrCreate(raw.FilePath, raw.Password, raw.UseEncryption);
                }
                catch (Exception ex)
                {
                    Engine.Logger.LogError(ex, "Failed to load resource file '{FilePath}'", raw.FilePath);
                    throw;
                }
            }
        }
    }

    private static void RestoreTilesheets(Dictionary<string, Tilesheet>? tilesheets)
    {
        TilesheetRegistry.Instance.Clear();
        if (tilesheets is null || tilesheets.Count == 0)
            return;

        foreach (var (key, saved) in tilesheets)
        {
            Tilesheet rebuilt;

            // 1) Rehydrate bitmap from AssetsFile entry (preferred) or file path (fallback)
            if (saved.AssetIdentifier is not null && saved.AssetIdentifier.IsValid)
            {
                var id = saved.AssetIdentifier;
                rebuilt = new Tilesheet(id.AssetsFile, id.AssetName);
            }
            else if (!string.IsNullOrWhiteSpace(saved.ImageFilePath) && File.Exists(saved.ImageFilePath))
            {
                rebuilt = new Tilesheet(saved.Name, saved.ImageFilePath);
            }
            else
            {
                Engine.Logger.LogWarning(
                    "EngineState.LoadFromFile: Skipping tilesheet '{Key}' because it has no valid AssetIdentifier and no ImageFilePath.",
                    key);
                continue;
            }

            // 2) Restore metadata (these trigger cache rebuild as needed)
            rebuilt.Name = saved.Name;
            rebuilt.TileSize = saved.TileSize;
            rebuilt.InitialOffsetX = saved.InitialOffsetX;
            rebuilt.InitialOffsetY = saved.InitialOffsetY;
            rebuilt.XPixelsBetweenTiles = saved.XPixelsBetweenTiles;
            rebuilt.YPixelsBetweenTiles = saved.YPixelsBetweenTiles;
            rebuilt.OverhangPixels = saved.OverhangPixels;

            // 3) Restore extensible tilesheet metadata
            rebuilt.ValueBag = new Dictionary<string, string>(saved.ValueBag);

            // 4) Reapply bitmap transforms recorded in the save.
            //
            // IMPORTANT: SkBitmap is not serialized, so these operations must be replayed here.
            // ApplyMask() also premultiplies alpha internally in your implementation.
            if (saved.MaskColor is not null)
            {
                rebuilt.ApplyMask(saved.MaskColor, saved.MaskTolerance);
            }
            else if (saved.Premultiplied)
            {
                // ApplyMask also premultiplies alpha internally,
                // so only call if Premultiplied and no MaskColor
                rebuilt.ApplyPremultiplyAlpha();
            }
        }
    }

    private static void RestoreCycles(Dictionary<string, Cycle>? cycles)
    {
        Cycle.ClearAllAnimationCycles();
        if (cycles is null || cycles.Count == 0) return;

        Cycle._cycles.Clear();
        foreach (var kvp in cycles)
            Cycle._cycles[kvp.Key] = kvp.Value;
    }

    private static void RestoreScenes(List<Scene>? scenes)
    {
        Scene.ClearAllScenes();
        if (scenes is null || scenes.Count == 0) return;

        Scene._allScenes.Clear();
        Scene._allScenes.AddRange(scenes);
    }

    private static void RestoreSprites(List<Sprite>? sprites)
    {
        SpriteManager.Clear();
        if (sprites is null || sprites.Count == 0) return;

        SpriteManager._spriteList.Clear();
        SpriteManager._spriteList.AddRange(sprites);
    }

    #endregion deserialization helpers
}