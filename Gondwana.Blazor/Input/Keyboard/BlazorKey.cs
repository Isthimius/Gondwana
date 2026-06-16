namespace Gondwana.Blazor.Input.Keyboard;

/// <summary>
/// Maps browser <c>KeyboardEvent.code</c> values to stable integer key codes used by
/// <see cref="BlazorKeyboardAdapter"/> and the Gondwana <c>KeyboardEventPoller</c>.
/// </summary>
/// <remarks>
/// Enum member names exactly match the browser <c>KeyboardEvent.code</c> strings (e.g.
/// <c>KeyA</c>, <c>ArrowLeft</c>, <c>Space</c>) so that <see cref="Enum.TryParse{T}"/>
/// can be used to convert browser codes directly to key codes without an explicit lookup table.
/// Pass a member name or its integer value to
/// <c>KeyboardEventPoller.StartMonitoringKey</c>.
/// </remarks>
public enum BlazorKey
{
    /// <summary>No key / unknown.</summary>
    None = 0,

    // ── Letters ──────────────────────────────────────────────────────────────
    /// <summary>The A key.</summary>
    KeyA = 1,
    /// <summary>The B key.</summary>
    KeyB = 2,
    /// <summary>The C key.</summary>
    KeyC = 3,
    /// <summary>The D key.</summary>
    KeyD = 4,
    /// <summary>The E key.</summary>
    KeyE = 5,
    /// <summary>The F key.</summary>
    KeyF = 6,
    /// <summary>The G key.</summary>
    KeyG = 7,
    /// <summary>The H key.</summary>
    KeyH = 8,
    /// <summary>The I key.</summary>
    KeyI = 9,
    /// <summary>The J key.</summary>
    KeyJ = 10,
    /// <summary>The K key.</summary>
    KeyK = 11,
    /// <summary>The L key.</summary>
    KeyL = 12,
    /// <summary>The M key.</summary>
    KeyM = 13,
    /// <summary>The N key.</summary>
    KeyN = 14,
    /// <summary>The O key.</summary>
    KeyO = 15,
    /// <summary>The P key.</summary>
    KeyP = 16,
    /// <summary>The Q key.</summary>
    KeyQ = 17,
    /// <summary>The R key.</summary>
    KeyR = 18,
    /// <summary>The S key.</summary>
    KeyS = 19,
    /// <summary>The T key.</summary>
    KeyT = 20,
    /// <summary>The U key.</summary>
    KeyU = 21,
    /// <summary>The V key.</summary>
    KeyV = 22,
    /// <summary>The W key.</summary>
    KeyW = 23,
    /// <summary>The X key.</summary>
    KeyX = 24,
    /// <summary>The Y key.</summary>
    KeyY = 25,
    /// <summary>The Z key.</summary>
    KeyZ = 26,

    // ── Digit row ────────────────────────────────────────────────────────────
    /// <summary>The 1 key on the digit row.</summary>
    Digit1 = 27,
    /// <summary>The 2 key on the digit row.</summary>
    Digit2 = 28,
    /// <summary>The 3 key on the digit row.</summary>
    Digit3 = 29,
    /// <summary>The 4 key on the digit row.</summary>
    Digit4 = 30,
    /// <summary>The 5 key on the digit row.</summary>
    Digit5 = 31,
    /// <summary>The 6 key on the digit row.</summary>
    Digit6 = 32,
    /// <summary>The 7 key on the digit row.</summary>
    Digit7 = 33,
    /// <summary>The 8 key on the digit row.</summary>
    Digit8 = 34,
    /// <summary>The 9 key on the digit row.</summary>
    Digit9 = 35,
    /// <summary>The 0 key on the digit row.</summary>
    Digit0 = 36,

    // ── Navigation / cursor ──────────────────────────────────────────────────
    /// <summary>The left arrow key.</summary>
    ArrowLeft = 37,
    /// <summary>The right arrow key.</summary>
    ArrowRight = 38,
    /// <summary>The up arrow key.</summary>
    ArrowUp = 39,
    /// <summary>The down arrow key.</summary>
    ArrowDown = 40,

    // ── Common special keys ──────────────────────────────────────────────────
    /// <summary>The space bar.</summary>
    Space = 41,
    /// <summary>The Enter / Return key.</summary>
    Enter = 42,
    /// <summary>The Escape key.</summary>
    Escape = 43,
    /// <summary>The Backspace key.</summary>
    Backspace = 44,
    /// <summary>The Tab key.</summary>
    Tab = 45,
    /// <summary>The Delete key.</summary>
    Delete = 46,
    /// <summary>The Insert key.</summary>
    Insert = 47,
    /// <summary>The Home key.</summary>
    Home = 48,
    /// <summary>The End key.</summary>
    End = 49,
    /// <summary>The Page Up key.</summary>
    PageUp = 50,
    /// <summary>The Page Down key.</summary>
    PageDown = 51,

    // ── Modifier keys ────────────────────────────────────────────────────────
    /// <summary>The left Shift key.</summary>
    ShiftLeft = 52,
    /// <summary>The right Shift key.</summary>
    ShiftRight = 53,
    /// <summary>The left Control key.</summary>
    ControlLeft = 54,
    /// <summary>The right Control key.</summary>
    ControlRight = 55,
    /// <summary>The left Alt key.</summary>
    AltLeft = 56,
    /// <summary>The right Alt / AltGr key.</summary>
    AltRight = 57,
    /// <summary>The left Meta / Windows / Command key.</summary>
    MetaLeft = 58,
    /// <summary>The right Meta / Windows / Command key.</summary>
    MetaRight = 59,
    /// <summary>The Caps Lock key.</summary>
    CapsLock = 60,
    /// <summary>The Num Lock key.</summary>
    NumLock = 61,
    /// <summary>The Scroll Lock key.</summary>
    ScrollLock = 62,

    // ── Function keys ────────────────────────────────────────────────────────
    /// <summary>The F1 key.</summary>
    F1 = 63,
    /// <summary>The F2 key.</summary>
    F2 = 64,
    /// <summary>The F3 key.</summary>
    F3 = 65,
    /// <summary>The F4 key.</summary>
    F4 = 66,
    /// <summary>The F5 key.</summary>
    F5 = 67,
    /// <summary>The F6 key.</summary>
    F6 = 68,
    /// <summary>The F7 key.</summary>
    F7 = 69,
    /// <summary>The F8 key.</summary>
    F8 = 70,
    /// <summary>The F9 key.</summary>
    F9 = 71,
    /// <summary>The F10 key.</summary>
    F10 = 72,
    /// <summary>The F11 key.</summary>
    F11 = 73,
    /// <summary>The F12 key.</summary>
    F12 = 74,

    // ── Numpad ───────────────────────────────────────────────────────────────
    /// <summary>Numpad 0.</summary>
    Numpad0 = 75,
    /// <summary>Numpad 1.</summary>
    Numpad1 = 76,
    /// <summary>Numpad 2.</summary>
    Numpad2 = 77,
    /// <summary>Numpad 3.</summary>
    Numpad3 = 78,
    /// <summary>Numpad 4.</summary>
    Numpad4 = 79,
    /// <summary>Numpad 5.</summary>
    Numpad5 = 80,
    /// <summary>Numpad 6.</summary>
    Numpad6 = 81,
    /// <summary>Numpad 7.</summary>
    Numpad7 = 82,
    /// <summary>Numpad 8.</summary>
    Numpad8 = 83,
    /// <summary>Numpad 9.</summary>
    Numpad9 = 84,
    /// <summary>Numpad +.</summary>
    NumpadAdd = 85,
    /// <summary>Numpad -.</summary>
    NumpadSubtract = 86,
    /// <summary>Numpad *.</summary>
    NumpadMultiply = 87,
    /// <summary>Numpad /.</summary>
    NumpadDivide = 88,
    /// <summary>Numpad decimal point.</summary>
    NumpadDecimal = 89,
    /// <summary>Numpad Enter.</summary>
    NumpadEnter = 90,

    // ── Punctuation / symbols ────────────────────────────────────────────────
    /// <summary>The - / _ key.</summary>
    Minus = 91,
    /// <summary>The = / + key.</summary>
    Equal = 92,
    /// <summary>The [ / { key.</summary>
    BracketLeft = 93,
    /// <summary>The ] / } key.</summary>
    BracketRight = 94,
    /// <summary>The \ / | key.</summary>
    Backslash = 95,
    /// <summary>The ; / : key.</summary>
    Semicolon = 96,
    /// <summary>The ' / " key.</summary>
    Quote = 97,
    /// <summary>The ` / ~ key.</summary>
    Backquote = 98,
    /// <summary>The , / &lt; key.</summary>
    Comma = 99,
    /// <summary>The . / &gt; key.</summary>
    Period = 100,
    /// <summary>The / / ? key.</summary>
    Slash = 101,
}
