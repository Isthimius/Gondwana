using System.Runtime.Serialization;

namespace Gondwana.Common.Enums
{
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
}
