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
| Gondwana Avalonia WASM Game | `gondwana-wasm` | Starter cross-platform game using Gondwana + Avalonia, targeting both desktop and browser/WASM |

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
- `MyGameHost.cs` — `WinFormsGameHost` subclass with `// TODO` override stubs for loading tilesheets, building the scene, and handling keyboard input
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

### Choosing a backbuffer

Both templates accept a `--Backbuffer` parameter to choose the rendering backend:

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
- `MyGameHost.cs` derives from `WinFormsGpuGameHost` / `AvaloniaGpuGameHost` instead of `WinFormsGameHost` / `AvaloniaGameHost`.

### Avalonia WASM (desktop + browser)

```bash
dotnet new gondwana-wasm -n MyGame
cd MyGame
dotnet run                                    # run on desktop
```

Build and publish for browser:

```bash
dotnet workload install wasm-tools            # one-time per machine
dotnet publish -f net8.0-browser -c Release
# Output: bin/Release/net8.0-browser/browser-wasm/AppBundle/
```

This scaffolds a project containing:

- `MyGame.csproj` — multi-targets `net8.0` and `net8.0-browser`; `Avalonia.Desktop` / `Avalonia.Browser` and `Gondwana.Audio.Browser` are applied conditionally; defines `BROWSER` constant for the browser target
- `Program.cs` — desktop entry point (`#if !BROWSER`); uses `UsePlatformDetect()` and `StartWithClassicDesktopLifetime()`
- `Program.Browser.cs` — browser entry point (`#if BROWSER`); imports `gondwana-audio.js` via `JSHost.ImportAsync`, then starts Avalonia with `StartBrowserAppAsync()`
- `App.cs` — handles both `IClassicDesktopStyleApplicationLifetime` (desktop) and `ISingleViewApplicationLifetime` (WASM)
- `GameWindow.cs` — desktop `Window` (compiled only when `BROWSER` is not defined)
- `GameView.cs` — browser single-view `UserControl`
- `GameRenderSurface.cs` — thin project-specific subclass of `AvaloniaBitmapRenderSurfaceControl`
- `MyGameHost.cs` — `AvaloniaGameHost` subclass with `// TODO` stubs showing both desktop and browser audio patterns
- `wwwroot/gondwana-audio.js` — the Gondwana browser audio JS module, also shipped by the `Gondwana.Audio.Browser` NuGet package as a content file
- `assets/README.txt` — instructions for adding sprites and other assets

## Further reading

- **Getting started (15-minute guide)** — [Make Your First Game in 15 Minutes](https://github.com/Isthimius/Gondwana/wiki/Make-Your-First-Game-in-15-Minutes)
- **Wiki** — https://github.com/Isthimius/Gondwana/wiki
- **API reference** — https://isthimius.github.io/Gondwana/
- **NuGet packages** — https://www.nuget.org/packages/Gondwana
