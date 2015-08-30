using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gondwana.Logging
{
    public class LogFileException : System.Exception
    {
        public LogFileException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
