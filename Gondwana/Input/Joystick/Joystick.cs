using System.Runtime.InteropServices;

namespace Gondwana.Input.Joystick
{
    public static class Joystick
    {
        #region Win32 p/invoke
        [DllImport("winmm.dll")]
        private static extern int joyGetNumDevs();

        [DllImport("winmm.dll")]
        private static extern int joyGetPosEx(int uJoyID, [MarshalAs(UnmanagedType.Struct)] ref JoyInfoEx pji);
        #endregion

        #region public properties
        public static int JoysticksSupported
        {
            get { return joyGetNumDevs(); }
        }
        #endregion

        #region public methods
        public static int GetJoystickInfo(JoystickID joyID, out JoyInfoEx joyInfo)
        {
            return GetJoystickInfo((int)joyID, (int)JoystickConstants.JOY_RETURNALL, out joyInfo);
        }

        public static int GetJoystickInfo(out JoyInfoEx joyInfo)
        {
            return GetJoystickInfo(JoystickConstants.JOYSTICKID1,
                (int)JoystickConstants.JOY_RETURNALL, out joyInfo);
        }

        public static int GetJoystickInfo(int joyID, out JoyInfoEx joyInfo)
        {
            return GetJoystickInfo(joyID, (int)JoystickConstants.JOY_RETURNALL, out joyInfo);
        }

        public static int GetJoystickInfo(int joyID, JoyInfoReturnFlags dwFlags, out JoyInfoEx joyInfo)
        {
            return GetJoystickInfo(joyID, (int)dwFlags, out joyInfo);
        }

        public static int GetJoystickInfo(int joyID, int dwFlags, out JoyInfoEx joyInfo)
        {
            joyInfo = new JoyInfoEx();
            joyInfo.dwFlags = dwFlags;
            joyInfo.dwSize = Marshal.SizeOf(joyInfo);
            return joyGetPosEx(joyID, ref joyInfo);
        }
        #endregion
    }
}
