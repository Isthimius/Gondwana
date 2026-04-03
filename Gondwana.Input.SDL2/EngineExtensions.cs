using Gondwana.Input.Gamepad.SDL2;

namespace Gondwana.Input.SDL2;

/// <summary>
/// Provides extension methods for configuring SDL2-specific features on the Gondwana engine.
/// </summary>
public static class EngineExtensions
{
    /// <summary>
    /// Initializes and starts the SDL2 gamepad manager for cross-platform gamepad support.
    /// </summary>
    /// <param name="engine">The engine instance to configure.</param>
    public static void InitializeSdlGamepadManager(this Engine engine)
    {
        Engine.Instance.Input.GamepadManager = SdlGamepadManager.Start();
    }
}