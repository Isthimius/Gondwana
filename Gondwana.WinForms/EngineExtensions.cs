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

    public static XInputGamepadManager InitializeXInputGamepadManager(this Engine engine)
    {
        return XInputGamepadManager.Start();
    }

    public static WinFormsKeyboardAdapter InitializeWinFormsKeyboardAdapter(this Engine engine, Form form)
    {
        if (form == null)
        {
            Engine.Logger.LogError("WinFormsKeyboardAdapter initialization failed: Form cannot be null.");
            throw new ArgumentNullException(nameof(form));
        }

        return new WinFormsKeyboardAdapter(form);
    }
}
