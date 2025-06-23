using System.Runtime.Serialization;

namespace Gondwana.Drawing.Collisions
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
