using System.Runtime.CompilerServices;
using Gondwana.Blazor.Rendering;
using Gondwana.Input.Keyboard;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace Gondwana.Blazor.Input.Keyboard;

/// <summary>
/// Keyboard adapter for Blazor applications that feeds the Gondwana
/// <see cref="KeyboardEventPoller"/> by translating browser <c>KeyboardEvent.code</c>
/// values into <see cref="BlazorKey"/> integer codes.
/// </summary>
/// <remarks>
/// <para>
/// Key codes correspond to <see cref="BlazorKey"/> values cast to <see cref="int"/>.
/// Use <see cref="GetKeyCodeFromString"/> to resolve a key name at runtime, or use
/// <c>(int)BlazorKey.Space</c> directly in game code.
/// </para>
/// <para>
/// Keyboard events are captured on the canvas element owned by
/// <see cref="BlazorBitmapRenderSurfaceComponent"/>. The canvas must have focus for events to
/// be received; the component requests focus automatically on first render.
/// </para>
/// </remarks>
public sealed class BlazorKeyboardAdapter : IKeyboardAdapter, IDisposable
{
    private static readonly int KeyArraySize = Enum.GetValues<BlazorKey>().Cast<int>().Max() + 1;

    private readonly BlazorBitmapRenderSurfaceComponent _component;
    private readonly int[] _down;
    private int _modsBits;
    private bool _isDisposed;

    /// <inheritdoc/>
    public KeyboardModifierState CurrentKeyboardModifiers =>
        (KeyboardModifierState)Volatile.Read(ref _modsBits);

    /// <summary>
    /// Initializes a new instance of <see cref="BlazorKeyboardAdapter"/> and attaches it to
    /// the keyboard events on the specified render surface component.
    /// </summary>
    /// <param name="component">The render surface component to capture keyboard input from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is null.</exception>
    public BlazorKeyboardAdapter(BlazorBitmapRenderSurfaceComponent component)
    {
        _component = component ?? throw new ArgumentNullException(nameof(component));
        _down = new int[KeyArraySize];

        _component.KeyDown += OnKeyDown;
        _component.KeyUp += OnKeyUp;

        Engine.Logger.LogInformation("BlazorKeyboardAdapter initialized.");
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDown(int keyCode)
    {
        if ((uint)keyCode >= (uint)_down.Length) return false;
        return Volatile.Read(ref _down[keyCode]) != 0;
    }

    /// <summary>
    /// Converts a key name (a <see cref="BlazorKey"/> member name or a browser
    /// <c>KeyboardEvent.code</c> string) to its integer key code.
    /// </summary>
    /// <param name="keyName">The key name to resolve.</param>
    /// <returns>The integer key code, or <see langword="null"/> if the name is not recognized.</returns>
    public static int? GetKeyCodeFromString(string keyName)
    {
        if (Enum.TryParse<BlazorKey>(keyName, true, out var key) && key != BlazorKey.None)
            return (int)key;

        Engine.Logger.LogWarning("Invalid Blazor key name: {KeyName}", keyName);
        return null;
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (_isDisposed) return;
        SetDown(e.Code, true);
        UpdateMods(e);
    }

    private void OnKeyUp(KeyboardEventArgs e)
    {
        if (_isDisposed) return;
        SetDown(e.Code, false);
        UpdateMods(e);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetDown(string code, bool down)
    {
        if (!Enum.TryParse<BlazorKey>(code, false, out var key) || key == BlazorKey.None) return;
        var index = (int)key;
        if ((uint)index >= (uint)_down.Length) return;
        Volatile.Write(ref _down[index], down ? 1 : 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMods(KeyboardEventArgs e)
    {
        int mods = 0;
        if (e.ShiftKey) mods |= (int)KeyboardModifierState.Shift;
        if (e.CtrlKey) mods |= (int)KeyboardModifierState.Ctrl;
        if (e.AltKey) mods |= (int)KeyboardModifierState.Alt;
        Volatile.Write(ref _modsBits, mods);
    }

    /// <summary>Releases all resources and removes event handlers registered by this adapter.</summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _component.KeyDown -= OnKeyDown;
        _component.KeyUp -= OnKeyUp;

        Engine.Logger.LogInformation("BlazorKeyboardAdapter disposed.");
    }
}
