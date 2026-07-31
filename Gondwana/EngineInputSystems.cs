using Gondwana.Input.Gamepad;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Input.Touch;

namespace Gondwana;

/// <summary>
/// Provides centralized access to all the input systems of the engine, including gamepad, keyboard, mouse, and touch input.
/// </summary>
public sealed class EngineInputSystems
{
    internal EngineInputSystems() { }

    private IGamepadManager<IGamepadAdapter>? _gamepadManager = null;

    /// <summary>
    /// Gets or sets the gamepad manager responsible for handling gamepad input.
    /// </summary>
    /// <remarks>Setting this property attaches an update callback to the engine cycle, polling attached adapters</remarks>
    public IGamepadManager<IGamepadAdapter>? GamepadManager
    {
        get => _gamepadManager;
        set
        {
            GamepadEventPoller.Initialize(value?.ConnectedAdapters);
            _gamepadManager = value;
        }
    }

    /// <summary>
    /// Gets the gamepad event polling subsystem, if initialized.
    /// </summary>
    /// <value>
    /// The <see cref="Gondwana.Input.Gamepad.GamepadEventPoller"/> instance if initialized;
    /// otherwise, <c>null</c>.
    /// </value>
    /// <remarks>
    /// This property provides access to the gamepad input subsystem. The poller is
    /// automatically initialized when a <see cref="GamepadManager"/> is assigned.
    /// </remarks>
    public GamepadEventPoller? GamepadEventPoller => GamepadEventPoller.Instance;

    /// <summary>
    /// Gets the keyboard event polling subsystem, if initialized.
    /// </summary>
    /// <value>
    /// The <see cref="Gondwana.Input.Keyboard.KeyboardEventPoller"/> instance if initialized;
    /// otherwise, <c>null</c>.
    /// </value>
    /// <remarks>
    /// This property provides access to the keyboard input subsystem. The poller must be
    /// initialized via <see cref="Initialize"/> with a valid <see cref="IKeyboardAdapter"/>
    /// before use.
    /// </remarks>
    public KeyboardEventPoller? KeyboardEventPoller => KeyboardEventPoller.Instance ?? null;

    /// <summary>
    /// Gets the mouse event polling subsystem, if initialized.
    /// </summary>
    /// <value>
    /// The <see cref="Gondwana.Input.Mouse.MouseEventPoller"/> instance if initialized;
    /// otherwise, <c>null</c>.
    /// </value>
    /// <remarks>
    /// This property provides access to the mouse input subsystem. The poller must be
    /// initialized via <see cref="Initialize"/> with a valid <see cref="IMouseAdapter"/>
    /// before use.
    /// </remarks>
    public MouseEventPoller? MouseEventPoller => MouseEventPoller.Instance ?? null;

    /// <summary>
    /// Gets or sets the touch adapter responsible for providing raw touch state to the engine.
    /// </summary>
    /// <remarks>
    /// Setting this property disposes the previous adapter (if it implements
    /// <see cref="IDisposable"/>) and initializes a new <see cref="TouchEventPoller"/> instance
    /// backed by the supplied adapter. Pass <see langword="null"/> to clear the current adapter
    /// without replacing it.
    /// </remarks>
    public ITouchAdapter? TouchAdapter
    {
        get => TouchEventPoller.Instance?.Adapter;
        set
        {
            if (value is null)
                TouchEventPoller.Reset();
            else
                TouchEventPoller.Initialize(value);
        }
    }

    /// <summary>
    /// Gets the touch event polling subsystem, if initialized.
    /// </summary>
    /// <value>
    /// The <see cref="Gondwana.Input.Touch.TouchEventPoller"/> instance if initialized;
    /// otherwise, <c>null</c>.
    /// </value>
    /// <remarks>
    /// This property provides access to the touch input subsystem, which also implements
    /// <see cref="ITouchInput"/> for gesture recognizer consumption. Initialize it by assigning
    /// a platform adapter to <see cref="TouchAdapter"/>, or by calling
    /// <c>engine.InitializeAvaloniaTouchAdapter(control)</c> from the <c>Gondwana.Avalonia</c>
    /// package.
    /// </remarks>
    public TouchEventPoller? TouchEventPoller => TouchEventPoller.Instance;
}
