namespace Gondwana.Input.Keyboard;

public sealed class KeyDownEventArgs : EventArgs
{
    public KeyEventConfiguration KeyConfig { get; }
    public ModifierState Modifiers { get; }

    public bool IsShift => Modifiers.HasFlag(ModifierState.Shift);
    public bool IsCtrl => Modifiers.HasFlag(ModifierState.Ctrl);
    public bool IsAlt => Modifiers.HasFlag(ModifierState.Alt);

    public KeyDownEventArgs(KeyEventConfiguration config, ModifierState modifiers)
    {
        KeyConfig = config;
        Modifiers = modifiers;
    }
}