using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;

namespace Gondwana.Common.Utility
{
    public static class ResourceHandler
    {
        public enum MediaType
        {
            WavFile,
            MediaFile,
            BmpFile
        }

        public static void AddResource(string resKey, MediaType mediaType, string file)
        {

        }

        public static void RemoveResource(string resKey)
        {

        }

        public static void ClearResources()
        {
            
        }

        public static object GetResource(string resKey)
        {
            return null;    
        }
    }
}
