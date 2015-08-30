using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gondwana.Configuration.Registry
{
    public class RegistryWriterError
    {
        public RegistryOperation Operation;
        public DateTime TimeStamp;
        public string Message;
        public Exception Ex;
    }
}
