using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Gondwana.Scripting
{
    /// <summary>
    /// Interface that enables the class instance to be written to a script
    /// file by the <see cref="Gondwana.Scripting.Parser"/> static class
    /// </summary>
    public interface IScriptable
    {
        ScriptSection Script { get; }
        bool IncludeScriptComments { get; }
        bool IncludeScriptWhiteSpace { get; }
    }
}
