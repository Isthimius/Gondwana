using System.Runtime.InteropServices;

namespace Gondwana.Common.Win32
{
    /// <summary>
    /// Static wrapper class for p/invoke calls to Win32 methods in gdi32.dll and user32.dll
    /// </summary>
    public static class pInvoke
    {
        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern int BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjSource, int nXSrc, int nYSrc, TernaryRasterOperations dwRop);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern int StretchBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjSource, int nXSrc, int nYSrc, int nSrcWidth, int nSrcHeight, TernaryRasterOperations dwRop);
    }
}
