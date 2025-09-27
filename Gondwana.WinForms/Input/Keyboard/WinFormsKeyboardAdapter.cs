using Gondwana.Input.Keyboard;
using Microsoft.Extensions.Logging;

namespace Gondwana.WinForms.Input.Keyboard;

/// <summary>
/// Passive WinForms key state collector that feeds the KeyboardHandler.
/// </summary>
public sealed class WinFormsKeyboardAdapter : IKeyboardAdapter, IDisposable
{
    private readonly Control _control;
    private readonly HashSet<string> _pressedKeys = new();
    private KeyboardModifierState _mods;

    public ICollection<string> PressedKeys => _pressedKeys;

    public KeyboardModifierState CurrentKeyboardModifiers => _mods;

    internal WinFormsKeyboardAdapter(Control control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _control.KeyDown += OnKeyDown;
        _control.KeyUp += OnKeyUp;

        // Only set KeyPreview and handle FormClosed if it's actually a Form
        if (control is Form form)
        {
            form.KeyPreview = true;
            form.FormClosed += (_, __) => Dispose();
        }
        else
        {
            // If it's not a Form, hook into the general Disposed event
            _control.Disposed += (_, __) => Dispose();
        }

        Engine.Logger.LogInformation("WinFormsKeyboardAdapter initialized. Starting to poll key presses.");
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _pressedKeys.Add(NormalizeKey(e.KeyCode));
        RecomputeModifiers(e);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _pressedKeys.Remove(NormalizeKey(e.KeyCode));
        RecomputeModifiers(e);
    }

    private void RecomputeModifiers(KeyEventArgs e)
    {
        _mods = KeyboardModifierState.None;
        if (e.Shift) _mods |= KeyboardModifierState.Shift;
        if (e.Control) _mods |= KeyboardModifierState.Ctrl;
        if (e.Alt) _mods |= KeyboardModifierState.Alt;
    }

    private static string NormalizeKey(Keys keyCode) => keyCode switch
    {
        Keys.Up => "ArrowUp",
        Keys.Down => "ArrowDown",
        Keys.Left => "ArrowLeft",
        Keys.Right => "ArrowRight",
        _ => keyCode.ToString()
    };

    public void Dispose()
    {
        _control.KeyDown -= OnKeyDown;
        _control.KeyUp -= OnKeyUp;

        Engine.Logger.LogInformation("WinFormsKeyboardAdapter disposed.");
    }
}