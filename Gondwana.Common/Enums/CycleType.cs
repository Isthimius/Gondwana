using System.Runtime.Serialization;

namespace Gondwana.Common.Enums
{
    /// <summary>
    /// Simple is self-terminating; the other two are repeating
    /// </summary>
    [DataContract]
    public enum CycleType
    {
        [EnumMember]
        Simple,

        [EnumMember]
        Repeating,
        
        [EnumMember]
        PingPong
    }
}
