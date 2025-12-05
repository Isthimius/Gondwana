namespace Gondwana.Input.Keyboard;

public sealed class KeyDownEventArgs : EventArgs
{
    public KeyEventConfiguration KeyConfig { get; }
    public KeyboardModifierState Modifiers { get; }
    public KeyAction KeyAction { get; }

    public bool IsShift => Modifiers.HasFlag(KeyboardModifierState.Shift);
    public bool IsCtrl => Modifiers.HasFlag(KeyboardModifierState.Ctrl);
    public bool IsAlt => Modifiers.HasFlag(KeyboardModifierState.Alt);

    public KeyDownEventArgs(KeyEventConfiguration config, KeyboardModifierState modifiers, KeyAction action)
    {
        KeyConfig = config;
        Modifiers = modifiers;
        KeyAction = action;
    }
}
