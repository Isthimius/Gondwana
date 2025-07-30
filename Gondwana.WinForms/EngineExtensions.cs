using Gondwana.Input.Gamepad;
using Gondwana.Input.Keyboard;
using Gondwana.WinForms.Audio;
using Gondwana.WinForms.Input.Gamepad;
using Gondwana.WinForms.Input.Keyboard;
using Microsoft.Extensions.Logging;
using System.Windows.Forms;

namespace Gondwana.WinForms;

public static class EngineExtensions
{
    public static void InitializeWinFormsAudioFormats(this Engine engine)
    {
        WinFormsAudioSupport.RegisterExtendedAudioFormats();
    }

    public static void InitializeXInputGamepadManager(this Engine engine)
    {
        Engine.Instance.GamepadManager = XInputGamepadManager.Start();
    }

    public static void InitializeWinFormsKeyboardAdapter(this Engine engine, Form form)
    {
        Engine.Logger.LogInformation("Initializing WinFormsKeyboardAdapter...");

        if (form == null)
        {
            Engine.Logger.LogError("WinFormsKeyboardAdapter initialization failed: Form cannot be null.");
            throw new ArgumentNullException(nameof(form));
        }

        KeyboardEventPoller.Initialize(new WinFormsKeyboardAdapter(form));
    }
}
