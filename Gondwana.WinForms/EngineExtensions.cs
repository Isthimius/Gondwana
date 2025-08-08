using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.WinForms.Audio;
using Gondwana.WinForms.Input.Gamepad.SDL2;
using Gondwana.WinForms.Input.Gamepad.XInput;
using Gondwana.WinForms.Input.Keyboard;
using Gondwana.WinForms.Input.Mouse;
using Microsoft.Extensions.Logging;

namespace Gondwana.WinForms;

public static class EngineExtensions
{
    public static void InitializeWinFormsAudioFormats(this Engine engine)
    {
        WinFormsAudioSupport.RegisterExtendedAudioFormats();
    }

    public static void InitializeSdlGamepadManager(this Engine engine)
    {
        Engine.Instance.GamepadManager = SdlGamepadManager.Start();
    }

    public static void InitializeXInputGamepadManager(this Engine engine)
    {
        Engine.Instance.GamepadManager = XInputGamepadManager.Start();
    }

    public static void InitializeWinFormsKeyboardAdapter(this Engine engine, Control control)
    {
        Engine.Logger.LogInformation("Initializing WinFormsKeyboardAdapter...");

        if (control == null)
        {
            Engine.Logger.LogError("WinFormsKeyboardAdapter initialization failed: Control cannot be null.");
            throw new ArgumentNullException(nameof(control));
        }

        KeyboardEventPoller.Initialize(new WinFormsKeyboardAdapter(control));
    }

    public static void InitializeWinFormsMouseAdapter(this Engine engine, Control control)
    {
        Engine.Logger.LogInformation("Initializing WinFormsMouseAdapter...");

        if (control == null)
        {
            Engine.Logger.LogError("WinFormsMousedAdapter initialization failed: Control cannot be null.");
            throw new ArgumentNullException(nameof(control));
        }

        MouseEventPoller.Initialize(new WinFormsMouseAdapter(control));
    }
}
