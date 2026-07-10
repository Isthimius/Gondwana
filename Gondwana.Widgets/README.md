# Gondwana.Widgets

**Gondwana.Widgets** provides reusable UI and gameplay widgets for the Gondwana game engine,
including menus, dialogs, labels, buttons, overlays, HUD elements, and other
DirectDrawing-friendly 2D/2.5D game controls.

It builds on the core `Gondwana` rendering model and is intended for in-game interface elements
rather than platform-native UI. Widgets are designed to render inside Gondwana scenes, views,
and backbuffers using the same engine-driven drawing pipeline as the rest of the game.

## Features

- Reusable in-game UI widgets for Gondwana projects
- DirectDrawing-friendly controls and overlays
- Game-oriented components such as HUD elements, status bars, labels, dialogs, and menus
- Code-first widget composition with no external editor or scene GUI required
- Designed for 2D and 2.5D games using Gondwana's scene, view, and rendering systems
- Cross-platform-friendly architecture through the core Gondwana rendering pipeline

## Installation

```bash
dotnet add package Gondwana.Widgets
```

## Usage

Add the package to your game project, then create and register widgets as part of your normal
Gondwana scene or DirectDrawing setup.

Example:

```csharp
using Gondwana.Widgets;
```

A typical widget can be used for in-game interface elements such as:

- HUD overlays
- Dialog boxes
- Labels and text panels
- Menu screens
- Buttons and selectable options
- Health bars and status indicators
- NPC conversation boxes

Exact usage depends on the specific widget type being used.

## Widget Scope

`Gondwana.Widgets` is intended for **game UI**, not operating-system UI.

Use it for interface elements that should appear inside the game world or game viewport, such as
dialog boxes, overlays, HUDs, menus, and sprite-adjacent indicators.

For platform-specific hosting and application surfaces, use one of the hosting or adapter packages
instead, such as:

- `Gondwana.WinForms`
- `Gondwana.Avalonia`
- `Gondwana.Blazor`

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Widgets/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine

## License

MIT
