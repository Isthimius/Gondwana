using System.Runtime.Serialization;

namespace Gondwana.Drawing.Sprites;

[DataContract]
public enum VerticalAlignment
{
    [EnumMember]
    Top,

    [EnumMember]
    Middle,
    
    [EnumMember]
    Bottom
}
