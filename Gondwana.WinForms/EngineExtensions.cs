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
        foreach (var adapter in XInputGamepadManager.Start().ConnectedAdapters)
        {
            Engine.Instance.GamepadAdapters!.Add(adapter);
        }

        return;
    }

    public static void InitializeWinFormsKeyboardAdapter(this Engine engine, Form form)
    {
        if (form == null)
        {
            Engine.Logger.LogError("WinFormsKeyboardAdapter initialization failed: Form cannot be null.");
            throw new ArgumentNullException(nameof(form));
        }

        // TODO: make this Keyboard attachable to VisibleSurface
        Engine.Instance.KeyboardAdapter = new WinFormsKeyboardAdapter(form);
        return;
    }
}
