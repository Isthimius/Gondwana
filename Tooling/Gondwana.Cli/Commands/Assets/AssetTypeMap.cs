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
///   "Audio": ["wav", "mp3", "ogg"]
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
    /// Resolves and loads the asset-type map from the first location that exists:
    /// <list type="number">
    ///   <item>The explicit <paramref name="explicitPath"/> (if non-null).</item>
    ///   <item><c>gondwana-asset-types.json</c> in the current working directory.</item>
    ///   <item><c>gondwana-asset-types.json</c> next to the running executable (the shipped default).</item>
    /// </list>
    /// Returns <see langword="null"/> and sets <paramref name="error"/> if no valid config can be found or parsed.
    /// </summary>
    public static AssetTypeMap? Load(string? explicitPath, out string? error)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(explicitPath))
            candidates.Add(Path.GetFullPath(explicitPath));

        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), DefaultConfigFileName));

        var exeDir = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(exeDir))
            candidates.Add(Path.Combine(exeDir, DefaultConfigFileName));

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
                continue;

            return ParseFile(candidate, out error);
        }

        error = $"No asset-type config found. " +
                $"Looked for '{DefaultConfigFileName}' in the current directory and next to the executable. " +
                $"Use --type-map <file> to specify an explicit path.";
        return null;
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
