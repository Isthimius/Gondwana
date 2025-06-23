using System.Runtime.Serialization;

namespace Gondwana.Drawing.Animation
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
