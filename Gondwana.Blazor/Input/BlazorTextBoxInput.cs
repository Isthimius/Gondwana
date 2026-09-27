using Gondwana.Blazor.Input.Keyboard;
using Gondwana.Input.Keyboard;
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;

namespace Gondwana.Blazor.Input;

/// <summary>
/// Applies Blazor keyboard codes and printable-character translation to a <see cref="TextBoxWidget"/>.
/// </summary>
public static class BlazorTextBoxInput
{
    /// <summary>
    /// Configures a text box to interpret <see cref="BlazorKey"/> values produced by
    /// <see cref="BlazorKeyboardAdapter"/>.
    /// </summary>
    public static TextBoxWidget Configure(TextBoxWidget textBox)
    {
        ArgumentNullException.ThrowIfNull(textBox);

        textBox.SubmitKey = (int)BlazorKey.Enter;
        textBox.BackspaceKey = (int)BlazorKey.Backspace;
        textBox.DeleteKey = (int)BlazorKey.Delete;
        textBox.LeftKey = (int)BlazorKey.ArrowLeft;
        textBox.RightKey = (int)BlazorKey.ArrowRight;
        textBox.HomeKey = (int)BlazorKey.Home;
        textBox.EndKey = (int)BlazorKey.End;
        textBox.CharacterResolver = ResolveCharacter;

        return textBox;
    }

    /// <summary>
    /// Registers the Blazor key codes needed by editable text boxes with the supplied keyboard poller.
    /// </summary>
    public static void StartMonitoring(KeyboardEventPoller keyboard)
    {
        ArgumentNullException.ThrowIfNull(keyboard);

        RegisterRange(BlazorKey.KeyA, BlazorKey.KeyZ);
        RegisterRange(BlazorKey.Digit1, BlazorKey.Digit0);
        RegisterRange(BlazorKey.Numpad0, BlazorKey.Numpad9);

        foreach (BlazorKey key in new[]
                 {
                     BlazorKey.Space,
                     BlazorKey.Enter,
                     BlazorKey.Backspace,
                     BlazorKey.Delete,
                     BlazorKey.ArrowLeft,
                     BlazorKey.ArrowRight,
                     BlazorKey.Home,
                     BlazorKey.End,
                     BlazorKey.NumpadAdd,
                     BlazorKey.NumpadSubtract,
                     BlazorKey.NumpadMultiply,
                     BlazorKey.NumpadDivide,
                     BlazorKey.NumpadDecimal,
                     BlazorKey.Minus,
                     BlazorKey.Equal,
                     BlazorKey.BracketLeft,
                     BlazorKey.BracketRight,
                     BlazorKey.Backslash,
                     BlazorKey.Semicolon,
                     BlazorKey.Quote,
                     BlazorKey.Backquote,
                     BlazorKey.Comma,
                     BlazorKey.Period,
                     BlazorKey.Slash
                 })
        {
            keyboard.StartMonitoringKey((int)key);
        }

        void RegisterRange(BlazorKey first, BlazorKey last)
        {
            for (int key = (int)first; key <= (int)last; key++)
                keyboard.StartMonitoringKey(key);
        }
    }

    /// <summary>
    /// Resolves printable characters from a Blazor widget keyboard event.
    /// </summary>
    public static char? ResolveCharacter(WidgetKeyboardEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if ((args.Modifiers & (KeyboardModifierState.Ctrl | KeyboardModifierState.Alt)) != 0)
            return null;

        bool shift = (args.Modifiers & KeyboardModifierState.Shift) != 0;
        var key = (BlazorKey)args.Key;

        if (key is >= BlazorKey.KeyA and <= BlazorKey.KeyZ)
        {
            char letter = (char)('a' + ((int)key - (int)BlazorKey.KeyA));
            return shift ? char.ToUpperInvariant(letter) : letter;
        }

        if (key is >= BlazorKey.Digit1 and <= BlazorKey.Digit9)
        {
            int offset = (int)key - (int)BlazorKey.Digit1;
            if (!shift)
                return (char)('1' + offset);

            const string shiftedDigits = "!@#$%^&*(";
            return shiftedDigits[offset];
        }

        if (key == BlazorKey.Digit0)
            return shift ? ')' : '0';

        if (key is >= BlazorKey.Numpad0 and <= BlazorKey.Numpad9)
            return (char)('0' + ((int)key - (int)BlazorKey.Numpad0));

        return key switch
        {
            BlazorKey.Space => ' ',
            BlazorKey.NumpadAdd => '+',
            BlazorKey.NumpadSubtract => '-',
            BlazorKey.NumpadMultiply => '*',
            BlazorKey.NumpadDivide => '/',
            BlazorKey.NumpadDecimal => '.',
            BlazorKey.Minus => shift ? '_' : '-',
            BlazorKey.Equal => shift ? '+' : '=',
            BlazorKey.BracketLeft => shift ? '{' : '[',
            BlazorKey.BracketRight => shift ? '}' : ']',
            BlazorKey.Backslash => shift ? '|' : '\\',
            BlazorKey.Semicolon => shift ? ':' : ';',
            BlazorKey.Quote => shift ? '"' : '\'',
            BlazorKey.Backquote => shift ? '~' : '`',
            BlazorKey.Comma => shift ? '<' : ',',
            BlazorKey.Period => shift ? '>' : '.',
            BlazorKey.Slash => shift ? '?' : '/',
            _ => null
        };
    }
}
