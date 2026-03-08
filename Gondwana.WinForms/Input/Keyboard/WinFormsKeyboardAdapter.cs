using System.Runtime.CompilerServices;
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
    private readonly Control _lifetimeOwner;

    // Windows VK codes are 0..255
    private readonly int[] _down = new int[256];

    // modifier bits published lock-free
    private int _modsBits;

    private bool _isDisposed;

    public KeyboardModifierState CurrentKeyboardModifiers =>
        (KeyboardModifierState)Volatile.Read(ref _modsBits);

    internal WinFormsKeyboardAdapter(Control lifetimeOwner)
    {
        _lifetimeOwner = lifetimeOwner ?? throw new ArgumentNullException(nameof(lifetimeOwner));

        Application.AddMessageFilter(this);
        _lifetimeOwner.Disposed += OnOwnerDisposed;

        Engine.Logger.LogInformation("WinFormsKeyboardAdapter initialized. Using IMessageFilter for key polling.");
    }

    private void OnOwnerDisposed(object? sender, EventArgs e) => Dispose();

    // IMessageFilter: called on UI thread for each message.
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
                SetDown(ExtractVk(m), true);
                break;

            case WM_KEYUP:
            case WM_SYSKEYUP:
                SetDown(ExtractVk(m), false);
                break;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDown(int keyCode)
    {
        int idx = keyCode & 0xFF;
        return Volatile.Read(ref _down[idx]) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetDown(int keyCode, bool down)
    {
        int idx = keyCode & 0xFF;
        Volatile.Write(ref _down[idx], down ? 1 : 0);
        UpdateMods();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMods()
    {
        int mods = 0;
        var mk = Control.ModifierKeys;

        if ((mk & Keys.Shift) != 0) mods |= (int)KeyboardModifierState.Shift;
        if ((mk & Keys.Control) != 0) mods |= (int)KeyboardModifierState.Ctrl;
        if ((mk & Keys.Alt) != 0) mods |= (int)KeyboardModifierState.Alt;

        Volatile.Write(ref _modsBits, mods);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ExtractVk(in Message m)
    {
        // WParam contains VK. Clamp to 16 bits and then the low 8 bits are standard VK range.
        return m.WParam.ToInt32() & 0xFFFF;
    }

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
