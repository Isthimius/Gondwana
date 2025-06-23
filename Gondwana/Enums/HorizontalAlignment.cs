using System.Runtime.Serialization;

namespace Gondwana.Common.Enums
{
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
}
