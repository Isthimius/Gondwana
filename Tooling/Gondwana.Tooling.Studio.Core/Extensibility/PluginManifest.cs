using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>Version 1 of the Studio package manifest. Contract versions are independent of engine releases.</summary>
public sealed class PluginManifest
{
    public const int SupportedSchemaVersion = 1;
    public const int SupportedContractVersion = 1;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    [JsonPropertyName("version")]
    public string? Version { get; init; }
    [JsonPropertyName("entryAssembly")]
    public string? EntryAssembly { get; init; }
    [JsonPropertyName("contractVersion")]
    public int ContractVersion { get; init; }
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    [JsonPropertyName("author")]
    public string? Author { get; init; }

    internal static PluginManifest Read(string directory)
    {
        using var stream = File.OpenRead(Path.Combine(directory, "plugin.json"));
        return JsonSerializer.Deserialize<PluginManifest>(stream)
            ?? throw new InvalidDataException("plugin.json must contain an object.");
    }

    internal void Validate()
    {
        if (SchemaVersion != SupportedSchemaVersion)
            throw new InvalidDataException($"Unsupported schemaVersion {SchemaVersion}; expected {SupportedSchemaVersion}.");
        if (Id is null || !Regex.IsMatch(Id, @"\A[a-z0-9]+(?:[.-][a-z0-9]+)*\z", RegexOptions.CultureInvariant))
            throw new InvalidDataException("id must be a stable lowercase identifier using letters, digits, dots and hyphens.");
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidDataException("name is required.");
        // SemVer 2.0.0: numeric prerelease identifiers cannot contain leading zeroes.
        const string number = @"(?:0|[1-9][0-9]*)";
        const string pre = @"(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*)";
        if (Version is null || !Regex.IsMatch(Version,
            $@"\A{number}\.{number}\.{number}(?:-{pre}(?:\.{pre})*)?(?:\+[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*)?\z", RegexOptions.CultureInvariant))
            throw new InvalidDataException("version must be a SemVer 2.0.0 version, for example 1.0.0 or 1.1.0-beta.1.");
        if (string.IsNullOrWhiteSpace(EntryAssembly) ||
            !Regex.IsMatch(EntryAssembly, @"\A[a-zA-Z0-9_][a-zA-Z0-9_.-]*\.dll\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
            throw new InvalidDataException("entryAssembly must be a DLL filename in the package root, without path separators or traversal.");
        if (ContractVersion < 1)
            throw new InvalidDataException("contractVersion must be a positive integer.");
    }
}
