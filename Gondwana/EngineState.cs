using System.IO.Compression;
using Gondwana.Assets;
using Gondwana.Audio;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Gondwana;

/// <summary>
/// Represents the complete serializable state of the game engine, including assets, scenes, sprites,
/// audio resources, and custom data. This class provides functionality to save and load engine state
/// to/from files with support for selective state management, compression, and merge operations.
/// The state can be persisted as JSON and optionally compressed using GZip compression.
/// </summary>
[JsonObject(IsReference = true)]
public sealed class EngineState
{
    /// <summary>
    /// Gets or sets the JSON serializer settings used for serializing and deserializing engine state.
    /// These settings are configured to handle type information, preserve object references, and
    /// produce indented (human-readable) JSON output. The default configuration uses automatic type
    /// name handling and preserves all object references to maintain complex object graphs.
    /// </summary>
    public static JsonSerializerSettings JsonSerializerSettings { get; set; }
        = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
            PreserveReferencesHandling = PreserveReferencesHandling.All
        };

    /// <summary>
    /// Gets the collection of all loaded asset files (resource archives) currently registered with the engine.
    /// Asset files contain packed game resources such as images, audio, and data files that have been
    /// loaded into memory. This property provides a snapshot of the current asset files for serialization purposes.
    /// </summary>
    [JsonProperty]
    public IEnumerable<AssetsFile> AssetsFiles => AssetsFile.AllAssetsFiles;

    /// <summary>
    /// Gets a dictionary of all registered tilesheets, keyed by their unique identifiers.
    /// Tilesheets contain tile graphics and metadata used for rendering tile-based game worlds.
    /// This property provides access to the current tilesheet registry for serialization and state management.
    /// </summary>
    [JsonProperty]
    public IDictionary<string, Tilesheet> Tilesheets => TilesheetRegistry.Instance.GetAll().ToDictionary();

    /// <summary>
    /// Gets the dictionary of all registered animation cycles, keyed by their unique identifiers.
    /// Animation cycles define sprite animation sequences including frame data, timing, and playback behavior.
    /// This property provides direct access to the cycle registry for serialization purposes.
    /// </summary>
    [JsonProperty]
    public Dictionary<string, Cycle> Cycles => Cycle._cycles;

    /// <summary>
    /// Gets the list of all scenes currently registered with the engine.
    /// Scenes represent distinct game locations or levels, containing layers, entities, and scene-specific data.
    /// This property provides direct access to the scene collection for serialization and state management.
    /// </summary>
    [JsonProperty]
    public List<Scene> Scenes => Scene._allScenes;

    /// <summary>
    /// Gets the list of all active sprites currently managed by the sprite manager.
    /// Sprites are visual game entities that can be positioned, animated, and rendered on screen.
    /// This property provides direct access to the sprite collection for serialization purposes.
    /// </summary>
    [JsonProperty]
    public List<Sprite> Sprites => SpriteManager.Instance._spriteList;

    /// <summary>
    /// Gets the dictionary of all registered audio resources, keyed by their unique identifiers.
    /// Audio resources include sound effects, music tracks, and their associated playback settings
    /// such as volume, pan, and looping behavior. This property provides access to the audio resource
    /// registry for serialization and state management.
    /// </summary>
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
    /// Values are accessed using strongly-typed <see cref="ValueKey{T}"/> instances.
    /// </para>
    /// <para>
    /// *** NOTE: This property is NOT included in the serialized JSON. ***
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
    [JsonIgnore]
    public TypedValueBag ValueBag { get; set; } = new();

    /// <summary>
    /// Clears all engine state components, including assets, tilesheets, animation cycles, scenes,
    /// sprites, audio resources, and custom value bag data. This method resets the engine to a clean state
    /// by disposing or clearing all registered resources and collections. Use this when you need to
    /// completely reset the engine state, such as when loading a new game or returning to a main menu.
    /// </summary>
    internal void Clear()
    {
        AssetsFile.ClearAll();
        TilesheetRegistry.Instance.Clear();
        Cycle.ClearAllAnimationCycles();
        Scene.ClearAllScenes();
        SpriteManager.Instance.Clear();
        AudioResourceManager.Instance.Dispose();
        ValueBag.Clear();
    }

    /// <summary>
    /// Saves the current engine state to a file in JSON format with optional compression and selective
    /// state component inclusion. The saved state can later be loaded using <see cref="LoadFromFile"/>
    /// or merged using <see cref="MergeFromFile"/>.
    /// </summary>
    /// <param name="path">
    /// The file path where the engine state should be saved. The directory must exist and be writable.
    /// If the file already exists, it will be overwritten.
    /// </param>
    /// <param name="compress">
    /// If <c>true</c>, the JSON output will be compressed using GZip compression, reducing file size
    /// at the cost of additional processing time. If <c>false</c>, the JSON is written as plain text.
    /// Default is <c>false</c>.
    /// </param>
    /// <param name="parts">
    /// Specifies which parts of the engine state should be included in the saved file. Use bitwise
    /// flags from <see cref="EngineStateParts"/> to select specific components, or use
    /// <see cref="EngineStateParts.All"/> to save the complete state. Default is <see cref="EngineStateParts.All"/>.
    /// </param>
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
    /// Loads engine state from a file, replacing the current engine state with the saved state.
    /// This method clears existing state components before loading, providing a clean slate for the
    /// loaded data. Dependencies between state parts (such as tilesheets depending on asset files)
    /// are automatically handled.
    /// </summary>
    /// <param name="path">
    /// The file path from which to load the engine state. The file must exist and contain valid
    /// serialized engine state data in JSON format.
    /// </param>
    /// <param name="compressed">
    /// If <c>true</c>, the file is expected to be GZip-compressed and will be decompressed before
    /// deserialization. If <c>false</c>, the file is read as plain text JSON. Default is <c>false</c>.
    /// </param>
    /// <param name="parts">
    /// Specifies which parts of the engine state should be loaded from the file. Use bitwise flags
    /// from <see cref="EngineStateParts"/> to select specific components. Note that dependencies
    /// are automatically included (e.g., loading tilesheets will also load asset files).
    /// Default is <see cref="EngineStateParts.All"/>.
    /// </param>
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
    /// Loads engine state from a file and merges it with the current engine state, optionally
    /// overwriting existing items with matching identifiers. Unlike <see cref="LoadFromFile"/>,
    /// this method does not clear existing state before loading, allowing incremental state updates
    /// and data patching scenarios.
    /// </summary>
    /// <param name="path">
    /// The file path from which to load the engine state. The file must exist and contain valid
    /// serialized engine state data in JSON format.
    /// </param>
    /// <param name="compressed">
    /// If <c>true</c>, the file is expected to be GZip-compressed and will be decompressed before
    /// deserialization. If <c>false</c>, the file is read as plain text JSON. Default is <c>false</c>.
    /// </param>
    /// <param name="overwriteExisting">
    /// If <c>true</c>, items from the loaded state will replace existing items with the same
    /// identifiers (such as scene IDs or sprite nicknames). If <c>false</c>, existing items are
    /// preserved and only new items from the loaded state are added. Default is <c>false</c>.
    /// </param>
    /// <param name="parts">
    /// Specifies which parts of the engine state should be merged from the file. Use bitwise flags
    /// from <see cref="EngineStateParts"/> to select specific components. Dependencies are
    /// automatically included. Default is <see cref="EngineStateParts.All"/>.
    /// </param>
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
    /// Single "apply" path used by both LoadFromFile and MergeFromFile.
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
            LoadAssetsFiles(snapshot.AssetsFiles ?? Enumerable.Empty<AssetsFile>(), overwriteExisting);

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
    }

    private static void ClearSelected(EngineStateParts parts)
    {
        if (parts.HasFlag(EngineStateParts.AssetsFiles))
        {
            AssetsFile.ClearAll();
            SvgResourceManager.Instance.Clear();
        }

        if (parts.HasFlag(EngineStateParts.Tilesheets))
            TilesheetRegistry.Instance.Clear();

        if (parts.HasFlag(EngineStateParts.Cycles))
            Cycle.ClearAllAnimationCycles();

        if (parts.HasFlag(EngineStateParts.Scenes))
            Scene.ClearAllScenes();

        if (parts.HasFlag(EngineStateParts.Sprites))
            SpriteManager.Instance.Clear();

        if (parts.HasFlag(EngineStateParts.Audio))
            AudioResourceManager.Instance.Dispose();
    }

    private static void LoadAssetsFiles(IEnumerable<AssetsFile> resourceFiles, bool overwriteExisting)
    {
        // Replace raw deserialized resource files with proper loaded instances
        if (resourceFiles.Any())
        {
            foreach (var raw in resourceFiles)
            {
                try
                {
                    var loaded = AssetsFile.LoadOrCreate(raw.FilePath, raw.Password, raw.UseEncryption);

                    if (overwriteExisting)
                    {
                        foreach (var entry in loaded.GetAllEntries().Where(e => e.AssetType == AssetTypes.Svg))
                            SvgResourceManager.Instance.Unload(entry.AssetName);
                    }

                    SvgResourceManager.Instance.LoadFromEngineAssetsFile(loaded);
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

        // 2) Restore metadata.
        rebuilt.Name = saved.Name;

        // 3) Restore regions.
        //
        // IMPORTANT: Deserialized TilesheetRegion instances are saved-state specs.
        // They do not own a live Tilesheet reference after JSON deserialization.
        // Recreate live regions on the rebuilt tilesheet so each region is attached
        // to the rebuilt source bitmap and can build its own cache.
        foreach (var savedRegion in saved.Regions)
        {
            rebuilt.AddRegion(
                savedRegion.Name,
                savedRegion.Area,
                savedRegion.Spacing,
                savedRegion.TileSize,
                savedRegion.OverhangPixels);
        }

        // 4) Restore extensible tilesheet metadata
        rebuilt.ValueBag = saved.ValueBag.Clone();

        // 5) Reapply bitmap transforms recorded in the save.
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

                AudioResourceManager.Instance.LoadFromEngineAssetsFile(af);
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

            RebuildTilesheetFromSaved(key, saved);
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
        for (int i = 0; i < SpriteManager.Instance._spriteList.Count; i++)
        {
            var id = SpriteManager.Instance._spriteList[i].Nickname;
            if (!string.IsNullOrWhiteSpace(id) && !existingIndexById.ContainsKey(id))
                existingIndexById.Add(id, i);
        }

        var seenIncoming = new HashSet<string>(StringComparer.Ordinal);

        foreach (var incoming in sprites)
        {
            if (incoming is null)
                continue;

            if (string.IsNullOrWhiteSpace(incoming.Nickname))
                incoming.Nickname = Guid.NewGuid().ToString();

            if (!seenIncoming.Add(incoming.Nickname))
            {
                // Same-ID appears again in the incoming list: last one wins.
                overwriteExisting = true;
            }

            if (existingIndexById.TryGetValue(incoming.Nickname, out int existingIndex))
            {
                if (!overwriteExisting)
                    continue;

                SpriteManager.Instance._spriteList[existingIndex] = incoming;
            }
            else
            {
                existingIndexById[incoming.Nickname] = SpriteManager.Instance._spriteList.Count;
                SpriteManager.Instance.AddSprite(incoming);
            }
        }
    }

    #endregion deserialization helpers
}
