using Gondwana.Common.Win32;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Gondwana
{
    public static class Cursors
    {
        #region private fields
        private static CursorsInstance cursorsInstance = new CursorsInstance();
        #endregion

        #region public methods
        public static void SetCursor(string file)
        {
            cursorsInstance.SetCursor(file, null);
        }

        public static void SetCursor(string file, Control ctl)
        {
            cursorsInstance.SetCursor(file, ctl);
        }

        public static void ResetCursor()
        {
            cursorsInstance.ResetCursor(null);
        }

        public static void ResetCursor(Control ctl)
        {
            cursorsInstance.ResetCursor(ctl);
        }

        public static void ShowCursor(bool visible)
        {
            pInvoke.ShowCursor(visible);
        }
        #endregion

        internal class CursorsInstance
        {
            private Cursor origCur = Cursor.Current;
            private Dictionary<string, Cursor> loadedCursors = new Dictionary<string, Cursor>();

            // constructor
            internal CursorsInstance() { }

            // finalizer for clean up
            ~CursorsInstance()
            {
                ResetCursor(null);
                loadedCursors.Clear();
            }

            internal void SetCursor(string file, Control ctl)
            {
                if (!loadedCursors.ContainsKey(file))
                {
                    IntPtr curPtr = pInvoke.LoadCursorFromFile(file);
                    Cursor curObj = new Cursor(curPtr);
                    loadedCursors.Add(file, curObj);
                }

                if (ctl == null)
                    System.Windows.Forms.Cursor.Current = loadedCursors[file];
                else
                    ctl.Cursor = loadedCursors[file];
            }

            // TODO: implement this here guy
            internal void SetCursorFromResx(string resName, Control ctl)
            {
                throw new NotImplementedException();
            }

            internal void ResetCursor(Control ctl)
            {
                pInvoke.ShowCursor(true);

                if (ctl == null)
                    System.Windows.Forms.Cursor.Current = origCur;
                else
                    ctl.Cursor = origCur;
            }
        }
    }
}
