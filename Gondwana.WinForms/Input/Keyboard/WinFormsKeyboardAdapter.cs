using Gondwana.Input.Keyboard;
using Microsoft.Extensions.Logging;

namespace Gondwana.WinForms.Input.Keyboard;

/// <summary>
/// Passive WinForms key state collector that feeds the KeyboardHandler.
/// </summary>
public sealed class WinFormsKeyboardAdapter : IKeyboardAdapter, IDisposable
{
    private readonly Control _control;   // focusable control we were given
    private readonly Form? _form;        // parent form, if any

    private readonly HashSet<string> _pressedKeys = new(StringComparer.OrdinalIgnoreCase);
    private KeyboardModifierState _mods;

    public ICollection<string> PressedKeys => _pressedKeys;

    public KeyboardModifierState CurrentKeyboardModifiers => _mods;

    internal WinFormsKeyboardAdapter(Control control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));

        // This control is what actually has focus — it must decide which keys are "input keys".
        _control.PreviewKeyDown += OnPreviewKeyDown;

        // If we have a Form, use it as the global key source with KeyPreview = true.
        _form = control.FindForm();
        if (_form is not null)
        {
            _form.KeyPreview = true;
            _form.KeyDown += OnKeyDown;
            _form.KeyUp += OnKeyUp;
            _form.FormClosed += (_, __) => Dispose();
        }
        else
        {
            // No form yet; fall back to the control itself.
            _control.KeyDown += OnKeyDown;
            _control.KeyUp += OnKeyUp;
            _control.Disposed += (_, __) => Dispose();
        }

        Engine.Logger.LogInformation("WinFormsKeyboardAdapter initialized. Starting to poll key presses.");
    }

    private void OnPreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
    {
        // This runs on the *focused control*.
        if (e.KeyCode is Keys.Up or Keys.Down or Keys.Left or Keys.Right)
        {
            // Tell WinForms: treat these as input keys so KeyDown/KeyUp fire.
            e.IsInputKey = true;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        Engine.Logger.LogInformation("KeyDown: {KeyCode}", e.KeyCode);
        _pressedKeys.Add(NormalizeKey(e.KeyCode));
        RecomputeModifiers(e);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        Engine.Logger.LogInformation("KeyUp: {KeyCode}", e.KeyCode);
        _pressedKeys.Remove(NormalizeKey(e.KeyCode));
        RecomputeModifiers(e);
    }

    private void RecomputeModifiers(KeyEventArgs e)
    {
        _mods = KeyboardModifierState.None;

        if (e.Shift)
            _mods |= KeyboardModifierState.Shift;

        if (e.Control)
            _mods |= KeyboardModifierState.Ctrl;

        if (e.Alt)
            _mods |= KeyboardModifierState.Alt;
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
        _control.PreviewKeyDown -= OnPreviewKeyDown;

        if (_form is not null)
        {
            _form.KeyDown -= OnKeyDown;
            _form.KeyUp -= OnKeyUp;
        }
        else
        {
            _control.KeyDown -= OnKeyDown;
            _control.KeyUp -= OnKeyUp;
        }

        Engine.Logger.LogInformation("WinFormsKeyboardAdapter disposed.");
    }
}