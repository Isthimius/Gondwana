using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Gondwana.Assets;
using Gondwana.Audio;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;

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
    /// Represents the serialized tilesheet data captured for a single tilesheet entry.
    /// </summary>
    private sealed class TilesheetStateEntry
    {
        /// <summary>
        /// Gets or sets the path to the external GTS file used for this tilesheet entry, when applicable.
        /// </summary>
        [JsonProperty]
        public string? GtsPath { get; set; }

        /// <summary>
        /// Gets or sets the in-memory tilesheet definition associated with this entry.
        /// </summary>
        [JsonProperty]
        public TilesheetDefinition? Definition { get; set; }
    }


    /// <summary>
    /// Represents one serialized animation entry, either inline as a GANI definition or by
    /// reference to an external .gani file.
    /// </summary>
    [JsonConverter(typeof(AnimationStateEntryConverter))]
    private sealed class AnimationStateEntry
    {
        /// <summary>
        /// Gets or sets the path to the external GANI file used for this animation entry.
        /// </summary>
        [JsonProperty]
        public string? GaniPath { get; set; }

        /// <summary>
        /// Gets or sets the inline GANI definition associated with this entry.
        /// </summary>
        [JsonProperty]
        public AnimationDefinition? Definition { get; set; }

        /// <summary>
        /// Holds the raw JSON shape used by pre-GANI EngineState files.
        /// It is converted after tilesheets have been restored so Frame references can resolve.
        /// </summary>
        [JsonIgnore]
        public JObject? LegacyJson { get; set; }
    }

    /// <summary>
    /// Reads current GANI state entries while retaining legacy raw Cycle JSON for
    /// dependency-ordered conversion after tilesheets are available.
    /// </summary>
    private sealed class AnimationStateEntryConverter : JsonConverter<AnimationStateEntry>
    {
        public override AnimationStateEntry? ReadJson(
            JsonReader reader,
            Type objectType,
            AnimationStateEntry? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var obj = JObject.Load(reader);

            if (obj.Property(nameof(AnimationStateEntry.GaniPath), StringComparison.OrdinalIgnoreCase) is not null ||
                obj.Property(nameof(AnimationStateEntry.Definition), StringComparison.OrdinalIgnoreCase) is not null)
            {
                return new AnimationStateEntry
                {
                    GaniPath = obj.GetValue(
                        nameof(AnimationStateEntry.GaniPath),
                        StringComparison.OrdinalIgnoreCase)?.ToObject<string>(serializer),
                    Definition = obj.GetValue(
                        nameof(AnimationStateEntry.Definition),
                        StringComparison.OrdinalIgnoreCase)?.ToObject<AnimationDefinition>(serializer)
                };
            }

            // Do not deserialize a legacy Cycle yet. Its Frame values require the
            // referenced tilesheets, which are restored later in ApplySnapshot().
            return new AnimationStateEntry
            {
                LegacyJson = obj
            };
        }

        public override void WriteJson(
            JsonWriter writer,
            AnimationStateEntry? value,
            JsonSerializer serializer) =>
            throw new NotSupportedException();

        public override bool CanWrite => false;
    }

    /// <summary>
    /// Represents one serialized scene entry, either inline as a GSCN definition or by
    /// reference to an external .gscn file.
    /// </summary>
    [JsonConverter(typeof(SceneStateEntryConverter))]
    private sealed class SceneStateEntry
    {
        /// <summary>
        /// Gets or sets the path to the external GSCN file used for this scene entry, when applicable.
        /// </summary>
        [JsonProperty]
        public string? GscnPath { get; set; }

        /// <summary>
        /// Gets or sets the inline GSCN definition associated with this entry.
        /// </summary>
        [JsonProperty]
        public SceneDefinition? Definition { get; set; }

        /// <summary>
        /// Holds a legacy raw Scene while an older EngineState file is being applied.
        /// It is never emitted by new saves.
        /// </summary>
        [JsonIgnore]
        public Scene? LegacyScene { get; set; }
    }

    /// <summary>
    /// Reads both the current SceneStateEntry shape and legacy EngineState files that
    /// stored raw Scene objects directly in the Scenes collection.
    /// </summary>
    private sealed class SceneStateEntryConverter : JsonConverter<SceneStateEntry>
    {
        public override SceneStateEntry? ReadJson(
            JsonReader reader,
            Type objectType,
            SceneStateEntry? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var obj = JObject.Load(reader);

            if (obj.Property(nameof(SceneStateEntry.GscnPath), StringComparison.OrdinalIgnoreCase) is not null ||
                obj.Property(nameof(SceneStateEntry.Definition), StringComparison.OrdinalIgnoreCase) is not null)
            {
                return new SceneStateEntry
                {
                    GscnPath = obj.GetValue(
                        nameof(SceneStateEntry.GscnPath),
                        StringComparison.OrdinalIgnoreCase)?.ToObject<string>(serializer),
                    Definition = obj.GetValue(
                        nameof(SceneStateEntry.Definition),
                        StringComparison.OrdinalIgnoreCase)?.ToObject<SceneDefinition>(serializer)
                };
            }

            // Backward compatibility: pre-GSCN EngineState files stored Scene objects
            // directly in the Scenes list. Keep the deserialized instance intact so any
            // preserved references from other legacy objects still resolve to it.
            var legacyScene = obj.ToObject<Scene>(serializer)
                ?? throw new JsonSerializationException(
                    "Legacy EngineState scene entry deserialized to null.");

            return new SceneStateEntry
            {
                LegacyScene = legacyScene
            };
        }

        public override void WriteJson(
            JsonWriter writer,
            SceneStateEntry? value,
            JsonSerializer serializer) =>
            throw new NotSupportedException();

        public override bool CanWrite => false;
    }

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
            PreserveReferencesHandling = PreserveReferencesHandling.All,
            Converters =
            {
                new FrameJsonConverter()
            }
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
    public IDictionary<string, TilesheetDefinition> Tilesheets => CaptureTilesheetDefinitions(baseDirectory: null);

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
    /// <param name="separateGtsFiles">
    /// If <c>true</c>, tilesheet definitions are written to separate .gts files and
    /// referenced from the engine-state file.
    /// </param>
    /// <param name="parts">
    /// Specifies which parts of the engine state should be included in the saved file. Use bitwise
    /// flags from <see cref="EngineStateParts"/> to select specific components, or use
    /// <see cref="EngineStateParts.All"/> to save the complete state. Default is <see cref="EngineStateParts.All"/>.
    /// </param>
    /// <param name="separateGscnFiles">
    /// If <c>true</c>, scene definitions are written to separate .gscn files and
    /// referenced from the engine-state file. If <c>false</c>, GSCN definitions are
    /// embedded inline in the engine-state JSON.
    /// </param>
    /// <param name="separateGaniFiles">
    /// If <c>true</c>, animation definitions are written to separate .gani files and
    /// referenced from the engine-state file. If <c>false</c>, GANI definitions are
    /// embedded inline in the engine-state JSON.
    /// </param>
    public void SaveToFile(string path,
                           bool compress = false,
                           bool separateGtsFiles = false,
                           EngineStateParts parts = EngineStateParts.All,
                           bool separateGscnFiles = false,
                           bool separateGaniFiles = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Engine state path must be a non-empty string.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        var baseDirectory = Path.GetDirectoryName(fullPath);

        var snapshot = BuildSnapshot(
            parts,
            baseDirectory,
            fullPath,
            separateGtsFiles,
            separateGscnFiles,
            separateGaniFiles);

        var json = JsonConvert.SerializeObject(snapshot, JsonSerializerSettings);

        if (compress)
        {
            using var file = File.Create(fullPath);
            using var zip = new GZipStream(file, CompressionMode.Compress);
            using var writer = new StreamWriter(zip);
            writer.Write(json);
        }
        else
        {
            File.WriteAllText(fullPath, json);
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
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Engine state path must be a non-empty string.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        var baseDirectory = Path.GetDirectoryName(fullPath);

        string json = ReadJsonFile(fullPath, compressed);

        // Important: EngineState's collections are mostly getter-only proxies over registries,
        // so deserialize into a snapshot DTO with setters.
        var snapshot =
            JsonConvert.DeserializeObject<EngineStateSnapshot>(json, JsonSerializerSettings)
            ?? new EngineStateSnapshot();

        // Merge into the live engine state (registries)
        ApplySnapshot(snapshot, clearExisting: true, overwriteExisting: true, parts, baseDirectory);
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
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Engine state path must be a non-empty string.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        var baseDirectory = Path.GetDirectoryName(fullPath);

        string json = ReadJsonFile(fullPath, compressed);

        var snapshot =
            JsonConvert.DeserializeObject<EngineStateSnapshot>(json, JsonSerializerSettings)
            ?? new EngineStateSnapshot();

        // Merge into the live engine state (registries)
        ApplySnapshot(snapshot, clearExisting: false, overwriteExisting: overwriteExisting, parts, baseDirectory);
    }

    #region deserialization helpers

    private sealed class EngineStateSnapshot
    {
        [JsonProperty] public List<AssetsFile>? AssetsFiles { get; set; }
        [JsonProperty] public Dictionary<string, TilesheetStateEntry>? Tilesheets { get; set; }
        [JsonProperty] public Dictionary<string, AnimationStateEntry>? Cycles { get; set; }
        [JsonProperty] public List<SceneStateEntry>? Scenes { get; set; }
        [JsonProperty] public List<Sprite>? Sprites { get; set; }
        [JsonProperty] public Dictionary<string, AudioResource>? SoundResources { get; set; }
    }

    private static EngineStateParts NormalizeParts(EngineStateParts parts)
    {
        // GANI definitions resolve Frame references through the TilesheetRegistry.
        if (parts.HasFlag(EngineStateParts.Cycles))
            parts |= EngineStateParts.Tilesheets;

        // Tilesheets and Audio may depend on AssetsFiles for AssetIdentifier.Data.
        if (parts.HasFlag(EngineStateParts.Tilesheets) ||
            parts.HasFlag(EngineStateParts.Audio))
        {
            parts |= EngineStateParts.AssetsFiles;
        }

        return parts;
    }

    private EngineStateSnapshot BuildSnapshot(EngineStateParts parts,
                                              string? baseDirectory,
                                              string engineStatePath,
                                              bool separateGtsFiles,
                                              bool separateGscnFiles,
                                              bool separateGaniFiles)
    {
        return new EngineStateSnapshot
        {
            AssetsFiles = parts.HasFlag(EngineStateParts.AssetsFiles)
                ? AssetsFiles.ToList()
                : null,

            Tilesheets = parts.HasFlag(EngineStateParts.Tilesheets)
                ? CaptureTilesheetEntries(
                    baseDirectory,
                    engineStatePath,
                    separateGtsFiles)
                : null,

            Cycles = parts.HasFlag(EngineStateParts.Cycles)
                ? CaptureAnimationEntries(
                    baseDirectory,
                    engineStatePath,
                    separateGaniFiles)
                : null,

            Scenes = parts.HasFlag(EngineStateParts.Scenes)
                ? CaptureSceneEntries(
                    baseDirectory,
                    engineStatePath,
                    separateGscnFiles)
                : null,

            Sprites = parts.HasFlag(EngineStateParts.Sprites)
                ? Sprites
                : null,

            SoundResources = parts.HasFlag(EngineStateParts.Audio)
                ? SoundResources
                : null,
        };
    }

    private static Dictionary<string, AnimationStateEntry> CaptureAnimationEntries(
        string? baseDirectory,
        string engineStatePath,
        bool separateGaniFiles)
    {
        var result = new Dictionary<string, AnimationStateEntry>(StringComparer.Ordinal);
        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Work from a stable registry snapshot in case definitions are changed while
        // the EngineState snapshot is being assembled.
        foreach (var (key, cycle) in Cycle._cycles.ToList())
        {
            if (cycle is null)
                continue;

            if (separateGaniFiles)
            {
                var ganiDirectory = GetAnimationStateDirectory(engineStatePath);
                Directory.CreateDirectory(ganiDirectory);

                var baseFileName = SanitizeFileName(key);
                var ganiFileName = $"{baseFileName}.gani";
                int suffix = 2;

                while (!usedFileNames.Add(ganiFileName))
                    ganiFileName = $"{baseFileName}_{suffix++}.gani";

                var ganiFullPath = Path.Combine(ganiDirectory, ganiFileName);

                // Use the standalone serializer so .gani files do not acquire
                // EngineState-specific $id/$ref metadata.
                AnimationDefinitionSerializer.Save(ganiFullPath, cycle);

                result[key] = new AnimationStateEntry
                {
                    GaniPath = MakeRelativePath(ganiFullPath, baseDirectory)
                };
            }
            else
            {
                result[key] = new AnimationStateEntry
                {
                    Definition = AnimationDefinitionSerializer.FromCycle(cycle)
                };
            }
        }

        return result;
    }

    private static List<SceneStateEntry> CaptureSceneEntries(
        string? baseDirectory,
        string engineStatePath,
        bool separateGscnFiles)
    {
        var result = new List<SceneStateEntry>();
        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Work from a stable snapshot. Scene construction/disposal may occur on
        // other threads while a state file is being assembled.
        foreach (var scene in Scene._allScenes.ToList())
        {
            if (scene is null)
                continue;

            if (separateGscnFiles)
            {
                var gscnDirectory = GetSceneStateDirectory(engineStatePath);
                Directory.CreateDirectory(gscnDirectory);

                var baseFileName = SanitizeFileName(scene.ID);
                var gscnFileName = $"{baseFileName}.gscn";
                int suffix = 2;

                while (!usedFileNames.Add(gscnFileName))
                    gscnFileName = $"{baseFileName}_{suffix++}.gscn";

                var gscnFullPath = Path.Combine(gscnDirectory, gscnFileName);

                // Use the standalone GSCN serializer so the external file remains a
                // clean scene definition without EngineState $id/$ref metadata.
                SceneDefinitionSerializer.Save(gscnFullPath, scene);

                result.Add(new SceneStateEntry
                {
                    GscnPath = MakeRelativePath(
                        gscnFullPath,
                        baseDirectory)
                });
            }
            else
            {
                result.Add(new SceneStateEntry
                {
                    Definition = SceneDefinitionSerializer.FromScene(scene)
                });
            }
        }

        return result;
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
        EngineStateParts parts,
        string? baseDirectory)
    {
        parts = NormalizeParts(parts);

        // Legacy raw objects may register themselves while the snapshot DTO is
        // deserialized. Detach those incoming instances before clearing or merging so
        // they are treated as snapshot data rather than pre-existing live state.
        DetachLegacySnapshotScenes(snapshot.Scenes);
        DetachSnapshotSprites(snapshot.Sprites);

        // clear only what we're about to load.
        if (clearExisting)
            ClearSelected(parts);

        if (parts.HasFlag(EngineStateParts.AssetsFiles))
            LoadAssetsFiles(snapshot.AssetsFiles ?? Enumerable.Empty<AssetsFile>(), overwriteExisting);

        if (parts.HasFlag(EngineStateParts.Audio))
            MergeAudio(snapshot.AssetsFiles, snapshot.SoundResources, overwriteExisting);

        if (parts.HasFlag(EngineStateParts.Tilesheets))
            MergeTilesheets(snapshot.Tilesheets, overwriteExisting, baseDirectory);

        if (parts.HasFlag(EngineStateParts.Cycles))
            MergeAnimations(snapshot.Cycles, overwriteExisting, baseDirectory);

        if (parts.HasFlag(EngineStateParts.Scenes))
            MergeScenes(snapshot.Scenes, overwriteExisting, baseDirectory);

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
                        existing.PlaybackSpeed = spec.PlaybackSpeed;
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

    private static Dictionary<string, TilesheetDefinition> CaptureTilesheetDefinitions(string? baseDirectory)
    {
        return TilesheetRegistry.Instance
            .GetAll()
            .ToDictionary(
                kvp => kvp.Key,
                kvp => TilesheetDefinitionSerializer.FromTilesheet(
                    kvp.Value,
                    baseDirectory,
                    makePathsRelative: !string.IsNullOrWhiteSpace(baseDirectory)));
    }

    private static Dictionary<string, TilesheetStateEntry> CaptureTilesheetEntries(
        string? baseDirectory,
        string engineStatePath,
        bool separateGtsFiles)
    {
        var result = new Dictionary<string, TilesheetStateEntry>(StringComparer.Ordinal);

        foreach (var (key, tilesheet) in TilesheetRegistry.Instance.GetAll())
        {
            if (separateGtsFiles)
            {
                var gtsDirectory = GetTilesheetStateDirectory(engineStatePath);
                Directory.CreateDirectory(gtsDirectory);

                var gtsFileName = $"{SanitizeFileName(key)}.gts";
                var gtsFullPath = Path.Combine(gtsDirectory, gtsFileName);

                // Important:
                // Save the GTS using the GTS serializer, not EngineState.JsonSerializerSettings.
                // This avoids $id/$values noise in the .gts file.
                TilesheetDefinitionSerializer.Save(
                    gtsFullPath,
                    tilesheet,
                    makePathsRelative: true);

                var gtsPathForState = MakeRelativePath(
                    gtsFullPath,
                    baseDirectory);

                result[key] = new TilesheetStateEntry
                {
                    GtsPath = gtsPathForState
                };
            }
            else
            {
                result[key] = new TilesheetStateEntry
                {
                    Definition = TilesheetDefinitionSerializer.FromTilesheet(
                        tilesheet,
                        baseDirectory,
                        makePathsRelative: !string.IsNullOrWhiteSpace(baseDirectory))
                };
            }
        }

        return result;
    }

    private static string GetTilesheetStateDirectory(string engineStatePath)
    {
        var directory = Path.GetDirectoryName(engineStatePath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(engineStatePath);

        return Path.Combine(directory, $"{fileName}.tilesheets");
    }


    private static string GetAnimationStateDirectory(string engineStatePath)
    {
        var directory = Path.GetDirectoryName(engineStatePath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(engineStatePath);

        return Path.Combine(directory, $"{fileName}.animations");
    }


    private static string GetSceneStateDirectory(string engineStatePath)
    {
        var directory = Path.GetDirectoryName(engineStatePath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(engineStatePath);

        return Path.Combine(directory, $"{fileName}.scenes");
    }

    private static string MakeRelativePath(string path, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            return path;

        return Path.GetRelativePath(
            Path.GetFullPath(baseDirectory),
            Path.GetFullPath(path));
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Guid.NewGuid().ToString("N");

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(
            value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());

        return string.IsNullOrWhiteSpace(sanitized)
            ? Guid.NewGuid().ToString("N")
            : sanitized;
    }

    private static void MergeTilesheets(
        Dictionary<string, TilesheetStateEntry>? tilesheets,
        bool overwriteExisting,
        string? baseDirectory)
    {
        if (tilesheets is null || tilesheets.Count == 0)
            return;

        var registry = TilesheetRegistry.Instance.GetAll();

        foreach (var (key, entry) in tilesheets)
        {
            if (entry is null)
                continue;

            var existingKey = key;

            if (!overwriteExisting && registry.ContainsKey(existingKey))
                continue;

            Tilesheet rebuilt;

            if (!string.IsNullOrWhiteSpace(entry.GtsPath))
            {
                var gtsPath = ResolvePath(entry.GtsPath, baseDirectory);

                rebuilt = TilesheetFactory.FromDefinitionFile(gtsPath);
            }
            else if (entry.Definition is not null)
            {
                if (string.IsNullOrWhiteSpace(entry.Definition.Name))
                    entry.Definition.Name = key;

                rebuilt = TilesheetFactory.FromDefinition(
                    entry.Definition,
                    baseDirectory);
            }
            else
            {
                throw new InvalidDataException(
                    $"Tilesheet state entry '{key}' does not contain a GTS path or inline definition.");
            }

            TilesheetRegistry.Instance.Register(
                rebuilt,
                disposeReplaced: overwriteExisting);
        }
    }

    private static string ResolvePath(string path, string? baseDirectory)
    {
        if (Path.IsPathRooted(path))
            return path;

        if (string.IsNullOrWhiteSpace(baseDirectory))
            return path;

        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private static void MergeAnimations(
        Dictionary<string, AnimationStateEntry>? animations,
        bool overwriteExisting,
        string? baseDirectory)
    {
        if (animations is null || animations.Count == 0)
            return;

        var legacyIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, entry) in animations)
        {
            var id = entry?.LegacyJson?.GetValue(
                "$id",
                StringComparison.OrdinalIgnoreCase)?.Value<string>();

            if (!string.IsNullOrWhiteSpace(id))
                legacyIds[id] = key;
        }

        var definitions = new List<AnimationDefinition>();

        foreach (var (key, entry) in animations)
        {
            if (entry is null)
                continue;

            if (!overwriteExisting && Cycle._cycles.ContainsKey(key))
                continue;

            AnimationDefinition definition;

            if (!string.IsNullOrWhiteSpace(entry.GaniPath))
            {
                var ganiPath = ResolvePath(entry.GaniPath, baseDirectory);
                definition = AnimationDefinitionSerializer.Load(ganiPath);
            }
            else if (entry.Definition is not null)
            {
                definition = entry.Definition;
            }
            else if (entry.LegacyJson is not null)
            {
                definition = ConvertLegacyCycleDefinition(
                    key,
                    entry.LegacyJson,
                    legacyIds);
            }
            else
            {
                throw new InvalidDataException(
                    $"Animation state entry '{key}' does not contain a GANI path or inline definition.");
            }

            if (string.IsNullOrWhiteSpace(definition.Key))
            {
                definition.Key = key;
            }
            else if (!string.Equals(definition.Key, key, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Animation state entry '{key}' contains definition key '{definition.Key}'.");
            }

            definitions.Add(definition);
        }

        // Validate next-cycle identities before mutating the registry. Two-phase
        // materialization allows circular transitions such as idle -> blink -> idle.
        var availableKeys = new HashSet<string>(
            Cycle._cycles.Keys,
            StringComparer.Ordinal);

        foreach (var definition in definitions)
            availableKeys.Add(definition.Key);

        foreach (var definition in definitions)
        {
            if (!string.IsNullOrWhiteSpace(definition.NextCycleKey) &&
                !string.Equals(
                    definition.NextCycleKey,
                    definition.Key,
                    StringComparison.Ordinal) &&
                !availableKeys.Contains(definition.NextCycleKey))
            {
                throw new InvalidDataException(
                    $"GANI animation '{definition.Key}' references next cycle '{definition.NextCycleKey}', but no matching animation is available.");
            }
        }

        var materialized = new List<(Cycle Cycle, AnimationDefinition Definition)>();

        foreach (var definition in definitions)
        {
            var cycle = AnimationDefinitionSerializer.MaterializeCycle(definition);
            materialized.Add((cycle, definition));
        }

        foreach (var (cycle, definition) in materialized)
            AnimationDefinitionSerializer.ApplyNextCycle(cycle, definition);
    }

    private static AnimationDefinition ConvertLegacyCycleDefinition(
        string stateKey,
        JObject legacyCycle,
        IReadOnlyDictionary<string, string> legacyIds)
    {
        if (legacyCycle.GetValue("$ref", StringComparison.OrdinalIgnoreCase) is JToken rootReference)
        {
            var referenceId = rootReference.Value<string>();
            if (!string.IsNullOrWhiteSpace(referenceId) &&
                legacyIds.TryGetValue(referenceId, out var referencedKey))
            {
                throw new InvalidDataException(
                    $"Legacy animation state entry '{stateKey}' is a reference to '{referencedKey}' rather than a standalone cycle.");
            }
        }

        var sequence = legacyCycle.GetValue(
            nameof(Cycle.Sequence),
            StringComparison.OrdinalIgnoreCase) as JObject
            ?? throw new InvalidDataException(
                $"Legacy animation state entry '{stateKey}' does not contain a FrameSequence.");

        var cycleType = CycleType.Simple;
        var cycleTypeToken = sequence.GetValue(
            nameof(FrameSequence.SequenceCycleType),
            StringComparison.OrdinalIgnoreCase);

        if (cycleTypeToken is not null && cycleTypeToken.Type != JTokenType.Null)
            cycleType = cycleTypeToken.ToObject<CycleType>();

        var frameListToken = sequence.GetValue(
            "frameList",
            StringComparison.OrdinalIgnoreCase);

        var frameTokens = frameListToken switch
        {
            JArray array => array,
            JObject wrapper when wrapper.GetValue(
                "$values",
                StringComparison.OrdinalIgnoreCase) is JArray array => array,
            null => new JArray(),
            _ => throw new InvalidDataException(
                $"Legacy animation state entry '{stateKey}' has an invalid frame list.")
        };

        var frames = new List<AnimationFrameDefinition>();
        foreach (var token in frameTokens)
        {
            if (token is not JObject frame)
            {
                throw new InvalidDataException(
                    $"Legacy animation state entry '{stateKey}' contains an invalid frame.");
            }

            var tilesheetToken = frame.GetValue(
                "tilesheet",
                StringComparison.OrdinalIgnoreCase);

            string? tilesheetName = tilesheetToken?.Type switch
            {
                JTokenType.String => tilesheetToken.Value<string>(),
                JTokenType.Object => ((JObject)tilesheetToken).GetValue(
                    "Name",
                    StringComparison.OrdinalIgnoreCase)?.Value<string>(),
                _ => null
            };

            frames.Add(new AnimationFrameDefinition
            {
                Tilesheet = tilesheetName ?? string.Empty,
                RegionName = frame.GetValue(
                    "regionName",
                    StringComparison.OrdinalIgnoreCase)?.Value<string>()
                    ?? TilesheetRegion.DefaultRegionName,
                XTile = frame.GetValue(
                    "xTile",
                    StringComparison.OrdinalIgnoreCase)?.Value<int>() ?? 0,
                YTile = frame.GetValue(
                    "yTile",
                    StringComparison.OrdinalIgnoreCase)?.Value<int>() ?? 0
            });
        }

        string? nextCycleKey = stateKey;
        var nextCycleToken = legacyCycle.GetValue(
            nameof(Cycle.NextCycle),
            StringComparison.OrdinalIgnoreCase);

        if (nextCycleToken?.Type == JTokenType.Null)
        {
            nextCycleKey = null;
        }
        else if (nextCycleToken is JObject nextCycleObject)
        {
            var referenceId = nextCycleObject.GetValue(
                "$ref",
                StringComparison.OrdinalIgnoreCase)?.Value<string>();

            if (!string.IsNullOrWhiteSpace(referenceId))
            {
                if (!legacyIds.TryGetValue(referenceId, out nextCycleKey))
                {
                    throw new InvalidDataException(
                        $"Legacy animation state entry '{stateKey}' references unknown cycle object '{referenceId}'.");
                }
            }
            else
            {
                nextCycleKey = nextCycleObject.GetValue(
                    nameof(Cycle.CycleKey),
                    StringComparison.OrdinalIgnoreCase)?.Value<string>();
            }
        }

        return new AnimationDefinition
        {
            Key = legacyCycle.GetValue(
                nameof(Cycle.CycleKey),
                StringComparison.OrdinalIgnoreCase)?.Value<string>() ?? stateKey,
            ThrottleTime = legacyCycle.GetValue(
                nameof(Cycle.ThrottleTime),
                StringComparison.OrdinalIgnoreCase)?.Value<double>() ?? 0,
            CycleType = cycleType,
            HideTileOnCycleEnd = legacyCycle.GetValue(
                nameof(Cycle.HideTileOnCycleEnd),
                StringComparison.OrdinalIgnoreCase)?.Value<bool>() ?? false,
            NextCycleKey = nextCycleKey,
            Frames = frames,
            Source = AnimationDefinitionSource.Generated()
        };
    }

    private static void DetachLegacySnapshotScenes(List<SceneStateEntry>? scenes)
    {
        if (scenes is null)
            return;

        foreach (var entry in scenes)
        {
            if (entry?.LegacyScene is { } legacyScene)
                Scene._allScenes.Remove(legacyScene);
        }
    }

    private static void MergeScenes(
        List<SceneStateEntry>? scenes,
        bool overwriteExisting,
        string? baseDirectory)
    {
        if (scenes is null || scenes.Count == 0)
            return;

        var existingIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < Scene._allScenes.Count; i++)
        {
            var id = Scene._allScenes[i].ID;
            if (!string.IsNullOrWhiteSpace(id) && !existingIndexById.ContainsKey(id))
                existingIndexById.Add(id, i);
        }

        // Avoid duplicating the same incoming ID twice. A duplicate within one
        // snapshot is treated as an overwrite so the last incoming entry wins.
        var seenIncoming = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in scenes)
        {
            if (entry is null)
                continue;

            Scene incoming;
            bool materializedFromDefinition = false;

            if (entry.LegacyScene is not null)
            {
                incoming = entry.LegacyScene;
            }
            else if (!string.IsNullOrWhiteSpace(entry.GscnPath))
            {
                var gscnPath = ResolvePath(entry.GscnPath, baseDirectory);
                incoming = SceneDefinitionSerializer.LoadScene(gscnPath);
                materializedFromDefinition = true;
            }
            else if (entry.Definition is not null)
            {
                incoming = SceneDefinitionSerializer.ToScene(entry.Definition);
                materializedFromDefinition = true;
            }
            else
            {
                throw new InvalidDataException(
                    "Scene state entry does not contain a GSCN path or inline definition.");
            }

            // Scene materialization registers runtime scenes immediately. Detach newly
            // materialized definitions so this method remains the single merge owner.
            if (materializedFromDefinition)
                Scene._allScenes.Remove(incoming);

            if (string.IsNullOrWhiteSpace(incoming.ID))
                incoming.ID = Guid.NewGuid().ToString();

            bool duplicateIncoming = !seenIncoming.Add(incoming.ID);

            if (existingIndexById.TryGetValue(incoming.ID, out int existingIndex))
            {
                if (!overwriteExisting && !duplicateIncoming)
                {
                    if (materializedFromDefinition)
                        incoming.Dispose();

                    continue;
                }

                Scene._allScenes[existingIndex] = incoming;
            }
            else
            {
                existingIndexById[incoming.ID] = Scene._allScenes.Count;
                Scene._allScenes.Add(incoming);
            }
        }
    }

    private static void RebindSpriteSceneLayer(Sprite sprite)
    {
        var layerId = sprite.SerializedSceneLayerIdForBinding;
        if (string.IsNullOrWhiteSpace(layerId))
        {
            if (!ReferenceEquals(sprite.SceneLayer, SceneLayer.Empty))
                return;

            throw new InvalidDataException(
                $"Sprite '{sprite.Nickname ?? sprite.Id.ToString()}' does not contain a SceneLayer identity.");
        }

        SceneLayer? targetLayer = null;
        var sceneId = sprite.SerializedSceneIdForBinding;

        if (!string.IsNullOrWhiteSpace(sceneId))
        {
            var scene = Scene._allScenes.FirstOrDefault(
                candidate => string.Equals(candidate.ID, sceneId, StringComparison.Ordinal));

            targetLayer = scene?.GetSceneLayerByID(layerId);
        }
        else
        {
            // Older data may have a layer ID but no scene ID. Layer IDs are normally
            // GUIDs; accept a unique match and reject ambiguity.
            var matches = Scene._allScenes
                .SelectMany(scene => scene.SceneLayers)
                .Where(layer => string.Equals(layer.ID, layerId, StringComparison.Ordinal))
                .Take(2)
                .ToList();

            if (matches.Count == 1)
                targetLayer = matches[0];
            else if (matches.Count > 1)
                throw new InvalidDataException(
                    $"Sprite '{sprite.Nickname ?? sprite.Id.ToString()}' references ambiguous SceneLayer ID '{layerId}'.");
        }

        if (targetLayer is null)
        {
            throw new InvalidDataException(
                $"Sprite '{sprite.Nickname ?? sprite.Id.ToString()}' could not resolve SceneLayer '{layerId}'" +
                (string.IsNullOrWhiteSpace(sceneId) ? "." : $" in Scene '{sceneId}'."));
        }

        sprite.RebindSceneLayerAfterDeserialization(targetLayer);
    }

    private static void DetachSnapshotSprites(List<Sprite>? sprites)
    {
        if (sprites is null)
            return;

        foreach (var sprite in sprites)
        {
            if (sprite is not null)
                SpriteManager.Instance._spriteList.Remove(sprite);
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

            RebindSpriteSceneLayer(incoming);

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
