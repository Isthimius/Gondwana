using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Gondwana.Scripting
{
    public interface IScriptModifiable
    {
        void ApplyCommandLine(CommandLine cmdLn);
        void ApplyScriptSection(ScriptSection section);
    }
}
