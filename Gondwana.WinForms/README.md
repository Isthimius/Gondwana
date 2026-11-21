# Gondwana.WinForms

**Gondwana.WinForms** provides Windows Forms adapters for rendering, input, and audio.

It allows Gondwana applications to run as native WinForms applications with minimal setup.

## Features

- SkiaSharp rendering surface for WinForms
- Input integration (keyboard/mouse/gamepad/native multi-touch)
- Convenient initialization of the `Gondwana.Audio.NAudio` backend (reader and Vorbis registration live in that package)
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
Engine.Instance.InitializeWinFormsTouchAdapter(winFormBitmapRenderSurfaceControl);
Engine.Instance.Input.TouchEventPoller?.StartMonitoringTouch();
```

## Touch on Windows

`Gondwana.WinForms` includes `WinFormsTouchAdapter`, which captures native
`WM_TOUCH` contacts on the render control and its child windows, queues
beginnings/endings so fast taps survive engine polling, and converts positions
to logical Backbuffer pixels. It supports both bitmap and GPU render controls.

`WinFormsGameHostBase` initializes and starts touch monitoring automatically,
**before** it creates the normal `WidgetInputRouter`. Game code can subscribe
to `WidgetInputRouter.UnhandledPrimaryAction` for mouse clicks and touch taps
without duplicating input logic. The WinForms mouse adapter ignores only
Windows compatibility mouse events promoted from touchscreen contacts when
native touch is registered; genuine mouse and pen events continue normally.

The engine owns and disposes the touch adapter on shutdown. Applications
using low-level engine setup rather than a WinForms GameHost must initialize
touch and call `StartMonitoringTouch()` themselves.

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/api/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.WinForms/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.WinForms.Hosting` --- WinForms-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Widgets` --- UI widget library for creating in-game menus, HUDs, and overlays

## License

MIT
