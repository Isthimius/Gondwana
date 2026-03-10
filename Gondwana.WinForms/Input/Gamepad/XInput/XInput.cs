using System.Runtime.InteropServices;

namespace Gondwana.WinForms.Input.Gamepad.XInput;

/// <summary>
/// Provides interop definitions for XInput API to access Xbox controller input on Windows.
/// </summary>
internal static class XInput
{
    /// <summary>
    /// Retrieves the current state of the specified controller.
    /// </summary>
    /// <param name="dwUserIndex">Index of the user's controller. Can be a value from 0 to 3.</param>
    /// <param name="pState">Pointer to an <see cref="XINPUT_STATE"/> structure that receives the current state of the controller.</param>
    /// <returns>If the function succeeds, the return value is ERROR_SUCCESS (0). If the controller is not connected, the return value is ERROR_DEVICE_NOT_CONNECTED (1167).</returns>
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    internal static extern int GetState(int dwUserIndex, out XINPUT_STATE pState);

    /// <summary>
    /// Represents the state of a controller, including packet number and gamepad data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct XINPUT_STATE
    {
        /// <summary>
        /// State packet number. The packet number indicates whether there have been any changes in the state of the controller.
        /// </summary>
        public uint dwPacketNumber;
        
        /// <summary>
        /// <see cref="XINPUT_GAMEPAD"/> structure containing the current state of an Xbox controller.
        /// </summary>
        public XINPUT_GAMEPAD Gamepad;
    }

    /// <summary>
    /// Describes the current state of the Xbox controller including buttons, triggers, and thumbsticks.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct XINPUT_GAMEPAD
    {
        /// <summary>
        /// Bitmask of the device digital buttons. A set bit indicates that the corresponding button is pressed.
        /// </summary>
        public ushort wButtons;
        
        /// <summary>
        /// The current value of the left trigger analog control. The value ranges from 0 to 255.
        /// </summary>
        public byte bLeftTrigger;
        
        /// <summary>
        /// The current value of the right trigger analog control. The value ranges from 0 to 255.
        /// </summary>
        public byte bRightTrigger;
        
        /// <summary>
        /// Left thumbstick x-axis value. The value ranges from -32768 to 32767.
        /// </summary>
        public short sThumbLX;
        
        /// <summary>
        /// Left thumbstick y-axis value. The value ranges from -32768 to 32767.
        /// </summary>
        public short sThumbLY;
        
        /// <summary>
        /// Right thumbstick x-axis value. The value ranges from -32768 to 32767.
        /// </summary>
        public short sThumbRX;
        
        /// <summary>
        /// Right thumbstick y-axis value. The value ranges from -32768 to 32767.
        /// </summary>
        public short sThumbRY;
    }

    /// <summary>
    /// Defines button codes for Xbox controller buttons used in XInput.
    /// </summary>
    [Flags]
    internal enum XInputButtons : ushort
    {
        /// <summary>
        /// A button.
        /// </summary>
        A = 0x1000,
        
        /// <summary>
        /// B button.
        /// </summary>
        B = 0x2000,
        
        /// <summary>
        /// X button.
        /// </summary>
        X = 0x4000,
        
        /// <summary>
        /// Y button.
        /// </summary>
        Y = 0x8000,
        
        /// <summary>
        /// Directional pad up.
        /// </summary>
        DPadUp = 0x0001,
        
        /// <summary>
        /// Directional pad down.
        /// </summary>
        DPadDown = 0x0002,
        
        /// <summary>
        /// Directional pad left.
        /// </summary>
        DPadLeft = 0x0004,
        
        /// <summary>
        /// Directional pad right.
        /// </summary>
        DPadRight = 0x0008,
        
        /// <summary>
        /// START button.
        /// </summary>
        Start = 0x0010,
        
        /// <summary>
        /// BACK button.
        /// </summary>
        Back = 0x0020,
        
        /// <summary>
        /// Left shoulder button.
        /// </summary>
        LeftShoulder = 0x0100,
        
        /// <summary>
        /// Right shoulder button.
        /// </summary>
        RightShoulder = 0x0200,
        
        /// <summary>
        /// Left thumbstick button (pressing down on the left stick).
        /// </summary>
        LeftThumb = 0x0040,
        
        /// <summary>
        /// Right thumbstick button (pressing down on the right stick).
        /// </summary>
        RightThumb = 0x0080,
    }
}