using Gondwana.Input.Keyboard;

namespace Gondwana.Widgets.Menus;

/// <summary>A host key code and an exact combination of keyboard modifiers.</summary>
/// <remarks>Key codes follow the keyboard adapter, as with WidgetKeyboardEventArgs.
/// Display names use the virtual-key convention used by the built-in widgets.</remarks>
public readonly record struct KeyGesture
{
    /// <summary>Creates a gesture. Letter codes should use uppercase characters.</summary>
    public KeyGesture(int key, KeyboardModifierState modifiers = KeyboardModifierState.None)
    {
        if (key <= 0)
            throw new ArgumentOutOfRangeException(nameof(key));
        if ((modifiers & ~(KeyboardModifierState.Ctrl | KeyboardModifierState.Shift | KeyboardModifierState.Alt)) != 0)
            throw new ArgumentOutOfRangeException(nameof(modifiers));
        Key = key;
        Modifiers = modifiers;
    }

    /// <summary>Gets the adapter key code.</summary>
    public int Key { get; }
    /// <summary>Gets the exact modifier combination.</summary>
    public KeyboardModifierState Modifiers { get; }
    /// <summary>Creates a Control-key gesture.</summary>
    public static KeyGesture Ctrl(int key) => new(key, KeyboardModifierState.Ctrl);
    /// <summary>Creates a Control-Shift gesture.</summary>
    public static KeyGesture CtrlShift(int key) => new(key, KeyboardModifierState.Ctrl | KeyboardModifierState.Shift);
    /// <summary>Returns a display label such as Ctrl+O or Alt+F4.</summary>
    public override string ToString()
    {
        string key = Key switch
        {
            >= 65 and <= 90 or >= 48 and <= 57 => ((char)Key).ToString(),
            >= 112 and <= 135 => $"F{Key - 111}",
            8 => "Backspace", 9 => "Tab", 13 => "Enter", 27 => "Esc", 32 => "Space",
            33 => "PageUp", 34 => "PageDown", 35 => "End", 36 => "Home",
            37 => "Left", 38 => "Up", 39 => "Right", 40 => "Down", 45 => "Insert", 46 => "Delete",
            _ => Key.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        return (Modifiers.HasFlag(KeyboardModifierState.Ctrl) ? "Ctrl+" : "") +
               (Modifiers.HasFlag(KeyboardModifierState.Alt) ? "Alt+" : "") +
               (Modifiers.HasFlag(KeyboardModifierState.Shift) ? "Shift+" : "") + key;
    }
}
