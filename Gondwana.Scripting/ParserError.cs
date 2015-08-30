using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gondwana.Scripting
{
    public class ParserError
    {
        public string OriginalText { get; private set; }
        public string ErrorMessage { get; private set; }
        public Exception Exception { get; private set; }
        public ScriptSection ScriptSection { get; private set; }
        public CommandLine CommandLine { get; private set; }

        private ParserError() { }

        public static ParserError New(string origText, string errMsg, Exception ex, ScriptSection scriptSection, CommandLine commandLine)
        {
            ParserError parserErr = new ParserError()
            {
                OriginalText = origText,
                ErrorMessage = errMsg,
                Exception = ex,
                ScriptSection = scriptSection,
                CommandLine = commandLine
            };

            return parserErr;
        }
    }
}
