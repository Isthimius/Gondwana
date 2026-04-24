# Gondwana.WinForms

**Gondwana.WinForms** provides Windows Forms adapters for rendering, input, and audio.

It allows Gondwana applications to run as native WinForms applications with minimal setup.

## Features

- SkiaSharp rendering surface for WinForms
- Input integration (keyboard/mouse/gamepad)
- Audio integration
- Designed for desktop Windows applications

## Installation

```bash
dotnet add package Gondwana.WinForms
```

## Usage

Register WinForms adapters during application startup:

```csharp
Engine.Instance.InitializeWinFormsAudioFormats();
Engine.Instance.InitializeXInputGamepadManager();
Engine.Instance.InitializeWinFormsKeyboardAdapter(winFormBitmapRenderSurfaceControl);
Engine.Instance.InitializeWinFormsMouseAdapter(winFormBitmapRenderSurfaceControl);
```

## Documentation

-   **Source Code**\
    https://github.com/isthimius/Gondwana

-   **Architecture & Guides**\
    https://github.com/isthimius/Gondwana/wiki

-   **API Reference (Doxygen)**\
    https://isthimius.github.io/Gondwana/

## Related Packages

- `Gondwana` — Core engine
- `Gondwana.WinForms.Hosting` — WinForms game host

## License

MIT
