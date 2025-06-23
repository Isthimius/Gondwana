using System.Runtime.Serialization;

namespace Gondwana.Drawing.Sprites;

[DataContract]
public enum HorizontalAlignment
{
    [EnumMember]
    Left,

    [EnumMember]
    Center,
    
    [EnumMember]
    Right
}
