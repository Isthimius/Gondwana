namespace Gondwana.Input.Keyboard.WinForms;

public interface IKeyboardAdapter
{
    ICollection<string> PressedKeys { get; }
    public ModifierState CurrentModifiers { get; }
}
