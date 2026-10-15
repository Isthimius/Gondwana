using System.Text.Json;
using Gondwana.Assets;

namespace Gondwana.Cli.Commands.Assets;

/// <summary>
/// Loads and queries an extension-to-<see cref="AssetTypes"/> mapping from a JSON file.
/// </summary>
/// <remarks>
/// The JSON file must contain an object whose keys are <see cref="AssetTypes"/> member names
/// and whose values are arrays of file extensions (without a leading dot), e.g.:
/// <code>
/// {
///   "Image": ["png", "jpg", "bmp"],
///   "Audio": ["wav", "mp3", "ogg"],
///   "Svg": ["svg"]
/// }
/// </code>
/// </remarks>
internal sealed class AssetTypeMap
{
    private static readonly string DefaultConfigFileName = "gondwana-asset-types.json";

    // extension (lower-case, no dot) â†’ AssetTypes
    private readonly Dictionary<string, AssetTypes> _map;

    private static readonly Dictionary<string, AssetTypes> NativeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gts"] = AssetTypes.TilesheetDefinition,
        ["gani"] = AssetTypes.AnimationDefinition,
        ["gscn"] = AssetTypes.SceneDefinition,
        ["gspr"] = AssetTypes.SpriteDefinition,
        ["gsnd"] = AssetTypes.AudioDefinition
    };

    private AssetTypeMap(Dictionary<string, AssetTypes> map) => _map = map;

    /// <summary>
    /// Built-in extension â†’ <see cref="AssetTypes"/> defaults, overlaid by the selected configuration file.
    /// </summary>
    /// <returns>The default extension-to-asset-type mappings.</returns>
    internal static AssetTypeMap BuildDefault()
    {
        var map = new Dictionary<string, AssetTypes>(StringComparer.OrdinalIgnoreCase);

        foreach (var ext in new[] { "png", "jpg", "jpeg", "bmp", "gif", "webp", "tiff", "ico" })
            map[ext] = AssetTypes.Image;

        foreach (var ext in new[] { "wav", "mp3", "ogg", "flac", "aac", "wma", "mid", "midi" })
            map[ext] = AssetTypes.Audio;

        foreach (var ext in new[] { "mp4", "avi", "mkv", "mov", "wmv", "webm", "m4v" })
            map[ext] = AssetTypes.Video;

        foreach (var ext in new[] { "cur", "ani" })
            map[ext] = AssetTypes.Cursor;

        foreach (var ext in new[] { "ttf", "otf", "woff", "woff2" })
            map[ext] = AssetTypes.Font;

        foreach (var ext in new[] { "svg" })
            map[ext] = AssetTypes.Svg;

        foreach (var pair in NativeTypes) map[pair.Key] = pair.Value;
        return new AssetTypeMap(map);
    }

    /// <summary>
    /// Resolves and loads the asset-type map from the first location that exists:
    /// <list type="number">
    ///   <item>The explicit <paramref name="explicitPath"/> (if non-null).</item>
    ///   <item><c>gondwana-asset-types.json</c> in the current working directory.</item>
    ///   <item><c>gondwana-asset-types.json</c> next to the running executable (the shipped default).</item>
    ///   <item>Built-in defaults (when no file is found anywhere).</item>
    /// </list>
    /// Overlays the selected file on built-in defaults. Returns null and sets the error when
    /// the selected configuration cannot be read or contains invalid/conflicting mappings.
    /// </summary>
    /// <param name="explicitPath">The explicit path.</param>
    /// <param name="error">When this method returns, contains the error.</param>
    /// <returns>The resulting asset type map, or <see langword="null"/> when unavailable.</returns>
    public static AssetTypeMap? Load(string? explicitPath, out string? error)
    {
        // If the caller explicitly specified a file, it must exist and parse cleanly.
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return ParseFile(Path.GetFullPath(explicitPath), out error);

        // Otherwise try the well-known locations; fall back to built-in defaults.
        var candidates = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), DefaultConfigFileName),
        };

        var exeDir = Path.GetDirectoryName(typeof(AssetTypeMap).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(exeDir))
            candidates.Add(Path.Combine(exeDir, DefaultConfigFileName));

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
                continue;

            return ParseFile(candidate, out error);
        }

        // No config file found anywhere â€” use built-in defaults silently.
        error = null;
        return BuildDefault();
    }

    private static AssetTypeMap? ParseFile(string path, out string? error)
    {
        try
        {
            var json = File.ReadAllText(path);

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Type map must be a JSON object.");
            var configured = new Dictionary<string, AssetTypes>(StringComparer.OrdinalIgnoreCase);
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!types.Add(property.Name) || !Enum.GetNames<AssetTypes>().Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Unknown or duplicate asset type '{property.Name}'.");
                var type = Enum.Parse<AssetTypes>(property.Name, true);
                if (property.Value.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException($"Asset type '{property.Name}' requires an extension array.");
                foreach (var token in property.Value.EnumerateArray())
                {
                    if (token.ValueKind != JsonValueKind.String) throw new InvalidDataException("Extensions must be strings.");
                    var extension = token.GetString()!.Trim().TrimStart('.');
                    if (extension.Length == 0 || extension.Any(character => !char.IsAsciiLetterOrDigit(character)))
                        throw new InvalidDataException($"Invalid extension '{token.GetString()}'.");
                    if (!configured.TryAdd(extension, type))
                        throw new InvalidDataException($"Duplicate/conflicting extension '{extension}'.");
                    if (NativeTypes.TryGetValue(extension, out var native) && type != native)
                        throw new InvalidDataException($"Native extension '.{extension}' must map to {native}, not {type}.");
                }
            }
            var map = BuildDefault()._map;
            foreach (var pair in configured) map[pair.Key] = pair.Value;

            error = null;
            return new AssetTypeMap(map);
        }
        catch (JsonException jex)
        {
            error = $"Failed to parse '{path}': {jex.Message}";
            return null;
        }
        catch (Exception ex)
        {
            error = $"Failed to read '{path}': {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Returns the <see cref="AssetTypes"/> for the given file, or <paramref name="fallback"/> if
    /// the extension is not present in the map.
    /// </summary>
    /// <param name="filePath">The path of the file to process.</param>
    /// <param name="fallback">The fallback.</param>
    /// <returns>The asset type inferred from the file name and configured mappings.</returns>
    public AssetTypes Infer(string filePath, AssetTypes fallback)
    {
        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        return _map.TryGetValue(ext, out var type) ? type : fallback;
    }
}
