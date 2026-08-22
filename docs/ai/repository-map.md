# Gondwana Repository Map for AI Assistants

This is a navigation map, not an exhaustive architecture document. Use it to locate the likely owner of a behavior, then inspect current source and tests.

## Core Runtime

### `Gondwana/`

The primary engine package.

Start here for engine lifecycle, configuration, scenes, rendering, drawing, sprites, tilesheets, movement, collisions, input abstractions, timers, serialization/state, assets, logging, and other platform-neutral runtime behavior.

Important top-level entry points include types such as `Engine`, `EngineDispatcher`, `EngineManagers`, and `EngineState`.

### `Gondwana.Hosting/`

Platform-neutral hosting and lifecycle orchestration.

Use this when a question concerns common game-host startup, shutdown, initialization order, or behavior shared by multiple platform hosts.

### `Gondwana.Widgets/`

Reusable engine-rendered UI and gameplay widgets.

Use this for HUD elements, splash screens, dialogs, widget lifecycle, focus, pointer/keyboard routing, dragging, and widget composition.

## Platform Adapters and Hosts

### WinForms

- `Gondwana.WinForms/`
- `Gondwana.WinForms.Hosting/`

Use these for Windows-specific render surfaces, presentation, native input wiring, and `WinFormsGameHost`.

### Avalonia

- `Gondwana.Avalonia/`
- `Gondwana.Avalonia.Hosting/`

Use these for cross-platform desktop adapters, presentation/input integration, and `AvaloniaGameHost`.

### Blazor / WebAssembly

- `Gondwana.Blazor/`
- `Gondwana.Blazor.Hosting/`

Use these for browser rendering, JavaScript interop, WebAssembly input/presentation concerns, and `BlazorGameHost`.

Do not move platform-specific behavior into the core merely because more than one demo needs it. First determine whether it belongs in a shared host abstraction or a platform adapter.

## Optional Runtime Packages

- `Gondwana.Audio.Browser/` — browser audio integration.
- `Gondwana.Audio.Midi/` — MIDI and SoundFont support.
- `Gondwana.Input.SDL2/` — optional SDL2 gamepad input.
- `Gondwana.Video/` — experimental video integration.

Inspect package boundaries before introducing dependencies from core runtime code into an optional package.

## Demos

`Demos/` contains current executable examples and game experiments.

Notable examples include:

- `Demos/Spot/` — primary Windows showcase and substantial game example.
- `Demos/SpotAvalonia/` — Avalonia variant.
- `Demos/Spot.Blazor/` — browser/WebAssembly variant.
- `Demos/Gondwana.Platformer/` — platformer-oriented movement/collision example.
- `Demos/Gondwana.SpaceDuel/` — movement, rotation, combat, HUD, and space-game mechanics.
- `Demos/Gondwana.ParticleTest/` — particle behavior.
- `Demos/Gondwana.CoordinateTest/` — coordinate/projection behavior.
- `Demos/Gondwana.Flappy/` — small game/demo example.
- `Demos/WidgetsTest/` — widget examples.

Use demos as examples of public API composition, but verify engine behavior against current source and tests before treating a demo pattern as a contract.

## Tests

### `Testing/Gondwana.Tests/`

The baseline unit and integration test project for the engine and related packages.

When changing behavior:

1. Search for tests mentioning the affected type or subsystem.
2. Identify whether the existing test expresses a deliberate contract.
3. Add a focused regression test when fixing a bug or introducing behavior.
4. Avoid weakening a test merely to make a new implementation pass unless the requested behavior intentionally changes the contract.

## Tooling

### `Tooling/Gondwana.Cli/`

The `gondwana` command-line tool.

### `Tooling/Gondwana.Templates/`

`dotnet new` templates and therefore an important source for current recommended project boilerplate.

### Studio and asset tooling

- `Tooling/Gondwana.Tooling.Studio.Core/`
- `Tooling/Gondwana.Tooling.Studio.Avalonia/`
- `Tooling/Gondwana.Tooling.Studio.WinForms/`
- `Tooling/Gondwana.Tooling.Assets.WinForms/`
- `Tooling/Gondwana.Tooling.Tilesheets.WinForms/`
- `Tooling/scripts/`

Tooling is supplemental. Do not infer that runtime projects require Studio or a proprietary editor-owned project format.

## Repository-Level References

- `README.md` — current public overview, packages, features, demos, and design principles.
- `CHANGELOG.md` — release history; useful for understanding why something changed.
- `ROADMAP.md` — planned work; never treat an unchecked or described item as implemented without source evidence.
- `.github/workflows/` — authoritative automation and CI behavior.
- `Directory.Build.props` and `Directory.Build.targets` — shared build/package configuration.
- `global.json` and `version.json` — SDK/versioning configuration.
- `docs/` — generated/reference documentation assets plus this AI routing layer.
