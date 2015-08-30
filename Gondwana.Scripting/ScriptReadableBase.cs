using System;
using System.Collections.Generic;
using System.Text;

namespace Gondwana.Scripting
{
    /// <summary>
    /// Base class that enables a derived class to be instantiated via a
    /// <see cref="Gondwana.Scripting.ScriptSection"/> argument
    /// </summary>
    public abstract class ScriptReadableBase
    {
        #region fields
        private ScriptSection _script;
        #endregion

        #region public constructor
        public ScriptReadableBase(ScriptSection script)
        {
            _script = script;
        }
        #endregion

        #region properties
        public ScriptSection SourceScript
        {
            get { return _script; }
        }

        public bool LoadedFromScript
        {
            get { return (_script != null); }
        }
        #endregion
    }
}
