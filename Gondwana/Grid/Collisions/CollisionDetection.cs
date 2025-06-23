using System.Runtime.Serialization;

namespace Gondwana.Grid.Collisions
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
