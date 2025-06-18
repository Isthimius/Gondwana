using System.Runtime.Serialization;

namespace Gondwana.Resource;

[DataContract]
public class EngineResourceFileIdentifier
{
    private EngineResourceFileIdentifier() { }

    public EngineResourceFileIdentifier(EngineResourceFile resFile, EngineResourceFileTypes resType, string entry)
    {
        ResourceFile = resFile;
        ResourceType = resType;
        ResourceName = entry;
    }

    [DataMember]
    public EngineResourceFile ResourceFile { get; private set; }

    [DataMember]
    public EngineResourceFileTypes ResourceType { get; private set; }

    [DataMember]
    public string ResourceName { get; private set; }

    [IgnoreDataMember]
    public bool IsValid => Data != null;

    [IgnoreDataMember]
    public Stream Data => ResourceFile?[ResourceType, ResourceName];

    public override string ToString() =>
        $"Resource File {ResourceFile?.FilePath} / {ResourceType} / {ResourceName}";
}
