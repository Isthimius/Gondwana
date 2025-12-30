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

    /// <summary>
    /// Serializes the current engine state (or a selected subset of it) to disk as JSON.
    /// </summary>
    /// <param name="path">
    /// Destination file path for the saved engine state.
    /// </param>
    /// <param name="compress">
    /// If <c>true</c>, the JSON payload is written using GZip compression; otherwise it is written as plain text.
    /// </param>
    /// <param name="parts">
    /// Specifies which portions of the engine state should be included in the snapshot.
    /// </param>
    /// <remarks>
    /// This method captures a snapshot of the live engine registries and persists it in a format
    /// suitable for later restoration or merging.
    /// </remarks>
    public void SaveToFile(string path, bool compress = false, EngineStateParts parts = EngineStateParts.All)
    {
        var snapshot = BuildSnapshot(parts);

        var json = JsonConvert.SerializeObject(snapshot, JsonSerializerSettings);

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

    /// <summary>
    /// Loads an engine state snapshot from disk and replaces the current live engine state.
    /// </summary>
    /// <param name="path">
    /// Path to the engine state file to load.
    /// </param>
    /// <param name="compressed">
    /// Indicates whether the file is stored in compressed (GZip) form.
    /// </param>
    /// <param name="parts">
    /// Specifies which portions of the engine state should be applied from the snapshot.
    /// </param>
    /// <remarks>
    /// Existing engine state data is cleared before the snapshot is applied.
    /// This operation is destructive and should typically be used during startup or full resets.
    /// </remarks>
    public static void LoadFromFile(string path, bool compressed = false, EngineStateParts parts = EngineStateParts.All)
    {
        string json = ReadJsonFile(path, compressed);

        // Important: EngineState's collections are mostly getter-only proxies over registries,
        // so deserialize into a snapshot DTO with setters.
        var snapshot =
            JsonConvert.DeserializeObject<EngineStateSnapshot>(json, JsonSerializerSettings)
            ?? new EngineStateSnapshot();

        // Merge into the live engine state (registries)
        ApplySnapshot(snapshot, clearExisting: true, overwriteExisting: true, parts);
    }

    /// <summary>
    /// Loads an engine state snapshot from disk and merges it into the current live engine state.
    /// </summary>
    /// <param name="path">
    /// Path to the engine state file to merge.
    /// </param>
    /// <param name="compressed">
    /// Indicates whether the file is stored in compressed (GZip) form.
    /// </param>
    /// <param name="overwriteExisting">
    /// If <c>true</c>, existing entries in the engine state may be replaced by values from the snapshot.
    /// </param>
    /// <param name="parts">
    /// Specifies which portions of the engine state should be merged.
    /// </param>
    /// <remarks>
    /// Unlike <see cref="LoadFromFile"/>, this method preserves existing engine state data
    /// unless explicitly overwritten. It is intended for incremental updates, mod loading,
    /// or layered state composition.
    /// </remarks>
    public static void MergeFromFile(string path, bool compressed = false, bool overwriteExisting = false, EngineStateParts parts = EngineStateParts.All)
    {
        string json = ReadJsonFile(path, compressed);

        var snapshot =
            JsonConvert.DeserializeObject<EngineStateSnapshot>(json, JsonSerializerSettings)
            ?? new EngineStateSnapshot();

        // Merge into the live engine state (registries)
        ApplySnapshot(snapshot, clearExisting: false, overwriteExisting: overwriteExisting, parts);
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

    private static EngineStateParts NormalizeParts(EngineStateParts parts)
    {
        // Tilesheets and Audio may depend on AssetsFiles for AssetIdentifier.Data
        if (parts.HasFlag(EngineStateParts.Tilesheets) ||
            parts.HasFlag(EngineStateParts.Audio))
        {
            parts |= EngineStateParts.AssetsFiles;
        }

        return parts;
    }

    private EngineStateSnapshot BuildSnapshot(EngineStateParts parts)
    {
        return new EngineStateSnapshot
        {
            AssetsFiles = parts.HasFlag(EngineStateParts.AssetsFiles)
                ? AssetsFiles.ToList()
                : null,

            Tilesheets = parts.HasFlag(EngineStateParts.Tilesheets)
                ? Tilesheets.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                : null,

            Cycles = parts.HasFlag(EngineStateParts.Cycles)
                ? Cycles
                : null,

            Scenes = parts.HasFlag(EngineStateParts.Scenes)
                ? Scenes
                : null,

            Sprites = parts.HasFlag(EngineStateParts.Sprites)
                ? Sprites
                : null,

            SoundResources = parts.HasFlag(EngineStateParts.Audio)
                ? SoundResources
                : null,

            ValueBag = parts.HasFlag(EngineStateParts.ValueBag)
                ? ValueBag
                : null
        };
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
        EngineStateSnapshot snapshot,
        bool clearExisting,
        bool overwriteExisting,
        EngineStateParts parts)
    {
        parts = NormalizeParts(parts);

        // clear only what we're about to load.
        if (clearExisting)
            ClearSelected(parts);

        if (parts.HasFlag(EngineStateParts.AssetsFiles))
            LoadAssetsFiles(snapshot.AssetsFiles ?? Enumerable.Empty<AssetsFile>());

        if (parts.HasFlag(EngineStateParts.Audio))
            MergeAudio(snapshot.AssetsFiles, snapshot.SoundResources, overwriteExisting);

        if (parts.HasFlag(EngineStateParts.Tilesheets))
            MergeTilesheets(snapshot.Tilesheets, overwriteExisting);

        if (parts.HasFlag(EngineStateParts.Cycles))
            MergeCycles(snapshot.Cycles, overwriteExisting);

        if (parts.HasFlag(EngineStateParts.Scenes))
            MergeScenes(snapshot.Scenes, overwriteExisting);

        if (parts.HasFlag(EngineStateParts.Sprites))
            MergeSprites(snapshot.Sprites, overwriteExisting);

        if (parts.HasFlag(EngineStateParts.ValueBag))
            Engine.Instance.State.ValueBag.MergeFrom(snapshot.ValueBag, overwriteExisting);
    }

    private static void ClearSelected(EngineStateParts parts)
    {
        if (parts.HasFlag(EngineStateParts.AssetsFiles))
            AssetsFile.ClearAll();

        if (parts.HasFlag(EngineStateParts.Tilesheets))
            TilesheetRegistry.Instance.Clear();

        if (parts.HasFlag(EngineStateParts.Cycles))
            Cycle.ClearAllAnimationCycles();

        if (parts.HasFlag(EngineStateParts.Scenes))
            Scene.ClearAllScenes();

        if (parts.HasFlag(EngineStateParts.Sprites))
            SpriteManager.Clear();

        if (parts.HasFlag(EngineStateParts.Audio))
            AudioResourceManager.Instance.Dispose();

        if (parts.HasFlag(EngineStateParts.ValueBag))
            Engine.Instance.State.ValueBag.Clear();
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
        rebuilt.ValueBag = saved.ValueBag.Clone();

        // 4) Reapply bitmap transforms recorded in the save.
        //
        // IMPORTANT: SkBitmap is not serialized, so these operations must be replayed here.
        // ApplyMask() also premultiplies alpha internally in the implementation.
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

    private static void MergeScenes(List<Scene>? scenes, bool overwriteExisting)
    {
        if (scenes is null || scenes.Count == 0)
            return;

        // Index existing scenes by ID (case-sensitive; change if you prefer)
        var existingIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < Scene._allScenes.Count; i++)
        {
            var id = Scene._allScenes[i].ID;
            if (!string.IsNullOrWhiteSpace(id) && !existingIndexById.ContainsKey(id))
                existingIndexById.Add(id, i);
        }

        // Avoid duplicating the same incoming ID twice (keeps last one)
        var seenIncoming = new HashSet<string>(StringComparer.Ordinal);

        foreach (var incoming in scenes)
        {
            if (incoming is null)
                continue;

            // Ensure ID exists (important if something created scenes without IDs)
            if (string.IsNullOrWhiteSpace(incoming.ID))
                incoming.ID = Guid.NewGuid().ToString();

            // If the incoming list contains the same ID multiple times, last one wins.
            if (!seenIncoming.Add(incoming.ID))
            {
                // Replace the previously added/replaced incoming with this one:
                // easiest way: treat it as overwriteExisting=true for that ID
                overwriteExisting = true;
            }

            if (existingIndexById.TryGetValue(incoming.ID, out int existingIndex))
            {
                if (!overwriteExisting)
                    continue;

                Scene._allScenes[existingIndex] = incoming;
            }
            else
            {
                existingIndexById[incoming.ID] = Scene._allScenes.Count;
                Scene._allScenes.Add(incoming);
            }
        }
    }

    private static void MergeSprites(List<Sprite>? sprites, bool overwriteExisting)
    {
        if (sprites is null || sprites.Count == 0)
            return;

        var existingIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < SpriteManager._spriteList.Count; i++)
        {
            var id = SpriteManager._spriteList[i].ID;
            if (!string.IsNullOrWhiteSpace(id) && !existingIndexById.ContainsKey(id))
                existingIndexById.Add(id, i);
        }

        var seenIncoming = new HashSet<string>(StringComparer.Ordinal);

        foreach (var incoming in sprites)
        {
            if (incoming is null)
                continue;

            if (string.IsNullOrWhiteSpace(incoming.ID))
                incoming.ID = Guid.NewGuid().ToString();

            if (!seenIncoming.Add(incoming.ID))
            {
                // Same-ID appears again in the incoming list: last one wins.
                overwriteExisting = true;
            }

            if (existingIndexById.TryGetValue(incoming.ID, out int existingIndex))
            {
                if (!overwriteExisting)
                    continue;

                SpriteManager._spriteList[existingIndex] = incoming;
            }
            else
            {
                existingIndexById[incoming.ID] = SpriteManager._spriteList.Count;
                SpriteManager._spriteList.Add(incoming);
            }
        }
    }

    #endregion deserialization helpers
}
