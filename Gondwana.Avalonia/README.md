# Gondwana.Avalonia

**Gondwana.Avalonia** provides Avalonia UI adapters for rendering and input, mirroring
`Gondwana.WinForms` but targeting all platforms supported by Avalonia: **desktop** (Windows,
Linux, macOS), **WebAssembly**, **Android**, and **iOS / macOS (Catalyst)**.

## Features

- Bitmap rendering surface using Avalonia `WriteableBitmap` (no platform-specific SkiaSharp view package required; works on all Avalonia targets)
- Keyboard input integration (global key capture via Avalonia `TopLevel`)
- Mouse / pointer input integration
- Designed to be consumed by platform-specific Avalonia host projects

## Installation

```bash
dotnet add package Gondwana.Avalonia
```

## Usage

Register Avalonia adapters during application startup (inside your `Window.OnOpened` or similar):

```csharp
Engine.Instance.InitializeAvaloniaKeyboardAdapter(myWindow);
Engine.Instance.InitializeAvaloniaMouseAdapter(renderSurfaceControl);
```

### Key codes

`AvaloniaKeyboardAdapter` uses `(int)Avalonia.Input.Key` values as key codes.  Use
`AvaloniaKeyboardAdapter.GetKeyCodeFromString("Left")` to resolve a key name at runtime.

## Documentation

-   **Source Code**\
    https://github.com/isthimius/Gondwana

-   **Architecture & Guides**\
    https://github.com/isthimius/Gondwana/wiki

-   **API Reference (Doxygen)**\
    https://isthimius.github.io/Gondwana/

## Related Packages

- `Gondwana` – Core engine
- `Gondwana.Avalonia.Hosting` – Avalonia game host

## License

MIT
