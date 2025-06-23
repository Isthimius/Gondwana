using System.Runtime.Serialization;

namespace Gondwana.Drawing.Collisions
{
    [DataContract]
    public enum CollisionDetectionType
    {
        [EnumMember]
        None,

        [EnumMember]
        All,

        [EnumMember]
        OthersWithColDetect
    }
}
