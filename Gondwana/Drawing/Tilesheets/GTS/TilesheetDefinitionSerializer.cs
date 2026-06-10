using Newtonsoft.Json;

namespace Gondwana.Drawing.Tilesheets.GTS;

/// <summary>
/// Provides loading and saving helpers for Gondwana tilesheet definition (.gts) files.
/// </summary>
public static class TilesheetDefinitionSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Include,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    /// <summary>
    /// Loads a <see cref="TilesheetDefinition"/> from a .gts file.
    /// </summary>
    /// <param name="filePath">The path to the .gts file.</param>
    /// <returns>The deserialized tilesheet definition.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file cannot be deserialized.</exception>
    public static TilesheetDefinition Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("GTS file path must be a non-empty string.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"GTS file not found: {fullPath}", fullPath);

        try
        {
            var json = File.ReadAllText(fullPath);
            return FromJson(json, fullPath);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to deserialize GTS file: {fullPath}", ex);
        }
    }

    /// <summary>
    /// Saves a <see cref="TilesheetDefinition"/> to a .gts file.
    /// </summary>
    /// <param name="filePath">The path to save the .gts file to.</param>
    /// <param name="definition">The tilesheet definition to save.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is null.</exception>
    public static void Save(string filePath, TilesheetDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("GTS file path must be a non-empty string.", nameof(filePath));

        ArgumentNullException.ThrowIfNull(definition);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = ToJson(definition);

        File.WriteAllText(fullPath, json);
    }

    /// <summary>
    /// Deserializes a <see cref="TilesheetDefinition"/> from JSON text.
    /// </summary>
    /// <param name="json">The JSON content.</param>
    /// <returns>The deserialized tilesheet definition.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="json"/> is null or whitespace.</exception>
    /// <exception cref="InvalidDataException">Thrown when the JSON cannot be deserialized.</exception>
    public static TilesheetDefinition FromJson(string json)
    {
        return FromJson(json, sourceDescription: null);
    }

    /// <summary>
    /// Serializes a <see cref="TilesheetDefinition"/> to formatted JSON text.
    /// </summary>
    /// <param name="definition">The tilesheet definition to serialize.</param>
    /// <returns>The formatted JSON content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is null.</exception>
    public static string ToJson(TilesheetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return JsonConvert.SerializeObject(definition, Settings);
    }

    private static TilesheetDefinition FromJson(string json, string? sourceDescription)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("GTS JSON content must be a non-empty string.", nameof(json));

        try
        {
            var definition = JsonConvert.DeserializeObject<TilesheetDefinition>(json, Settings);

            if (definition is null)
            {
                var source = string.IsNullOrWhiteSpace(sourceDescription)
                    ? "GTS JSON content"
                    : sourceDescription;

                throw new InvalidDataException($"Failed to deserialize {source}. Result was null.");
            }

            return definition;
        }
        catch (JsonException ex)
        {
            var source = string.IsNullOrWhiteSpace(sourceDescription)
                ? "GTS JSON content"
                : sourceDescription;

            throw new InvalidDataException($"Failed to deserialize {source}.", ex);
        }
    }
}