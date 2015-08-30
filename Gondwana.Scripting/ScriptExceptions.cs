using System;
using System.Collections.Generic;
using System.Text;

namespace Gondwana.Scripting
{
    public class ParserException : System.Exception
    {
        public int LineNumber;
        public string Command;

        public ParserException(int lineNumber, string command, string message, Exception innerException)
            : base(message, innerException)
        {
            LineNumber = lineNumber;
            Command = command;
        }
    }
}
