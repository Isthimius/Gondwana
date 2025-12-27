using System.IO.Compression;
using System.Text.Json;
using System.Linq;
using Gondwana.Assets;
using Gondwana.Audio;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

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
        string json = ReadJsonFile(path, compressed);

        // Important: EngineState's collections are mostly getter-only proxies over registries,
        // so deserialize into a snapshot DTO with setters.
        var snapshot =
            JsonConvert.DeserializeObject<EngineStateSnapshot>(json, JsonSerializerSettings)
            ?? new EngineStateSnapshot();

        // Create a fresh engine state and clear global registries.
        var engineState = new EngineState();
        ApplySnapshot(engineState, snapshot, clearExisting: true, overwriteExisting: true);

        return engineState;
    }

    public static void MergeFromFile(
        string path,
        bool compressed = false,
        bool overwriteExisting = false)
    {
        string json = ReadJsonFile(path, compressed);

        var snapshot =
            JsonConvert.DeserializeObject<EngineStateSnapshot>(json, JsonSerializerSettings)
            ?? new EngineStateSnapshot();

        // Merge into the live engine state (registries)
        var target = Engine.Instance.State;
        ApplySnapshot(target, snapshot, clearExisting: false, overwriteExisting: overwriteExisting);
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

    private static string ReadJsonFile(string path, bool compressed)
    {
        if (compressed)
        {
            using var file = File.OpenRead(path);
            using var zip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(zip);
            return reader.ReadToEnd();
        }
        else
        {
            return File.ReadAllText(path);
        }
    }

    /// <summary>
    /// Single “apply” path used by both LoadFromFile and MergeFromFile.
    /// DRY: reads snapshot, loads assets, then merges/rehydrates everything in a consistent order.
    /// </summary>
    private static void ApplySnapshot(
        EngineState target,
        EngineStateSnapshot snapshot,
        bool clearExisting,
        bool overwriteExisting)
    {
        if (clearExisting)
            target.Clear(); // clears scenes/sprites/cycles/tilesheets/sounds (+ ValueBag)

        // 1) Load all asset files first (images/audio may be referenced by identifier).
        LoadAssetsFiles(snapshot.AssetsFiles ?? Enumerable.Empty<AssetsFile>());

        // 2) Audio: bulk-load packs first, then apply/rehydrate loose-file specs + per-key settings.
        MergeAudio(snapshot.AssetsFiles, snapshot.SoundResources, overwriteExisting);

        // 3) Tilesheets (rehydrate image bytes via AssetsFile or file path).
        MergeTilesheets(snapshot.Tilesheets, overwriteExisting);

        // 4) Cycles/scenes/sprites.
        MergeCycles(snapshot.Cycles, overwriteExisting);
        MergeScenes(snapshot.Scenes);
        MergeSprites(snapshot.Sprites);

        // 5) Extensible save data
        if (clearExisting)
        {
            // For a clean load, replace the bag wholesale.
            target.ValueBag = snapshot.ValueBag ?? new();
        }
        else
        {
            // For a merge, merge keys into the existing bag.
            MergeValueBag(target.ValueBag, snapshot.ValueBag, overwriteExisting);
        }
    }

    private static void LoadAssetsFiles(IEnumerable<AssetsFile> resourceFiles)
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

    private static Tilesheet? RebuildTilesheetFromSaved(string key, Tilesheet saved)
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
                "EngineState: Skipping tilesheet '{Key}' because it has no valid AssetIdentifier and no ImageFilePath.",
                key);
            return null;
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

        return rebuilt;
    }

    private static void MergeAudio(
        List<AssetsFile>? assetsFiles,
        Dictionary<string, AudioResource>? soundSpecs,
        bool overwriteExisting)
    {
        // 1) Load from asset packs
        if (assetsFiles is not null)
        {
            foreach (var af in assetsFiles)
            {
                if (overwriteExisting)
                {
                    foreach (var entry in af.GetAllEntries())
                    {
                        if (entry.AssetType == AssetTypes.Audio)
                            AudioResourceManager.Instance.Unload(entry.AssetName);
                    }
                }

                AudioResourceManager.Instance.LoadFromEngineResourceFile(af);
            }
        }

        // 2) Apply loose-file specs / overrides
        if (soundSpecs is null)
            return;

        foreach (var (key, spec) in soundSpecs)
        {
            if (AudioResourceManager.Instance.Contains(key))
            {
                if (!overwriteExisting)
                {
                    var existing = AudioResourceManager.Instance.Get(key);
                    if (existing is not null)
                    {
                        existing.Volume = spec.Volume;
                        existing.Pan = spec.Pan;
                        existing.IsLooping = spec.IsLooping;
                    }
                    continue;
                }

                AudioResourceManager.Instance.Unload(key);
            }

            // Ensure the audio spec is (re)created/registered in the manager.
            spec.ReloadIntoManager();
        }
    }

    private static void MergeTilesheets(
        Dictionary<string, Tilesheet>? tilesheets,
        bool overwriteExisting)
    {
        if (tilesheets is null || tilesheets.Count == 0)
            return;

        var registry = TilesheetRegistry.Instance.GetAll();

        foreach (var (key, saved) in tilesheets)
        {
            if (!overwriteExisting && registry.ContainsKey(key))
                continue;

            var rebuilt = RebuildTilesheetFromSaved(key, saved);
            if (rebuilt is null) continue;
            // ctor / registry side-effects already register the tilesheet
        }
    }

    private static void MergeCycles(Dictionary<string, Cycle>? cycles, bool overwriteExisting)
    {
        if (cycles is null || cycles.Count == 0)
            return;

        foreach (var (key, cycle) in cycles)
        {
            if (!overwriteExisting && Cycle._cycles.ContainsKey(key))
                continue;

            Cycle._cycles[key] = cycle;
        }
    }

    private static void MergeScenes(List<Scene>? scenes)
    {
        if (scenes is null || scenes.Count == 0)
            return;

        Scene._allScenes.AddRange(scenes);
    }

    private static void MergeSprites(List<Sprite>? sprites)
    {
        if (sprites is null || sprites.Count == 0)
            return;

        SpriteManager._spriteList.AddRange(sprites);
    }

    private static void MergeValueBag(
        TypedValueBag target,
        TypedValueBag? incoming,
        bool overwriteExisting)
    {
        if (incoming is null)
            return;

        // TypedValueBag stores data in a private Dictionary<string, JToken> named "_data".
        // We merge at the token level to preserve arbitrary structured values.
        static Dictionary<string, JToken> GetDataDict(TypedValueBag bag)
        {
            var field = typeof(TypedValueBag).GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(bag) is not Dictionary<string, JToken> dict)
                throw new InvalidOperationException("TypedValueBag internal storage field '_data' was not found or had an unexpected type.");

            return dict;
        }

        var targetData = GetDataDict(target);
        var incomingData = GetDataDict(incoming);

        foreach (var (key, token) in incomingData)
        {
            if (!overwriteExisting && targetData.ContainsKey(key))
                continue;

            targetData[key] = token.DeepClone();
        }
    }

    #endregion deserialization helpers
}
