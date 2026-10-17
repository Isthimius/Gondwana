using System.Text.Json;

namespace Gondwana.Cli.Commands.Assets;

/// <summary>Reads per-archive credentials for scoped destructive operations.</summary>
internal static class AssetCommandCredentials
{
    /// <summary>Loads absolute archive paths using the host filesystem's comparison rules.</summary>
    /// <param name="path">Optional credential JSON filename.</param>
    /// <returns>A normalized map; credential values are never included in diagnostics.</returns>
    internal static Dictionary<string, string> Load(string? path)
    {
        var credentials = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (path is not null)
            foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid credential map."))
            {
                if (!Path.IsPathFullyQualified(pair.Key)) throw new InvalidDataException("Credential keys must be absolute archive paths.");
                credentials.Add(Path.GetFullPath(pair.Key), pair.Value);
            }
        return credentials;
    }
}
