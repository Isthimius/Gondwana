using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gondwana.Scripting.Ini
{
    public class DirtyCloseEventArgs : System.EventArgs
    {
        public readonly IniFile iniFile;
        public bool SaveValues = false;

        protected internal DirtyCloseEventArgs(IniFile file)
        {
            iniFile = file;
        }
    }
}
