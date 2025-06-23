using System.Runtime.Serialization;

namespace Gondwana.Resource;

[DataContract]
public enum EngineResourceFileTypes
{
    [EnumMember] Bitmap = 0,
    [EnumMember] Audio = 1,
    [EnumMember] Video = 2,
    [EnumMember] Cursor = 3,
    [EnumMember] Misc = 4
}
