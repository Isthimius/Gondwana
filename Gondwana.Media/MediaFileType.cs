using System.Runtime.Serialization;

namespace Gondwana.Media
{
    [DataContract]
    public enum MediaFileType
    {
        [EnumMember]
        wav,

        [EnumMember]
        mid,

        [EnumMember]
        midi,

        [EnumMember]
        wma,

        [EnumMember]
        avi,

        [EnumMember]
        ogg,

        [EnumMember]
        mp3,

        [EnumMember]
        mpg,

        [EnumMember]
        mpeg,

        [EnumMember]
        wmv,

        [EnumMember]
        asx,

        [EnumMember]
        unknown
    }
}
