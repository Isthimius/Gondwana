using System.Runtime.InteropServices;

namespace Gondwana.WinForms.Input.Gamepad;

internal static class XInput
{
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    internal static extern int GetState(int dwUserIndex, out XINPUT_STATE pState);

    [StructLayout(LayoutKind.Sequential)]
    internal struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [Flags]
    public enum XInputButtons : ushort
    {
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000,
        DPadUp = 0x0001,
        DPadDown = 0x0002,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
    }
}
