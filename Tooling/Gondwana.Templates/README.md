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

## Usage

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

## Further reading

- **Getting started (15-minute guide)** — [first-game-in-15-minutes.md](https://github.com/Isthimius/Gondwana/blob/master/first-game-in-15-minutes.md)
- **Wiki** — https://github.com/Isthimius/Gondwana/wiki
- **API reference** — https://isthimius.github.io/Gondwana/
- **NuGet packages** — https://www.nuget.org/packages/Gondwana
