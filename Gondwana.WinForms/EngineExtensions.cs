using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.WinForms.Audio;
using Gondwana.WinForms.Input.Gamepad.SDL2;
using Gondwana.WinForms.Input.Gamepad.XInput;
using Gondwana.WinForms.Input.Keyboard;
using Gondwana.WinForms.Input.Mouse;
using Microsoft.Extensions.Logging;

namespace Gondwana.WinForms;

/// <summary>
/// Provides extension methods for configuring Windows Forms-specific features on the Gondwana engine.
/// </summary>
public static class EngineExtensions
{
    /// <summary>
    /// Registers support for extended audio formats including OGG, OGA, MOGG (Vorbis), WMA, and M4A.
    /// </summary>
    /// <param name="engine">The engine instance to configure.</param>
    public static void InitializeWinFormsAudioFormats(this Engine engine)
    {
        WinFormsAudioSupport.RegisterExtendedAudioFormats();
    }

    /// <summary>
    /// Initializes and starts the SDL2 gamepad manager for cross-platform gamepad support.
    /// </summary>
    /// <param name="engine">The engine instance to configure.</param>
    public static void InitializeSdlGamepadManager(this Engine engine)
    {
        Engine.Instance.Input.GamepadManager = SdlGamepadManager.Start();
    }

    /// <summary>
    /// Initializes and starts the XInput gamepad manager for Xbox controller support on Windows.
    /// </summary>
    /// <param name="engine">The engine instance to configure.</param>
    public static void InitializeXInputGamepadManager(this Engine engine)
    {
        Engine.Instance.Input.GamepadManager = XInputGamepadManager.Start();
    }

    /// <summary>
    /// Initializes the Windows Forms keyboard adapter for the specified control.
    /// </summary>
    /// <param name="engine">The engine instance to configure.</param>
    /// <param name="control">The control to capture keyboard input from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is null.</exception>
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

    /// <summary>
    /// Initializes the Windows Forms mouse adapter for the specified control.
    /// </summary>
    /// <param name="engine">The engine instance to configure.</param>
    /// <param name="control">The control to capture mouse input from.</param>
    /// <param name="mouseEventConfiguration">Optional configuration for mouse event handling.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is null.</exception>
    public static void InitializeWinFormsMouseAdapter(this Engine engine, Control control, MouseEventConfiguration? mouseEventConfiguration = null)
    {
        Engine.Logger.LogInformation("Initializing WinFormsMouseAdapter...");

        if (control == null)
        {
            Engine.Logger.LogError("WinFormsMousedAdapter initialization failed: Control cannot be null.");
            throw new ArgumentNullException(nameof(control));
        }

        MouseEventPoller.Initialize(new WinFormsMouseAdapter(control), mouseEventConfiguration);
    }
}