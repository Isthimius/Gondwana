using Gondwana.Input.Keyboard;
using Microsoft.Extensions.Logging;

namespace Gondwana.WinForms.Input.Keyboard;

/// <summary>
/// Global WinForms key state collector that feeds the KeyboardHandler.
/// Uses IMessageFilter so it sees ALL key messages (including arrows),
/// regardless of which control has focus or how WinForms classifies them.
/// </summary>
public sealed class WinFormsKeyboardAdapter : IKeyboardAdapter, IMessageFilter, IDisposable
{
    private readonly Control _lifetimeOwner; // just so we know when to auto-dispose
    private readonly HashSet<string> _pressedKeys = new(StringComparer.OrdinalIgnoreCase);
    private KeyboardModifierState _mods;
    private bool _isDisposed;

    public ICollection<string> PressedKeys => _pressedKeys;

    public KeyboardModifierState CurrentKeyboardModifiers => _mods;

    internal WinFormsKeyboardAdapter(Control lifetimeOwner)
    {
        _lifetimeOwner = lifetimeOwner ?? throw new ArgumentNullException(nameof(lifetimeOwner));

        // Listen to all key messages at the application level.
        Application.AddMessageFilter(this);
        _lifetimeOwner.Disposed += OnOwnerDisposed;

        Engine.Logger.LogInformation("WinFormsKeyboardAdapter initialized. Using IMessageFilter for key polling.");
    }

    private void OnOwnerDisposed(object? sender, EventArgs e)
    {
        Dispose();
    }

    // IMessageFilter: called for every Windows message before normal dispatch.
    public bool PreFilterMessage(ref Message m)
    {
        const int WM_KEYDOWN = 0x0100;
        const int WM_KEYUP = 0x0101;
        const int WM_SYSKEYDOWN = 0x0104;
        const int WM_SYSKEYUP = 0x0105;

        if (_isDisposed)
            return false;

        switch (m.Msg)
        {
            case WM_KEYDOWN:
            case WM_SYSKEYDOWN:
                HandleKeyDown((Keys)(m.WParam.ToInt32() & 0xFFFF));
                break;

            case WM_KEYUP:
            case WM_SYSKEYUP:
                HandleKeyUp((Keys)(m.WParam.ToInt32() & 0xFFFF));
                break;
        }

        // Never eat the message; let WinForms do whatever it wants too.
        return false;
    }

    private void HandleKeyDown(Keys keyCode)
    {
        _pressedKeys.Add(NormalizeKey(keyCode));
        RecomputeModifiers();
    }

    private void HandleKeyUp(Keys keyCode)
    {
        _pressedKeys.Remove(NormalizeKey(keyCode));
        RecomputeModifiers();
    }

    private void RecomputeModifiers()
    {
        _mods = KeyboardModifierState.None;

        if ((Control.ModifierKeys & Keys.Shift) != 0)
            _mods |= KeyboardModifierState.Shift;

        if ((Control.ModifierKeys & Keys.Control) != 0)
            _mods |= KeyboardModifierState.Ctrl;

        if ((Control.ModifierKeys & Keys.Alt) != 0)
            _mods |= KeyboardModifierState.Alt;
    }

    private static string NormalizeKey(Keys keyCode) => keyCode.ToString();

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        Application.RemoveMessageFilter(this);
        _lifetimeOwner.Disposed -= OnOwnerDisposed;

        Engine.Logger.LogInformation("WinFormsKeyboardAdapter disposed.");
    }
}
