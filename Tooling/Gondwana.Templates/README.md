# Gondwana Templates

`dotnet new` templates for the [Gondwana Game Engine](https://github.com/Isthimius/Gondwana).

[![NuGet](https://img.shields.io/nuget/v/Gondwana.Templates)](https://www.nuget.org/packages/Gondwana.Templates)

---

## Install

```bash
dotnet new install Gondwana.Templates
```

## Available Templates

| Template | Short name | Description |
|---|---|---|
| Gondwana WinForms Game | `gondwana-winforms` | Starter Windows desktop game using Gondwana + WinForms |
| Gondwana Avalonia Game | `gondwana-avalonia` | Starter cross-platform desktop game using Gondwana + Avalonia (Windows, macOS, Linux) |
| Gondwana Blazor WebAssembly Game | `gondwana-blazor` | Starter browser-based game using Gondwana + Blazor WebAssembly with GPU-backed WebGL rendering |

## Usage

### WinForms (Windows only)

```bash
dotnet new gondwana-winforms -n MyGame
cd MyGame
dotnet run
```

This scaffolds a ready-to-run WinForms project containing:

- `MyGame.csproj` — targets `net8.0-windows` with the four Gondwana packages pre-referenced
- `Program.cs` — `[STAThread]` WinForms entry point
- `GameWindow.cs` — `Form` wired to the engine lifecycle (`OnLoad` → host, `OnShown` → `Initialize`, `OnFormClosed` → `Dispose`)
- `MyGameHost.cs` — `WinFormsGameHostBase` subclass with `// TODO` override stubs for loading tilesheets, building the scene, and handling keyboard input
- `assets/README.txt` — instructions for adding sprites and other asset files

### Avalonia (Windows, macOS, Linux)

```bash
dotnet new gondwana-avalonia -n MyGame
cd MyGame
dotnet run
```

This scaffolds a ready-to-run Avalonia project containing:

- `MyGame.csproj` — targets `net8.0` with the Gondwana and `Avalonia.Desktop` packages pre-referenced
- `Program.cs` — Avalonia `AppBuilder` entry point using `UsePlatformDetect()`
- `App.cs` — `Application` subclass that creates the main window on startup
- `GameWindow.cs` — `Window` wired to the engine lifecycle (`OnOpened` → host + `Initialize`, `OnClosed` → `Dispose`)
- `MyGameHost.cs` — `AvaloniaGameHost` subclass with `// TODO` override stubs for loading tilesheets, building the scene, and handling keyboard input
- `assets/README.txt` — instructions for adding sprites and other asset files

### Choosing a desktop backbuffer

The WinForms and Avalonia templates accept a `--Backbuffer` parameter to choose the rendering backend:

| Value | Description |
|---|---|
| `bitmap` | CPU-based bitmap backbuffer using SkiaSharp **(default)**. Available for WinForms on Windows and Avalonia on Windows, macOS, and Linux. |
| `gpu` | GPU-accelerated OpenGL backbuffer. Requires an OpenGL-capable desktop target. |

```bash
# GPU-accelerated WinForms project
dotnet new gondwana-winforms -n MyGame --Backbuffer gpu

# GPU-accelerated Avalonia project
dotnet new gondwana-avalonia -n MyGame --Backbuffer gpu
```

Omitting `--Backbuffer` is equivalent to passing `--Backbuffer bitmap`.

When `--Backbuffer gpu` is used:
- `GameWindow.cs` uses `WinFormGpuRenderSurfaceControl` / `AvaloniaGpuRenderSurfaceControl` instead of the bitmap variant.
- `MyGameHost.cs` derives from `WinFormsGpuGameHost` / `AvaloniaGpuGameHost` instead of `WinFormsBitmapGameHost` / `AvaloniaGameHost`.

### Blazor WebAssembly (browser-based)

```bash
dotnet new gondwana-blazor -n MyGame
cd MyGame
dotnet run                                    # serve locally and run in your browser
```

Build and publish for browser:

```bash
dotnet workload install wasm-tools            # one-time per machine
dotnet publish -f net8.0-browser -c Release
# Output: bin/Release/net8.0-browser/publish/wwwroot/
```

This scaffolds a Blazor WebAssembly project using Gondwana's GPU-backed WebGL rendering path and containing:

- `MyGame.csproj` — targets `net8.0-browser` with Blazor WebAssembly and Gondwana packages
- `Program.cs` — Blazor WebAssembly entry point with audio module import
- `App.razor` — Root Blazor Router component
- `Pages/Index.razor` — Main page (routed to "/") containing the game
- `GameRenderSurface.razor` — responsive GPU canvas, stable logical-resolution setup, and loading/error overlay; change `LogicalWidth`/`LogicalHeight` here
- `MyGameHost.cs` — `BlazorGpuGameHost` subclass with `// TODO` stubs for loading assets and building the scene
- `wwwroot/index.html` — pre-WASM HTML loading indicator
- `wwwroot/css/startup.css` — responsive layout plus shared pre-WASM and Blazor loader styles
- `assets/README.txt` — instructions for adding sprites and other assets

### Responsive presentation and loading

The scaffold defaults to an **800×600 logical game size**, configurable via
`LogicalWidth` and `LogicalHeight` in `GameRenderSurface.razor`. The
component keeps those values together for browser sizing and the one-time
`ConfigureLogicalResolution` call. CSS fits the entire canvas into phone,
landscape, and desktop viewports without upscaling beyond the desktop size.
It includes a `100vh` fallback and `100dvh` for changing mobile browser chrome.
Once the logical size has been established, browser resize/rotation uses the
existing presentation and pointer/touch coordinate transforms without changing
game-space dimensions. To customize the fit behavior, edit `PresentationStyle`;
omit `ConfigureLogicalResolution` for applications needing variable resolution.

The `index.html` loader appears **before** .NET/WebAssembly starts. A matching
`GameRenderSurface.razor` overlay covers application initialization; an
initialization failure shows an error instead of a permanent loading animation.
Both phases use `wwwroot/css/startup.css` loaded before Blazor. Customize the
game title and mark in `index.html` and `GameRenderSurface.razor`, and adjust
the colors/animation in the stylesheet. These HTML/CSS screens are separate
from an in-engine `SplashScreen`.

The Blazor template intentionally uses WebGL as the default and expected browser rendering backend. Gondwana retains the CPU bitmap Blazor renderer as a compatibility and diagnostic path, but it is not exposed as a normal template option.

**Note:** The `gondwana-audio.js` file is automatically included via the `Gondwana.Audio.Browser` NuGet package.

Audio uses the common `Engine.Managers.AudioResources` API with a platform backend:

- WinForms hosts configure `Gondwana.Audio.NAudio` before `LoadAssets`; the backend is a transitive dependency, so no extra package or initialization is required.
- Blazor `Program.cs` imports the audio module and calls `Engine.Instance.UseBrowserAudio()`. Load URI assets with `LoadFromUri`, placing files under `wwwroot/assets`, and start playback from a user gesture handler. Browser autoplay policy and codec support apply.
- Cross-platform Avalonia (`net8.0`) has no default desktop audio backend. Configure a compatible backend before loading audio. `gondwana add audio` and `gondwana add midi` explain the limitation without retargeting the project. NAudio and the current MIDI implementation require Windows targets.

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/api/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Tooling/Gondwana.Templates/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.Audio.Browser` --- Browser-based audio playback support
-   `Gondwana.Audio.NAudio` --- Windows audio backend, configured automatically by the WinForms host
-   `Gondwana.Audio.Midi` --- Windows MIDI support layered on the NAudio backend
-   `Gondwana.Avalonia` --- Avalonia rendering and input adapters
-   `Gondwana.Avalonia.Hosting` --- Avalonia-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Blazor` --- Blazor WebAssembly rendering and input adapters, including the GPU-backed WebGL path and bitmap compatibility renderer
-   `Gondwana.Blazor.Hosting` --- Blazor-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Hosting` --- Standard platform-agnostic scaffolding for initializing and running Gondwana games
-   `Gondwana.Input.SDL2` --- SDL2-based input handling
-   `Gondwana.Video` --- Video playback support
-   `Gondwana.Video.Widgets` --- Optional interactive video Widgets with opt-in dragging (desktop only)
-   `Gondwana.Widgets` --- UI widget library for creating in-game menus, HUDs, and overlays
-   `Gondwana.WinForms` --- WinForms rendering and input adapters
-   `Gondwana.WinForms.Hosting` --- WinForms-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Cli` --- Command-line interface for scaffolding and managing Gondwana projects

## License

MIT
