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
- `GameHost.cs` — `WinFormsGameHost` subclass with `// TODO` override stubs for loading tilesheets, building the scene, and handling keyboard input
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
- `GameHost.cs` — `AvaloniaGameHost` subclass with `// TODO` override stubs for loading tilesheets, building the scene, and handling keyboard input
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
- `GameHost.cs` derives from `WinFormsGpuGameHost` / `AvaloniaGpuGameHost` instead of `WinFormsGameHost` / `AvaloniaGameHost`.

## Further reading

- **Getting started (15-minute guide)** — [first-game-in-15-minutes.md](https://github.com/Isthimius/Gondwana/blob/master/first-game-in-15-minutes.md)
- **Wiki** — https://github.com/Isthimius/Gondwana/wiki
- **API reference** — https://isthimius.github.io/Gondwana/
- **NuGet packages** — https://www.nuget.org/packages/Gondwana
