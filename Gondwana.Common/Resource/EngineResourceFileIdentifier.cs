using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Gondwana.Common.Resource
{
    [DataContract]
    public class EngineResourceFileIdentifier
    {
        private EngineResourceFileIdentifier() { }

        public EngineResourceFileIdentifier(EngineResourceFile resFile, EngineResourceFileTypes resType, string entry)
        {
            ResourceFile = resFile;
            ResourceType = resType;
            ResourceName = entry;
        }

        [DataMember]
        public EngineResourceFile ResourceFile { get; private set; }

        [DataMember]
        public EngineResourceFileTypes ResourceType { get; private set; }

        [DataMember]
        public string ResourceName { get; private set; }

        [IgnoreDataMember]
        public bool IsValid
        {
            get
            {
                if (this.Data == null)
                    return false;
                else
                    return true;
            }
        }

        [IgnoreDataMember]
        public Stream Data
        {
            get
            {
                if (ResourceFile == null)
                    return null;

                return ResourceFile[ResourceType, ResourceName];
            }
        }
    }
}
