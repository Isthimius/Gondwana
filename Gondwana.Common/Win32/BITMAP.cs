using System;
using System.Runtime.InteropServices;

namespace Gondwana.Common.Win32
{
    /// <summary>
    /// The BITMAP structure defines the type, width, height, color format, and bit values of a bitmap.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAP
    {
        /// <summary>
        /// The bitmap type. This member must be zero.
        /// </summary>
        public int bmType;

        /// <summary>
        /// The width, in pixels, of the bitmap. The width must be greater than zero.
        /// </summary>
        public int bmWidth;

        /// <summary>
        /// The height, in pixels, of the bitmap. The height must be greater than zero.
        /// </summary>
        public int bmHeight;

        /// <summary>
        /// The number of bytes in each scan line. This value must be divisible by 2, because the system assumes that the bit 
        /// values of a bitmap form an array that is word aligned.
        /// </summary>
        public int bmWidthBytes;

        /// <summary>
        /// The count of color planes.
        /// </summary>
        public int bmPlanes;

        /// <summary>
        /// The number of bits required to indicate the color of a pixel.
        /// </summary>
        public int bmBitsPixel;

        /// <summary>
        /// A pointer to the location of the bit values for the bitmap. The bmBits member must be a pointer to an array of 
        /// character (1-byte) values.
        /// </summary>
        public IntPtr bmBits;
    }
}
