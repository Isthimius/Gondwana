using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Avalonia.Input.Keyboard;
using Gondwana.Avalonia.Input.Mouse;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;

namespace Gondwana.Avalonia;

/// <summary>
/// Provides extension methods for configuring Avalonia-specific features on the Gondwana engine.
/// </summary>
public static class EngineExtensions
{
    /// <summary>
    /// Initializes the Avalonia keyboard adapter for the specified control.
    /// The adapter listens for key events at the <see cref="Avalonia.Controls.TopLevel"/> level,
    /// capturing all keyboard input regardless of which child element has focus.
    /// Key codes correspond to <see cref="Avalonia.Input.Key"/> values cast to <see cref="int"/>.
    /// </summary>
    /// <param name="engine">The engine instance to configure.</param>
    /// <param name="control">The control (or window) to capture keyboard input from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is null.</exception>
    public static void InitializeAvaloniaKeyboardAdapter(this Engine engine, Control control)
    {
        Engine.Logger.LogInformation("Initializing AvaloniaKeyboardAdapter...");

        if (control == null)
        {
            Engine.Logger.LogError("AvaloniaKeyboardAdapter initialization failed: Control cannot be null.");
            throw new ArgumentNullException(nameof(control));
        }

        KeyboardEventPoller.Initialize(new AvaloniaKeyboardAdapter(control));
    }

    /// <summary>
    /// Initializes the Avalonia mouse adapter for the specified control.
    /// </summary>
    /// <param name="engine">The engine instance to configure.</param>
    /// <param name="control">The control to capture mouse/pointer input from.</param>
    /// <param name="mouseEventConfiguration">Optional configuration for mouse event handling.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is null.</exception>
    public static void InitializeAvaloniaMouseAdapter(this Engine engine, Control control, MouseEventConfiguration? mouseEventConfiguration = null)
    {
        Engine.Logger.LogInformation("Initializing AvaloniaMouseAdapter...");

        if (control == null)
        {
            Engine.Logger.LogError("AvaloniaMouseAdapter initialization failed: Control cannot be null.");
            throw new ArgumentNullException(nameof(control));
        }

        MouseEventPoller.Initialize(new AvaloniaMouseAdapter(control), mouseEventConfiguration);
    }
}
