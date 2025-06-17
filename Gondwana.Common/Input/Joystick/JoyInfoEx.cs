using System.Runtime.InteropServices;

namespace Gondwana.Input.Joystick
{
    /// <summary>
    /// Value type containing joystick position information.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct JoyInfoEx
    {
        /// <summary>Size of structure, in bytes.</summary>
        public int dwSize;
        /// <summary>Flags to indicate what information is valid for the device.</summary>
        public int dwFlags;
        /// <summary>X axis.</summary>
        public int dwXpos;
        /// <summary>Y axis.</summary>
        public int dwYpos;
        /// <summary>Z axis.</summary>
        public int dwZpos;
        /// <summary>Rudder position.</summary>
        public int dwRpos;
        /// <summary>5th axis position.</summary>
        public int dwUpos;
        /// <summary>6th axis position.</summary>
        public int dwVpos;
        /// <summary>State of buttons.</summary>
        public int dwButtons;
        /// <summary>Currently pressed button.</summary>
        public int dwButtonNumber;
        /// <summary>Angle of the POV hat, in degrees (0 - 35999, divide by 100 to get 0 - 359.99 degrees.</summary>
        public int dwPOV;
        /// <summary>Reserved.</summary>
        public int dwReserved1;
        /// <summary>Reserved.</summary>
        public int dwReserved2;
    }
}
