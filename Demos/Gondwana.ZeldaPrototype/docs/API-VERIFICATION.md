# Gondwana API verification

The project was generated against the public `master` branch of `Isthimius/Gondwana` at source commit:

```text
18d60e7d43dc8c88e2ae5bb4aa5e102822227330
```

The public wiki was inspected at commit:

```text
99cf85a820a96c13288ea0271ba671d3b0a9bbe8
```

Verification date: 2026-08-24.

## Project and lifecycle

Checked against:

- `AGENTS.md`
- `docs/ai/README.md`
- `docs/ai/implementation-workflow.md`
- `Tooling/Gondwana.Templates/templates/gondwana-winforms/`
- `Gondwana.Hosting/GameHostBase.cs`
- `Gondwana.WinForms.Hosting/WinFormsGameHostBase.cs`
- `Gondwana.WinForms.Hosting/WinFormsBitmapGameHost.cs`

The prototype follows the template's form lifecycle: create the host after controls exist, call `Initialize` from `OnShown`, and dispose the host when the form closes.

## Sprites, movement, cameras, and collisions

Checked against:

- `Gondwana/Drawing/Sprites/Sprite.cs`
- `Gondwana/Drawing/Sprites/SpriteManager.cs`
- `Gondwana/Physics/Movement/MovementController.Integrated.cs`
- `Gondwana/Physics/Collisions/CollisionResolver.cs`
- `Gondwana/Drawing/Tile.cs`
- `Gondwana/Scenes/SceneLayer.cs`
- `Gondwana/Rendering/Views/Camera.cs`
- `Demos/Gondwana.Platformer/PlatformerGameHost.cs`
- `Testing/Gondwana.Tests/CollisionProfileTests.cs`
- `Testing/Gondwana.Tests/SceneLayerTileCollisionTests.cs`
- wiki: `Sprites`, `Collision Detection`, `Scenes and SceneLayers`, and `Using Views and Cameras`

Important implementation result: collision registries and resolution run per `SceneLayer`. Therefore blocking tiles and movable actors share the gameplay layer. The ground remains a separate non-colliding layer.

## HUD and overlays

Checked against:

- `Gondwana/Drawing/Direct/DirectRectangle.cs`
- `Gondwana/Drawing/Direct/TextBlock.cs`
- `Demos/Gondwana.SpaceDuel/SpaceDuelGameHost.cs`
- wiki: `DirectDrawing` and `Widgets`

All menus and HUD objects are view-bound screen-space drawings. Health bars use world-bound `DirectRectangle` objects and subscribe to sprite movement. The repository's newer `HealthBarWidget` implementation and tests were also reviewed, but the public `2.*` package available during validation did not yet expose `Gondwana.Widgets.Hud`; the local implementation keeps the default package build working without guessing at unreleased package contents.

## Keyboard and controller input

Checked against:

- `Gondwana/Input/Keyboard/KeyboardEventPoller.cs`
- `Gondwana/Input/Gamepad/IGamepadAdapter.cs`
- `Gondwana/Input/Gamepad/GamepadStickState.cs`
- `Gondwana/Input/Gamepad/GamepadEventPoller.cs`
- `Gondwana/EngineInputSystems.cs`
- `Gondwana.WinForms/Input/Gamepad/XInput/XInputGamepadAdapter.cs`
- `Gondwana.WinForms/Input/Gamepad/XInput/XInputGamepadManager.cs`
- wiki: `Keyboard Input Quick Start` and `Gamepad Input Quick Start`

The game snapshots `PressedButtons` to implement rising-edge button actions, as recommended by the gamepad guide, and reads the left stick directly for held movement. Controller Y is inverted because Gondwana's grid uses positive Y downward while XInput describes stick-up as positive.

The current source initializes XInput in `WinFormsGameHostBase.ConfigureGamepads`, then `Engine.Initialize` assigns its optional `gamepadManager` parameter. Because the host does not pass that value into the later call, the prototype explicitly calls `Engine.InitializeXInputGamepadManager()` in `OnEngineInitialized`.

## Save/load

Checked against:

- `Gondwana/EngineState.cs`
- `Gondwana/TypedValueBag.cs`
- `Testing/Gondwana.Tests/TypedValueBagTests.cs`
- wiki: `Serialization and EngineState`

`TypedValueBag` is explicitly excluded from EngineState JSON. Full `EngineState` serialization is registry-oriented and can persist scenes, sprites, resources, and related engine state. A small game-specific JSON DTO is the safer and clearer fit for this prototype's player progress, inventory, pickups, and enemy health.

## Build validation

All Gondwana member names and signatures used by the project were checked against the source commit above, with the closest tests, demos, template, and wiki guidance inspected.

The solution compiled successfully with .NET SDK 8.0.424 in both supported configurations:

- the default public Gondwana `2.*` NuGet references
- direct project references to the pinned Gondwana source checkout above

Both builds completed with zero errors. The dependency graph emits existing compatibility warnings for OpenTK 3.1.0, OpenTK.GLControl 3.1.0, and SkiaSharp.Views.WindowsForms 3.119.2 targeting .NET Framework assets. The host and render surface follow the WinForms GPU template path. Runtime play-testing was not possible in the Linux validation environment because the prototype is a Windows Forms application.

### Fixed-tile compatibility

The current source exposes each fixed tile's collider through `Tile.Collider`. Gondwana 2.5.2 creates its fixed-tile collider in a separate internal `SceneLayerTile.Collider` field, so the inherited public property remains null. The prototype detects that case, creates a public `TileCollider`, and registers it directly with the layer. This preserves normal current-source behavior while keeping the package build playable; it can be removed after the package feed includes the collision work from #264.
