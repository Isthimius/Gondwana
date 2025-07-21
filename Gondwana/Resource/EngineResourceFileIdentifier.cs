using System.Text.Json.Serialization;

namespace Gondwana.Resource;

/// <summary>
/// Represents an identifier for a specific resource within a file, including its type and name.
/// </summary>
public sealed class EngineResourceFileIdentifier
{
    [JsonInclude]
    public EngineResourceFile ResourceFile { get; private set; } = null!;

    [JsonInclude]
    public EngineResourceFileTypes ResourceType { get; private set; }

    [JsonInclude]
    public string ResourceName { get; private set; } = null!;

    [JsonIgnore]
    public bool IsValid => Data is not null;

    [JsonIgnore]
    public Stream? Data => ResourceFile?[ResourceType, ResourceName];

    public EngineResourceFileIdentifier() { }

    public EngineResourceFileIdentifier(EngineResourceFile resFile, EngineResourceFileTypes resType, string entry)
    {
        ResourceFile = resFile;
        ResourceType = resType;
        ResourceName = entry;
    }

    public override string ToString() =>
        $"Resource File {ResourceFile?.FilePath} / {ResourceType} / {ResourceName}";
}
