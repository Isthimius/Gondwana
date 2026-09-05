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
- `GameRenderSurface.razor` — Blazor component wrapping `BlazorGpuRenderSurfaceComponent`
- `MyGameHost.cs` — `BlazorGpuGameHost` subclass with `// TODO` stubs for loading assets and building the scene
- `wwwroot/index.html` — HTML host page with loading indicator
- `assets/README.txt` — instructions for adding sprites and other assets

The Blazor template intentionally uses WebGL as the default and expected browser rendering backend. Gondwana retains the CPU bitmap Blazor renderer as a compatibility and diagnostic path, but it is not exposed as a normal template option.

**Note:** The `gondwana-audio.js` file is automatically included via the `Gondwana.Audio.Browser` NuGet package.

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Tooling/Gondwana.Templates/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.Audio.Browser` --- Browser-based audio playback support
-   `Gondwana.Audio.Midi` --- MIDI playback and sequencing support
-   `Gondwana.Avalonia` --- Avalonia rendering and input adapters
-   `Gondwana.Avalonia.Hosting` --- Avalonia-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Blazor` --- Blazor WebAssembly rendering and input adapters, including the GPU-backed WebGL path and bitmap compatibility renderer
-   `Gondwana.Blazor.Hosting` --- Blazor-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Hosting` --- Standard platform-agnostic scaffolding for initializing and running Gondwana games
-   `Gondwana.Input.SDL2` --- SDL2-based input handling
-   `Gondwana.Video` --- Video playback support
-   `Gondwana.Widgets` --- UI widget library for creating in-game menus, HUDs, and overlays
-   `Gondwana.WinForms` --- WinForms rendering and input adapters
-   `Gondwana.WinForms.Hosting` --- WinForms-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Cli` --- Command-line interface for scaffolding and managing Gondwana projects

## License

MIT
