using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Gondwana.Input.Keyboard;
using Microsoft.Extensions.Logging;

namespace Gondwana.Avalonia.Input.Keyboard;

/// <summary>
/// Global Avalonia key state collector that feeds the <see cref="KeyboardEventPoller"/>.
/// Attaches tunnel-phase handlers at the <see cref="TopLevel"/> so it sees ALL key messages
/// regardless of which control has focus, mirroring the WinForms <c>IMessageFilter</c> approach.
/// Key codes are <see cref="Key"/> values cast to <see cref="int"/>.
/// </summary>
public sealed class AvaloniaKeyboardAdapter : IKeyboardAdapter, IDisposable
{
    /// <summary>
    /// Converts an Avalonia <see cref="Key"/> name (case-insensitive) to its integer key code.
    /// </summary>
    /// <param name="keyName">The name of the key, matching a <see cref="Key"/> enumeration value.</param>
    /// <returns>The integer key code if the name is valid; otherwise <c>null</c>.</returns>
    public static int? GetKeyCodeFromString(string keyName)
    {
        if (Enum.TryParse<Key>(keyName, true, out var key))
            return (int)key;

        Engine.Logger.LogWarning("Invalid Avalonia key name: {KeyName}", keyName);
        return null;
    }

    private readonly Control _lifetimeOwner;
    private TopLevel? _topLevel;

    // Key state table indexed by (int)Key
    private readonly int[] _down = new int[512];

    // Modifier bits published lock-free
    private int _modsBits;

    private bool _isDisposed;

    /// <summary>
    /// Gets the current state of keyboard modifiers (Shift, Ctrl, Alt).
    /// </summary>
    public KeyboardModifierState CurrentKeyboardModifiers =>
        (KeyboardModifierState)Volatile.Read(ref _modsBits);

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaKeyboardAdapter"/> class.
    /// </summary>
    /// <param name="lifetimeOwner">
    /// The control whose visual-tree lifetime governs this adapter. Key events are captured
    /// at the <see cref="TopLevel"/> that owns this control.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lifetimeOwner"/> is null.</exception>
    public AvaloniaKeyboardAdapter(Control lifetimeOwner)
    {
        _lifetimeOwner = lifetimeOwner ?? throw new ArgumentNullException(nameof(lifetimeOwner));

        _lifetimeOwner.AttachedToVisualTree += OnAttachedToVisualTree;
        _lifetimeOwner.DetachedFromVisualTree += OnDetachedFromVisualTree;

        // If the control is already part of the visual tree, connect immediately
        if (TopLevel.GetTopLevel(_lifetimeOwner) != null)
            ConnectToTopLevel();

        Engine.Logger.LogInformation("AvaloniaKeyboardAdapter initialized.");
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        => ConnectToTopLevel();

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        => DisconnectFromTopLevel();

    private void ConnectToTopLevel()
    {
        if (_isDisposed) return;

        var tl = TopLevel.GetTopLevel(_lifetimeOwner);
        if (tl == null || ReferenceEquals(tl, _topLevel)) return;

        _topLevel = tl;
        tl.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        tl.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
    }

    private void DisconnectFromTopLevel()
    {
        if (_topLevel == null) return;

        _topLevel.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _topLevel.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);
        _topLevel = null;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_isDisposed) return;
        SetDown((int)e.Key, true);
        UpdateMods(e.KeyModifiers);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (_isDisposed) return;
        SetDown((int)e.Key, false);
        UpdateMods(e.KeyModifiers);
    }

    /// <summary>
    /// Returns <see langword="true"/> if the key represented by <paramref name="keyCode"/>
    /// (an <see cref="Key"/> value cast to <see cref="int"/>) is currently pressed.
    /// </summary>
    /// <param name="keyCode">An <see cref="Key"/> value cast to <see cref="int"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDown(int keyCode)
    {
        if ((uint)keyCode >= (uint)_down.Length) return false;
        return Volatile.Read(ref _down[keyCode]) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetDown(int keyCode, bool down)
    {
        if ((uint)keyCode >= (uint)_down.Length) return;
        Volatile.Write(ref _down[keyCode], down ? 1 : 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMods(KeyModifiers km)
    {
        int mods = 0;
        if ((km & KeyModifiers.Shift) != 0) mods |= (int)KeyboardModifierState.Shift;
        if ((km & KeyModifiers.Control) != 0) mods |= (int)KeyboardModifierState.Ctrl;
        if ((km & KeyModifiers.Alt) != 0) mods |= (int)KeyboardModifierState.Alt;
        Volatile.Write(ref _modsBits, mods);
    }

    /// <summary>
    /// Releases all resources and removes all event handlers registered by this adapter.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;

        DisconnectFromTopLevel();
        _lifetimeOwner.AttachedToVisualTree -= OnAttachedToVisualTree;
        _lifetimeOwner.DetachedFromVisualTree -= OnDetachedFromVisualTree;

        Engine.Logger.LogInformation("AvaloniaKeyboardAdapter disposed.");
    }
}
