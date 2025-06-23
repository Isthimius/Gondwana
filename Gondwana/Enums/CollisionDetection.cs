using System.Runtime.Serialization;

namespace Gondwana.Common.Enums
{
    [DataContract]
    public enum CollisionDetection
    {
        [EnumMember]
        None,

        [EnumMember]
        All,

        [EnumMember]
        OthersWithColDetect
    }
}
