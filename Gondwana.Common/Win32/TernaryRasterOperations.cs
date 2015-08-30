using System.Runtime.Serialization;

namespace Gondwana.Common.Win32
{
    /// <summary>
    /// Enumeration for the raster operations used in BitBlt and StretchBlt.
    /// </summary>
    [DataContract]
    public enum TernaryRasterOperations
    {
        /// <summary>
        /// dest = source
        /// </summary>
        [EnumMember]
        SRCCOPY = 0x00CC0020,

        /// <summary>
        /// dest = source OR dest
        /// </summary>
        [EnumMember]
        SRCPAINT = 0x00EE0086,

        /// <summary>
        /// dest = source AND dest
        /// </summary>
        [EnumMember]
        SRCAND = 0x008800C6,

        /// <summary>
        /// dest = source XOR dest
        /// </summary>
        [EnumMember]
        SRCINVERT = 0x00660046,

        /// <summary>
        /// dest = source AND (NOT dest)
        /// </summary>
        [EnumMember]
        SRCERASE = 0x00440328,

        /// <summary>
        /// dest = (NOT source)
        /// </summary>
        [EnumMember]
        NOTSRCCOPY = 0x00330008,

        /// <summary>
        /// dest = (NOT src) AND (NOT dest)
        /// </summary>
        [EnumMember]
        NOTSRCERASE = 0x001100A6,

        /// <summary>
        /// dest = (source AND pattern)
        /// </summary>
        [EnumMember]
        MERGECOPY = 0x00C000CA,

        /// <summary>
        /// dest = (NOT source) OR dest
        /// </summary>
        [EnumMember]
        MERGEPAINT = 0x00BB0226,

        /// <summary>
        /// dest = pattern
        /// </summary>
        [EnumMember]
        PATCOPY = 0x00F00021,

        /// <summary>
        /// dest = DPSnoo
        /// </summary>
        [EnumMember]
        PATPAINT = 0x00FB0A09,

        /// <summary>
        /// dest = pattern XOR dest
        /// </summary>
        [EnumMember]
        PATINVERT = 0x005A0049,

        /// <summary>
        /// dest = (NOT dest)
        /// </summary>
        [EnumMember]
        DSTINVERT = 0x00550009,

        /// <summary>
        /// dest = BLACK
        /// </summary>
        [EnumMember]
        BLACKNESS = 0x00000042,

        /// <summary>
        /// dest = WHITE
        /// </summary>
        [EnumMember]
        WHITENESS = 0x00FF0062
    };
}
