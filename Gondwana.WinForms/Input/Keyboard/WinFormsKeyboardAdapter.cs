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
    /// <summary>
    /// Attempts to convert a string representation of a key into its corresponding <see cref="Keys"/> value.
    /// </summary>
    /// <remarks>
    /// The comparison is case-insensitive. If the provided <paramref name="keyName"/> does not match a valid
    /// <see cref="Keys"/> enumeration value, a warning is logged and <c>null</c> is returned.
    /// </remarks>
    /// <param name="keyName">
    /// The name of the key to parse. This should match a value from the <see cref="Keys"/> enumeration
    /// (e.g., "A", "Enter", "Escape").
    /// </param>
    /// <returns>
    /// A nullable <see cref="Keys"/> value representing the parsed key if successful; otherwise, <c>null</c>
    /// if the input string is not a valid key name.
    /// </returns>
    public static Keys? GetKeyFromString(string keyName)
    {
        // Parse the received key string into the Keys enum (case-insensitive)
        if (Enum.TryParse<Keys>(keyName, true, out var key))
            return key;

        Engine.Logger.LogWarning("Invalid key name: {KeyName}", keyName);
        return null;
    }

    private readonly Control _lifetimeOwner;

    // Windows VK codes are 0..255
    private readonly int[] _down = new int[256];

    // modifier bits published lock-free
    private int _modsBits;

    private bool _isDisposed;

    /// <summary>
    /// Gets the current state of keyboard modifiers (Shift, Ctrl, Alt).
    /// </summary>
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

    /// <summary>
    /// Filters Windows messages to capture keyboard state changes for all key events.
    /// </summary>
    /// <param name="m">The Windows message to filter.</param>
    /// <returns>Always returns <see langword="false"/> to allow the message to continue processing.</returns>
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

    /// <summary>
    /// Determines whether the specified key is currently pressed.
    /// </summary>
    /// <param name="keyCode">The virtual key code to check.</param>
    /// <returns><see langword="true"/> if the key is currently pressed; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Releases all resources used by the <see cref="WinFormsKeyboardAdapter"/> and removes the message filter.
    /// </summary>
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
