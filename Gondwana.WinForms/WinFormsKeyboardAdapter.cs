using System.Windows.Forms;

namespace Gondwana.Input.Keyboard.WinForms;

/// <summary>
/// Passive WinForms key state collector that feeds the KeyboardHandler.
/// </summary>
public sealed class WinFormsKeyboardAdapter : IKeyboardAdapter, IDisposable
{
    private readonly Form _form;
    private readonly HashSet<string> _pressedKeys = new();
    private ModifierState _mods;

    public ICollection<string> PressedKeys => _pressedKeys;
    public ModifierState CurrentModifiers => _mods;

    public WinFormsKeyboardAdapter(Form form)
    {
        _form = form ?? throw new ArgumentNullException(nameof(form));
        _form.KeyPreview = true;

        _form.KeyDown += OnKeyDown;
        _form.KeyUp += OnKeyUp;
        _form.FormClosed += (_, __) => Dispose();
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
        _mods = ModifierState.None;
        if (e.Shift) _mods |= ModifierState.Shift;
        if (e.Control) _mods |= ModifierState.Ctrl;
        if (e.Alt) _mods |= ModifierState.Alt;
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
        _form.KeyDown -= OnKeyDown;
        _form.KeyUp -= OnKeyUp;
    }
}
