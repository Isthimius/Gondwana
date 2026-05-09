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

    // extension (lower-case, no dot) → AssetTypes
    private readonly Dictionary<string, AssetTypes> _map;

    private AssetTypeMap(Dictionary<string, AssetTypes> map) => _map = map;

    /// <summary>
    /// Built-in extension → <see cref="AssetTypes"/> defaults, used when no JSON config file is found.
    /// </summary>
    private static AssetTypeMap BuildDefault()
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
    /// Returns <see langword="null"/> and sets <paramref name="error"/> only when an explicit path is
    /// provided but cannot be read or parsed.
    /// </summary>
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

        var exeDir = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(exeDir))
            candidates.Add(Path.Combine(exeDir, DefaultConfigFileName));

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
                continue;

            return ParseFile(candidate, out error);
        }

        // No config file found anywhere — use built-in defaults silently.
        error = null;
        return BuildDefault();
    }

    private static AssetTypeMap? ParseFile(string path, out string? error)
    {
        try
        {
            var json = File.ReadAllText(path);

            // Deserialize as Dictionary<string, string[]>; keys are AssetTypes names.
            var raw = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (raw is null)
            {
                error = $"The file '{path}' is empty or not valid JSON.";
                return null;
            }

            var map = new Dictionary<string, AssetTypes>(StringComparer.OrdinalIgnoreCase);

            foreach (var (typeName, extensions) in raw)
            {
                if (!Enum.TryParse<AssetTypes>(typeName, ignoreCase: true, out var assetType))
                {
                    error = $"Unknown asset type '{typeName}' in '{path}'. " +
                            $"Valid types: {string.Join(", ", Enum.GetNames<AssetTypes>())}.";
                    return null;
                }

                foreach (var ext in extensions)
                {
                    var normalised = ext.TrimStart('.').ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(normalised))
                        map[normalised] = assetType;
                }
            }

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
    public AssetTypes Infer(string filePath, AssetTypes fallback)
    {
        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        return _map.TryGetValue(ext, out var type) ? type : fallback;
    }
}
