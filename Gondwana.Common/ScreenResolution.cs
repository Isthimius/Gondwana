using Gondwana.Common;
using Gondwana.Common.Exceptions;
using Gondwana.Common.Win32;
using System.Drawing;
using System.Windows.Forms;

namespace Gondwana
{
    public static class ScreenResolution
    {
        #region private fields
        private static ScreenResolutionInstance screenInstance = new ScreenResolutionInstance();
        #endregion

        public static void SetScreenResolution(Size size)
        {
            if (VisibleSurfaces.AllVisibleSurfaces.Count != 0)
                throw new ResolutionChangeException(
                    "Cannot update screen resolution with attached VisibleSurface instance(s).");
            else
                screenInstance.SetScreenResolution(size);
        }

        public static void ResetScreenResolution()
        {
            screenInstance.ResetScreenResolution();
        }

        internal class ScreenResolutionInstance
        {
            private Rectangle origRes = Screen.PrimaryScreen.Bounds;

            // constructor
            internal ScreenResolutionInstance() { }

            // finalizer for clean up
            ~ScreenResolutionInstance()
            {
                ResetScreenResolution();
            }

            internal void SetScreenResolution(Size newSz)
            {
                // only call method if not the same size as it is currently
                if (Screen.PrimaryScreen.Bounds.Size != newSz)
                    Win32Support.SetScreenResolution(newSz);
            }

            internal void ResetScreenResolution()
            {
                // only reset if previously changed
                if (Screen.PrimaryScreen.Bounds != origRes)
                    Win32Support.SetScreenResolution(origRes.Size);
            }
        }
    }
}
