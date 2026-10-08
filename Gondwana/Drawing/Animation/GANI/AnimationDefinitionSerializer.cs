using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets;
using Newtonsoft.Json;

namespace Gondwana.Drawing.Animation.GANI;

/// <summary>
/// Provides loading, saving, conversion, and materialization helpers for Gondwana animation (.gani) files.
/// </summary>
public static class AnimationDefinitionSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Include,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    /// <summary>
    /// Loads an animation definition from a loose definition file.
    /// </summary>
    /// <param name="filePath">The path of the definition file.</param>
    /// <returns>The resulting animation definition.</returns>
    public static AnimationDefinition Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("GANI file path must be a non-empty string.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"GANI file not found: {fullPath}", fullPath);

        try
        {
            var definition = FromJson(File.ReadAllText(fullPath), fullPath);
            ApplyDefaultSource(
                definition,
                AnimationDefinitionSource.LooseDefinitionFile(fullPath));
            definition.LoadStamp = new(definition, filePath: fullPath);
            return definition;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to deserialize GANI file: {fullPath}", ex);
        }
    }

    /// <summary>
    /// Loads an animation definition from a readable stream.
    /// </summary>
    /// <param name="stream">The stream supplying the serialized content. The stream remains open.</param>
    /// <returns>The resulting animation definition.</returns>
    public static AnimationDefinition Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        using var reader = new StreamReader(stream, leaveOpen: true);
        return FromJson(reader.ReadToEnd());
    }

    /// <summary>
    /// Loads an animation definition from an assets-file entry.
    /// </summary>
    /// <param name="assetsFile">The assets file containing the requested entry.</param>
    /// <param name="entryName">The entry name inside the assets file.</param>
    /// <returns>The resulting animation definition.</returns>
    public static AnimationDefinition Load(AssetsFile assetsFile, string entryName)
    {
        ArgumentNullException.ThrowIfNull(assetsFile);

        if (string.IsNullOrWhiteSpace(entryName))
            throw new ArgumentException("GANI asset entry name must be a non-empty string.", nameof(entryName));

        using var stream = assetsFile.Get(AssetTypes.AnimationDefinition, entryName)
            ?? throw new FileNotFoundException($"GANI asset entry not found: {entryName}", entryName);

        var definition = Load(stream);
        definition.Source = AnimationDefinitionSource.None();
        if (!string.IsNullOrWhiteSpace(assetsFile.SourcePath))
        {
            definition.Source = AnimationDefinitionSource.PackedDefinitionFile(
                assetsFile.SourcePath,
                entryName);
        }

        definition.LoadStamp = new(definition, assetsFilePath: assetsFile.SourcePath, entryName: entryName);
        return definition;
    }

    /// <summary>
    /// Saves the supplied definition or runtime content to a definition file.
    /// </summary>
    /// <param name="filePath">The path of the definition file.</param>
    /// <param name="definition">The persisted definition to process.</param>
    public static void Save(string filePath, AnimationDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("GANI file path must be a non-empty string.", nameof(filePath));

        ArgumentNullException.ThrowIfNull(definition);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var definitionToSave = CreateDefinitionForSaving(definition, fullPath);
        File.WriteAllText(fullPath, ToJson(definitionToSave));
    }

    /// <summary>
    /// Saves the supplied definition or runtime content to a definition file.
    /// </summary>
    /// <param name="filePath">The path of the definition file.</param>
    /// <param name="cycle">The cycle.</param>
    public static void Save(string filePath, Cycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        Save(filePath, FromCycle(cycle));
    }

    /// <summary>
    /// Deserializes a definition from JSON.
    /// </summary>
    /// <param name="json">The JSON text to deserialize.</param>
    /// <returns>The deserialized definition.</returns>
    public static AnimationDefinition FromJson(string json) =>
        FromJson(json, sourceDescription: null);

    /// <summary>
    /// Serializes the supplied content as JSON.
    /// </summary>
    /// <param name="definition">The persisted definition to process.</param>
    /// <returns>The serialized JSON text.</returns>
    public static string ToJson(AnimationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return JsonConvert.SerializeObject(definition, Settings);
    }

    /// <summary>
    /// Captures an animation cycle as a persistable definition.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The resulting animation definition.</returns>
    public static AnimationDefinition FromCycle(Cycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        return new AnimationDefinition
        {
            Key = cycle.CycleKey,
            ThrottleTime = cycle.ThrottleTime,
            CycleType = cycle.Sequence.SequenceCycleType,
            HideTileOnCycleEnd = cycle.HideTileOnCycleEnd,
            NextCycleKey = cycle.NextCycle?.CycleKey,
            Frames = cycle.Sequence.FrameList
                .Select((frame, index) =>
                {
                    var definition = CreateFrameDefinition(frame);
                    definition.DurationSeconds = cycle.Sequence.GetDurationSeconds(index);
                    return definition;
                })
                .ToList(),
            Source = AnimationDefinitionSource.Generated()
        };
    }

    /// <summary>
    /// Serializes the supplied content as JSON.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The serialized JSON text.</returns>
    public static string ToJson(Cycle cycle) => ToJson(FromCycle(cycle));

    /// <summary>
    /// Materializes and registers a runtime cycle from a GANI definition.
    /// Referenced tilesheets must already be registered.
    /// Referenced next cycles must already be registered unless the definition
    /// transitions to itself.
    /// </summary>
    /// <param name="definition">The persisted definition to process.</param>
    /// <returns>The resulting cycle.</returns>
    public static Cycle ToCycle(AnimationDefinition definition)
    {
        var previous = Cycle._cycles.TryGetValue(definition.Key, out var existing)
            ? existing
            : null;
        var cycle = MaterializeCycle(definition);

        try
        {
            ApplyNextCycle(cycle, definition);
            return cycle;
        }
        catch
        {
            if (Cycle._cycles.TryGetValue(cycle.CycleKey, out var registered) &&
                ReferenceEquals(registered, cycle))
            {
                cycle.Dispose();
                if (previous is not null)
                    Cycle._cycles[definition.Key] = previous;
            }

            throw;
        }
    }

    /// <summary>
    /// Loads an animation definition and creates its runtime cycle.
    /// </summary>
    /// <param name="filePath">The path of the definition file.</param>
    /// <returns>The resulting cycle.</returns>
    public static Cycle LoadCycle(string filePath) => ToCycle(Load(filePath));

    /// <summary>
    /// Loads an animation definition and creates its runtime cycle.
    /// </summary>
    /// <param name="assetsFile">The assets file containing the requested entry.</param>
    /// <param name="entryName">The entry name inside the assets file.</param>
    /// <returns>The resulting cycle.</returns>
    public static Cycle LoadCycle(AssetsFile assetsFile, string entryName) =>
        ToCycle(Load(assetsFile, entryName));

    internal static Cycle MaterializeCycle(AnimationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = AnimationDefinitionValidator.Validate(definition);
        if (errors.Count != 0)
        {
            throw new InvalidDataException(
                "GANI definition is invalid:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(error => "- " + error)));
        }

        var frames = definition.Frames
            .Select(ResolveFrame)
            .ToList();

        var sequence = new FrameSequence(frames)
        {
            SequenceCycleType = definition.CycleType
        };

        sequence.SetDurations(definition.Frames.Select(frame => frame.DurationSeconds));

        return new Cycle(
            sequence,
            definition.ThrottleTime,
            definition.Key,
            definition.HideTileOnCycleEnd);
    }

    internal static void ApplyNextCycle(
        Cycle cycle,
        AnimationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.NextCycleKey) ||
            string.Equals(
                definition.NextCycleKey,
                cycle.CycleKey,
                StringComparison.Ordinal))
        {
            cycle.NextCycle = cycle;
            CaptureProvenance(cycle, definition);
            return;
        }

        if (!Cycle._cycles.TryGetValue(definition.NextCycleKey, out var nextCycle))
        {
            throw new InvalidDataException(
                $"GANI animation '{definition.Key}' references next cycle '{definition.NextCycleKey}', but it is not registered.");
        }

        cycle.NextCycle = nextCycle;
        CaptureProvenance(cycle, definition);
    }

    private static void CaptureProvenance(Cycle cycle, AnimationDefinition definition) =>
        cycle.DefinitionProvenance = definition.LoadStamp?.Materialized(definition,
            () => FromCycle(cycle));

    private static AnimationDefinition FromJson(
        string json,
        string? sourceDescription)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("GANI JSON content must be a non-empty string.", nameof(json));

        try
        {
            var definition = JsonConvert.DeserializeObject<AnimationDefinition>(json, Settings);
            if (definition is null)
            {
                var source = string.IsNullOrWhiteSpace(sourceDescription)
                    ? "GANI JSON content"
                    : sourceDescription;

                throw new InvalidDataException($"Failed to deserialize {source}. Result was null.");
            }

            definition.TilesheetSources ??= [];
            for (int i = 0; i < definition.TilesheetSources.Count; i++)
            {
                if (definition.TilesheetSources[i] is null)
                {
                    throw new InvalidDataException(
                        $"GANI TilesheetSources cannot contain a null entry at index {i}.");
                }
            }

            definition.Frames ??= [];
            for (int i = 0; i < definition.Frames.Count; i++)
            {
                if (definition.Frames[i] is null)
                    throw new InvalidDataException($"GANI Frames cannot contain a null entry at index {i}.");
            }

            return definition;
        }
        catch (JsonException ex)
        {
            var source = string.IsNullOrWhiteSpace(sourceDescription)
                ? "GANI JSON content"
                : sourceDescription;

            throw new InvalidDataException($"Failed to deserialize {source}.", ex);
        }
    }

    private static AnimationFrameDefinition CreateFrameDefinition(Frame frame)
    {
        if (frame.Tilesheet is null)
        {
            throw new InvalidOperationException(
                "A GANI animation frame cannot be created from an unassigned runtime Frame.");
        }

        return new AnimationFrameDefinition
        {
            Tilesheet = frame.Tilesheet.Name,
            RegionName = frame.RegionName,
            XTile = frame.XTile,
            YTile = frame.YTile
        };
    }

    private static Frame ResolveFrame(AnimationFrameDefinition definition)
    {
        var tilesheet = TilesheetRegistry.Instance.GetOrNull(definition.Tilesheet)
            ?? throw new InvalidDataException(
                $"GANI references tilesheet '{definition.Tilesheet}', but it is not registered.");

        var region = tilesheet.GetRegion(definition.RegionName)
            ?? throw new InvalidDataException(
                $"GANI references region '{definition.RegionName}' on tilesheet '{definition.Tilesheet}', but it does not exist.");

        if (definition.XTile < 0 ||
            definition.YTile < 0 ||
            definition.XTile >= region.Columns ||
            definition.YTile >= region.Rows)
        {
            throw new InvalidDataException(
                $"GANI frame ({definition.XTile}, {definition.YTile}) is outside region '{definition.RegionName}' on tilesheet '{definition.Tilesheet}'.");
        }

        return tilesheet.GetFrame(
            definition.RegionName,
            definition.XTile,
            definition.YTile);
    }

    private static void ApplyDefaultSource(
        AnimationDefinition definition,
        AnimationDefinitionSource source)
    {
        if (definition.Source.Kind == AnimationDefinitionSourceKind.None)
            definition.Source = source;
    }

    private static AnimationDefinition CreateDefinitionForSaving(
        AnimationDefinition definition,
        string fullPath)
    {
        if (definition.Source.Kind != AnimationDefinitionSourceKind.None)
            return definition;

        var clone = FromJson(ToJson(definition));
        clone.Source = AnimationDefinitionSource.LooseDefinitionFile(fullPath);
        return clone;
    }
}
