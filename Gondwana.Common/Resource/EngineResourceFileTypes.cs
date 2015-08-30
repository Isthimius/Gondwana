using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Gondwana.Common.Resource
{
    [DataContract]
    public enum EngineResourceFileTypes : int
    {
        [EnumMember]
        Bitmap = 0,

        [EnumMember]
        Audio = 1,

        [EnumMember]
        Video = 2,

        [EnumMember]
        Cursor = 3,

        [EnumMember]
        Misc = 4
    }
}
