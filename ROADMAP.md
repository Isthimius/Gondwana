# ROADMAP

_Generated from 32 open GitHub issues on 2026-05-16._

This roadmap is grouped by workstream. Expand each issue title to view full ticket details.

## Demo Projects (2)

<details>
<summary><strong>#122 feat: Scrolling platformer demo — Demos/PlatformerDemo</strong></summary>

- **Issue:** [#122](https://github.com/Isthimius/Gondwana/issues/122)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:51Z
- **Updated:** 2026-05-03T22:24:51Z

### Ticket Details

## Summary
A scrolling platformer demo will showcase `PlatformerController`, `NavigationGrid` pathfinding, and the in-game UI / HUD together in a cohesive, runnable example. FlatRedBall's primary target genre is the platformer; Gondwana needs a reference project for the same.

## Dependencies
- PlatformerController (feat: Built-in PlatformerController) must be merged
- Tilemap / `.tmx` support must be merged
- Pathfinding (feat: Built-in A* pathfinding) must be merged
- In-game UI / HUD layer must be merged

## Scope of Work

### New Project: `Demos/PlatformerDemo/` (Avalonia preferred)
| Component | Details |
|---|---|
| **Level** | Side-scrolling level loaded from `Assets/level.tmx` with platforms, pits, and collectibles |
| **Player** | WASD / arrow keys; `PlatformerController` with jump, coyote-time, double-jump |
| **Enemy** | A patrolling enemy that uses `NavigationGrid` A* to walk along a tilemap path; reverses at level boundaries |
| **Collectibles** | Coin sprites; picking up a coin fires `ToastManager.Show("Coin +1!")` |
| **HUD** | Coin counter (`Label`) and health bar (`ProgressBar`) via the UI layer |
| **Camera** | `MovementController.Follow` with a lead-ahead offset (camera leads the player in the movement direction) |

### Sample Map: `Assets/level.tmx`
- Multi-layer: parallax sky background, solid platform layer, foreground decoration layer
- Object layer: `player_spawn`, `enemy_spawn x3`, `coin x10`, `exit`
- Freely licensed tileset committed with the project

## Acceptance Criteria
- [ ] Player jumps and lands on platforms with correct physics (no tunnelling at normal speeds)
- [ ] Enemy follows a calculated tile path and reverses at boundaries
- [ ] HUD updates in real time (coin counter increments, health bar reflects damage)
- [ ] Level can be modified in Tiled without touching C# code
- [ ] `dotnet run` works on all three desktop platforms

## Key Files / References
- PlatformerController (see feat issue)
- NavigationGrid (see pathfinding issue)
- `Gondwana.UI` (HUD layer)
- `Gondwana/Movement/MovementController.cs`

</details>

<details>
<summary><strong>#121 feat: Tilemap demo project — Demos/TilemapDemo</strong></summary>

- **Issue:** [#121](https://github.com/Isthimius/Gondwana/issues/121)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:50Z
- **Updated:** 2026-05-03T22:24:50Z

### Ticket Details

## Summary
FlatRedBall ships dozens of genre-specific demos. Gondwana's current demos (Spot, CoordinateTest, ParticleTest) don't showcase tile-based level design. Adding a `TilemapDemo` validates the `.tmx` importer and gives developers a working reference project.

## Dependencies
- `.tmx` tilemap support (Tiled integration issue) must be merged first

## Scope of Work

### New Project: `Demos/TilemapDemo/` (Avalonia preferred)
- **Level loading**: load `Assets/level1.tmx` at startup via `TmxImporter`
- **Rendering**: display at least 2 tile layers with different parallax factors
- **Player**: move a sprite with WASD/arrow keys using translation (no platformer physics required — this demo is top-down)
- **Collision**: solid tiles prevent the player from moving through them (AABB vs tile grid)
- **Debug overlay**: hovering a tile displays its name (from the TMX object layer) as a `Label`
- **Camera**: follows the player with a `MovementController.Follow` binding

### Sample Map: `Assets/level1.tmx`
Must be committed to the repo with the project:
- At least 2 tile layers (background + foreground with parallax difference)
- A collision layer defining walkable vs. solid tiles
- An object layer with ≥ 3 named entities: `player_spawn`, `enemy_spawn`, `exit`
- Uses a freely licensed 16×16 tilesheet (e.g., [Kenney.nl tilemap packs](https://kenney.nl/assets))

### Verification
The level should be editable in Tiled (https://www.mapeditor.org/) and reload in-engine without any code changes.

## Acceptance Criteria
- [ ] `dotnet run` in `Demos/TilemapDemo/` starts the game with a visible, multi-layer tiled level
- [ ] Player cannot walk through solid tiles (AABB collision works)
- [ ] Parallax layers scroll at visibly different rates as the player moves
- [ ] Hovering/touching a tile shows its name from the TMX object layer

## Key Files / References
- TmxImporter (see tilemap support issue)
- `Gondwana/Movement/MovementController.cs`
- `Gondwana/Collisions/CollisionResolver.cs`
- Kenney tile assets (public domain): https://kenney.nl/assets/tiny-town

</details>

## Gondwana.Tooling.Studio Tooling (3)

<details>
<summary><strong>#110 feat: Scene / Room Editor panel in Gondwana.Tooling.Studio</strong></summary>

- **Issue:** [#110](https://github.com/Isthimius/Gondwana/issues/110)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:38Z
- **Updated:** 2026-05-03T22:24:38Z

### Ticket Details

## Summary
There is no visual Scene Editor in Gondwana.Tooling.Studio. Developers currently hand-code all scene layout. FlatRedBall Glue and GameMaker's room editor are both central to their workflows. This issue tracks adding a **Scene Editor** dockable panel.

## Scope of Work
Add a `SceneEditorView` (Avalonia UserControl) that:
- Renders a live 2D viewport using an Avalonia canvas (or headless SkiaSharp surface) of the composed scene
- Shows a **tile palette** sourced from `.gondwana-tilesheet` files (left sidebar)
- **Stamp-paint tiles** onto a selected `SceneLayer` with configurable parallax factor
- **Drag-place** named `Sprite` entity instances at world coordinates
- **Draw axis-aligned `Aabb` collision boxes** visually (rectangle tool)
- Camera pan (middle-mouse / space+drag) and zoom (scroll wheel)
- Serialises / deserialises the scene to `.gondwana-scene` JSON

### `.gondwana-scene` File Format
```json
{
  "layers": [
    {
      "name": "background",
      "parallax": 0.5,
      "tilesheet": "tiles.gondwana-tilesheet",
      "tiles": [ { "tileIndex": 3, "x": 0, "y": 0 } ]
    }
  ],
  "entities": [
    { "name": "player_spawn", "x": 64, "y": 64 }
  ],
  "colliders": [
    { "x": 0, "y": 112, "width": 320, "height": 16 }
  ]
}
```

A runtime `SceneLoader` reads `.gondwana-scene` and constructs engine objects accordingly.

## Acceptance Criteria
- [ ] Tiles painted in the editor match the runtime rendering exactly
- [ ] Scene serialises and deserialises without data loss (round-trip test)
- [ ] Camera pan and zoom work smoothly
- [ ] The existing Spot demo level can be recreated from a `.gondwana-scene` file

## Dependencies
- Tilesheet Editor (#5) for tile palette source

## Key Files / References
- `Gondwana/Scenes/Scene.cs`
- `Gondwana/Scenes/SceneLayer.cs`
- `Gondwana/Drawing/Tilesheets/TilesheetRegistry.cs`
- `Tooling/Gondwana.Tooling.Studio.Avalonia/Views/`

</details>

<details>
<summary><strong>#109 feat: Animation Editor panel in Gondwana.Tooling.Studio</strong></summary>

- **Issue:** [#109](https://github.com/Isthimius/Gondwana/issues/109)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:37Z
- **Updated:** 2026-05-03T22:24:37Z

### Ticket Details

## Summary
There is no visual animation editor in Gondwana.Tooling.Studio. FlatRedBall Glue ships a full animation editor for defining frame sequences with timing. This issue tracks adding an **Animation Editor** dockable panel to Gondwana.Tooling.Studio.

## Scope of Work
Add an `AnimationEditorView` (Avalonia UserControl) that:
- Loads a `.gondwana-tilesheet` file as its source (tile picker grid on the left)
- Allows drag-drop ordering of tile thumbnails into a **FrameSequence** list (right panel)
- Per-frame duration editor (milliseconds, inline)
- Live playback preview at configured FPS (press ▶ to preview, ■ to stop)
- `CycleType` selector: Once / Loop / PingPong (maps to existing `CycleType` enum)
- Exports named animation assets as `.gondwana-animation` JSON

### `.gondwana-animation` File Format
```json
{
  "tilesheetPath": "sprites.gondwana-tilesheet",
  "name": "walk_right",
  "cycleType": "Loop",
  "frames": [
    { "tileIndex": 0, "durationMs": 100 },
    { "tileIndex": 1, "durationMs": 100 },
    { "tileIndex": 2, "durationMs": 100 }
  ]
}
```

### Engine Integration
The exported format maps 1:1 to:
- `Gondwana/Drawing/Animation/FrameSequence.cs`
- `Gondwana/Drawing/Animation/CycleType.cs`

No new engine code is required — loading is purely deserialization.

## Acceptance Criteria
- [ ] Tile picker loads tiles from a `.gondwana-tilesheet` file
- [ ] Frames can be added, reordered, and their durations edited inline
- [ ] Live preview plays the animation correctly at the configured FPS
- [ ] Exported `.gondwana-animation` deserializes into a working `FrameSequence` in the engine
- [ ] Opening the editor from the directory panel double-click works on existing `.gondwana-animation` files

## Key Files / References
- `Gondwana/Drawing/Animation/FrameSequence.cs`
- `Gondwana/Drawing/Animation/Cycle.cs`
- `Gondwana/Drawing/Animation/CycleType.cs`
- `Gondwana/Drawing/Animation/Animator.cs`
- `Tooling/Gondwana.Tooling.Studio/Views/`

</details>

<details>
<summary><strong>#108 feat: Tilesheet Editor panel in Gondwana.Tooling.Studio</strong></summary>

- **Issue:** [#108](https://github.com/Isthimius/Gondwana/issues/108)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:36Z
- **Updated:** 2026-05-03T22:24:36Z

### Ticket Details

## Summary
Gondwana.Tooling.Studio currently has directory and asset-file views but no visual sprite/tilesheet editor. FlatRedBall Glue and GameMaker both have integrated tilesheet/animation editors. This issue tracks adding a **Tilesheet Editor** dockable panel to Gondwana.Tooling.Studio.

## Current State
The IDE lives at `Tooling/Gondwana.Tooling.Studio.Avalonia/`. It uses Avalonia with dockable windows (`Docking/`) and already has asset file panels (`Views/AssetFilesView.axaml`).

## Scope of Work
Add a `TilesheetEditorView` (Avalonia UserControl) that:
- Opens an image file (PNG, BMP) via drag-drop or file picker
- Overlays a configurable tile grid (tile width × tile height, with live pixel preview)
- Allows naming individual tiles or ranges by clicking/selecting cells
- Exports a `.gondwana-tilesheet` JSON metadata file

### `.gondwana-tilesheet` File Format
```json
{
  "imagePath": "relative/path.png",
  "tileWidth": 16,
  "tileHeight": 16,
  "tiles": [
    { "index": 0, "name": "grass" },
    { "index": 1, "name": "dirt" }
  ]
}
```
This must be deserializable by `TilesheetRegistry` at runtime.

### Integration
- Register as a dockable panel in `MainWindow.axaml`
- Add **File → New → Tilesheet** menu entry
- Open existing `.gondwana-tilesheet` files from the directory panel double-click

## Acceptance Criteria
- [ ] User can open a PNG, set tile dimensions, and see the grid overlay immediately
- [ ] Clicking a tile cell opens an inline name-editor for that tile
- [ ] Saving exports a valid `.gondwana-tilesheet` JSON
- [ ] Runtime `TilesheetRegistry` can load the exported file and render tiles correctly

## Key Files / References
- `Tooling/Gondwana.Tooling.Studio.Avalonia/Views/`
- `Tooling/Gondwana.Tooling.Studio.Avalonia/Docking/`
- `Gondwana/Drawing/Tilesheets/Tilesheet.cs`
- `Gondwana/Drawing/Tilesheets/TilesheetRegistry.cs`

</details>

## Gameplay & Engine Systems (4)

<details>
<summary><strong>#107 feat: HealthComponent and DamageSource entity lifecycle system</strong></summary>

- **Issue:** [#107](https://github.com/Isthimius/Gondwana/issues/107)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:35Z
- **Updated:** 2026-05-03T22:24:35Z

### Ticket Details

## Summary
FlatRedBall ships a standardized damage and health system. Gondwana has no such concept. This issue tracks adding optional, composable `HealthComponent` and `DamageSource` types that plug into collision callbacks.

## Scope of Work

### `Gondwana.HealthComponent`
```csharp
public class HealthComponent
{
    public float MaxHealth { get; set; }
    public float CurrentHealth { get; private set; }
    public bool IsAlive { get; private set; }
    public bool IsInvincible { get; set; }
    public TimeSpan InvincibilityWindow { get; set; }

    public void TakeDamage(float amount, DamageSource source);
    public void Heal(float amount);
    public void Kill();

    public event EventHandler Damaged;
    public event EventHandler Healed;
    public event EventHandler Died;
}
```

### `Gondwana.DamageSource`
```csharp
public record DamageSource(float Amount, DamageType Type, object? Owner = null);
public enum DamageType { Physical, Environmental, Poison, Fire }
```

### Collision Convenience Extension
Add `result.ApplyDamage(source)` on `CollisionResult` — looks up a `HealthComponent` on the colliding entity and calls `TakeDamage`.

## Design Goals
- Opt-in and additive — no existing types change signatures
- Zero allocations in the hot path (no LINQ, no boxing in `TakeDamage`)
- `InvincibilityWindow` uses the engine's existing `HighResTimer`
- `HealthComponent` is not tied to `Sprite`; attach via composition

## Acceptance Criteria
- [ ] `TakeDamage` / `Heal` correctly adjusts `CurrentHealth` with min/max clamping
- [ ] `Died` event fires exactly once when health reaches zero
- [ ] Invincibility window blocks damage for its configured duration
- [ ] Works in the existing Spot demo (enemy damages player on collision)

## Key Files / References
- `Gondwana/Collisions/CollisionResult.cs`
- `Gondwana/Timers/HighResTimer.cs`
- FlatRedBall damage system: https://docs.flatredball.com/flatredball/tutorials/damage-dealing

</details>

<details>
<summary><strong>#106 feat: Built-in A* pathfinding with PathFollowMovementScript</strong></summary>

- **Issue:** [#106](https://github.com/Isthimius/Gondwana/issues/106)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:33Z
- **Updated:** 2026-05-03T22:24:33Z

### Ticket Details

## Summary
Gondwana has no pathfinding system. Both FlatRedBall (node network from Tiled) and GameMaker (`mp_grid`, `path_find`) provide built-in pathfinding. This issue tracks adding a `NavigationGrid` and A* implementation that integrates with the existing scene and movement layers.

## Scope of Work

### `Gondwana.Movement.NavigationGrid`
- Wraps a `SceneLayer`'s tile grid (or an explicit `bool[,]` walkability map)
- Exposes `IReadOnlyList FindPath(WorldPoint start, WorldPoint end)`
- A* with Manhattan / diagonal cost options (configurable)
- Supports dynamic walkability updates at runtime (for moving obstacles)
- Optional: path-smoothing post-process (string-pull / funnel algorithm)

### `Gondwana.Movement.Scripted.PathFollowMovementScript`
- Consumes a `NavigationGrid` path and drives a `Sprite` along it using existing `ScriptedMovement` infrastructure
- Events: `PathCompleted`, `WaypointReached`
- Properties: speed, smooth-turn radius, loop mode

## Acceptance Criteria
- [ ] `NavigationGrid.FindPath()` returns an optimal path on a simple grid map
- [ ] `PathFollowMovementScript` moves a sprite to a destination without getting stuck on tile corners
- [ ] Dynamic walkability changes (blocking/unblocking cells at runtime) are respected on the next `FindPath` call
- [ ] Integrates with existing `MovementController` / `ScriptedMovement` design without breaking the API
- [ ] Demo or test showing an AI entity following a calculated path

## Key Files / References
- `Gondwana/Movement/Scripted/ScriptedMovement.cs`
- `Gondwana/Movement/MovementController.cs`
- `Gondwana/Scenes/SceneLayer.cs` (tile grid backing store)
- FlatRedBall pathfinding: https://docs.flatredball.com/flatredball/ai/pathfinding

</details>

<details>
<summary><strong>#105 feat: Built-in PlatformerController with gravity, jump, and coyote-time</strong></summary>

- **Issue:** [#105](https://github.com/Isthimius/Gondwana/issues/105)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:32Z
- **Updated:** 2026-05-03T22:24:32Z

### Ticket Details

## Summary
Gondwana ships `MovementController` with follow/scripted/integrated modes and AABB collision, but has no ready-made platformer physics. FlatRedBall ships production-ready platformer movement out of the box. This issue tracks adding a `PlatformerController` on top of existing primitives.

## Scope of Work
Add `Gondwana.Movement.PlatformerController` that wraps `MovementController.Integrated` and provides:

- **Gravity** — configurable `float GravityAcceleration` and `float MaxFallSpeed`
- **Ground detection** — via `CollisionResolver` AABB bottom-edge test
- **Jump** — `Jump()` method with configurable peak height and apex time (computes initial velocity automatically)
- **Coyote time** (`float CoyoteTimeSec = 0.1f`) — grace window for jumping after walking off a ledge
- **Jump buffering** (`float JumpBufferSec = 0.083f`) — queued jump input processed on next landing
- **Wall-slide** — optional, with configurable friction coefficient
- **Horizontal deceleration** — ground friction vs. air drag curve

Integration requirements:
- Works with `CollisionGroupRegistry` and `ICollisionMovableEntity`
- Additive — does not replace `MovementController`; wraps it
- Physics parameters are data-driven and tweakable at runtime
- Demonstrate in a new `Demos/Platformer` project

## Acceptance Criteria
- [ ] Player falls with gravity and lands on solid tiles/sprites
- [ ] Jump reaches a predictable arc height with configurable peak/apex parameters
- [ ] Coyote time and jump buffering work correctly and independently
- [ ] A `Demos/Platformer` project compiles and runs on both WinForms and Avalonia

## Key Files / References
- `Gondwana/Movement/MovementController.Integrated.cs`
- `Gondwana/Collisions/CollisionResolver.cs`
- `Gondwana/Collisions/Aabb.cs`
- FlatRedBall platformer: https://docs.flatredball.com/flatredball/tutorials/platformer

</details>

<details>
<summary><strong>#22 Gondwana primitives</strong></summary>

- **Issue:** [#22](https://github.com/Isthimius/Gondwana/issues/22)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-01-31T00:07:59Z
- **Updated:** 2026-04-25T13:30:25Z

### Ticket Details

- all System.Drawing to SkiaSharp or Gondwana.Drawing.Primitives namespaces (including Point, Rectangle, Color, etc.)
- deprecate / remove System.Drawing <--> SkiaSharp helpers in Gondwana
- find any obsolete calls, refactor

-----
- all Canvas calls to Backbuffer
	- DrawBitmap
	- DrawImage
	- RestoreToCount (GpuBackbuffer only)
	- DrawRect
	- DrawPath
	- DrawPoints
	- Save
	- ClipRect
	- Restore
	- SaveLayer
	- SetMatrix
	- ResetMatrix
	- Translate (ViewRenderer, Viewport only, commented)
	- Clear
	- RotateDegrees
	- DrawRoundRect
	- DrawCircle
	- DrawText

</details>

## UI & Interaction (5)

<details>
<summary><strong>#117 feat: DialogueBox and ToastManager for in-game text and notifications</strong></summary>

- **Issue:** [#117](https://github.com/Isthimius/Gondwana/issues/117)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:46Z
- **Updated:** 2026-05-03T22:24:46Z

### Ticket Details

## Summary
FlatRedBall ships a `ToastManager` for transient notification overlays. Neither Gondwana nor common game-engine starting points include RPG-style dialogue boxes. This issue tracks adding `DialogueBox` and `ToastManager` built on top of the UI layer (see HUD layer issue).

## Dependencies
- In-game UI / HUD layer must be implemented first (it provides `HudLayer` and `Label`)

## Scope of Work

### `ToastManager`
```csharp
// Global singleton, registered with HudLayer
ToastManager.Show("Picked up Sword!", duration: TimeSpan.FromSeconds(2));
ToastManager.Show("Level Up!", style: ToastStyle.Success);
```
- Manages a FIFO queue of timed text notifications
- Slide-in / fade-out animation (configurable duration, easing curve)
- Configurable screen position (default: top-centre)
- Clears automatically after duration; also exposes `Dismiss()` for programmatic removal

### `DialogueBox`
```csharp
var dlg = new DialogueBox();
dlg.Show(new[] {
    new DialogueLine(speaker: "Elf",  text: "The dungeon is dangerous!"),
    new DialogueLine(speaker: "Hero", text: "I can handle it."),
});
dlg.DialogueCompleted += OnDialogueDone;
```
- Renders speaker name + body text with a configurable **typewriter effect** (characters/second)
- Advance on configurable key press or mouse click
- Second press on same line immediately reveals the full line (skip typewriter)
- Optional portrait `Sprite` slot in the dialogue frame
- Raises `DialogueCompleted` when all lines are shown and dismissed
- Raises `LineChanged` on each advance

## Acceptance Criteria
- [ ] `ToastManager.Show()` displays a timed notification that auto-dismisses after its duration
- [ ] Multiple toasts queue correctly and don't overlap
- [ ] `DialogueBox` advances through lines correctly on input key/click
- [ ] Typewriter effect can be skipped with a second press
- [ ] Both work in the Spot demo without affecting existing game logic

## Key Files / References
- Depends on: HUD layer (`Gondwana.UI`)
- `Gondwana/Input/Keyboard/`
- `Gondwana/Input/Mouse/`
- FlatRedBall ToastManager: https://docs.flatredball.com/flatredball/tutorials/toast

</details>

<details>
<summary><strong>#116 feat: In-game UI / HUD layer with core widgets (Label, Button, ProgressBar, Panel)</strong></summary>

- **Issue:** [#116](https://github.com/Isthimius/Gondwana/issues/116)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:45Z
- **Updated:** 2026-05-03T22:24:45Z

### Ticket Details

## Summary
Gondwana has no in-game UI widget layer. FlatRedBall ships FlatRedBall.Forms (MVVM WPF-style). GameMaker 2024+ has Flex Panels for responsive UI. This issue tracks a minimal, additive HUD/UI system layered on top of the existing renderer.

## Design Principles
- Code-first API consistent with Gondwana's overall philosophy
- `HudLayer` sits above all `SceneLayer`s in the `View` compositor (rendered last, screen-space)
- Input events wired from the existing `IMouseInput` polling
- v1: direct property manipulation — no MVVM binding required

## Scope of Work

### Package / Namespace: `Gondwana.UI`
| File | Purpose |
|---|---|
| `HudLayer.cs` | Registers with `ViewRenderer`, hosts and draws widgets |
| `Widget.cs` | Base class: `Position`, `Size`, `Visible`, `ZOrder`, `Parent` |
| `Label.cs` | Text display, backed by existing `FontManager` |
| `Button.cs` | `Label` + hit-test + `Clicked` event |
| `Panel.cs` | Rectangular container; background colour or image |
| `ProgressBar.cs` | `Value` (0–1), foreground/background colours, horizontal/vertical |
| `StackPanel.cs` | Simple vertical/horizontal auto-layout container |

All widgets render via SkiaSharp `SKCanvas`.

### Input Wiring
```csharp
// In Engine cycle, HudLayer checks mouse state and dispatches events:
if (mouseInput.IsLeftButtonJustReleased() && widget.HitTest(mousePos))
    widget.RaiseClicked();
```

## Acceptance Criteria
- [ ] A `Label` renders text at a screen-space position with correct font and colour
- [ ] A `Button` fires `Clicked` when left mouse button is released inside its bounds
- [ ] A `ProgressBar` fills proportionally to its `Value` property (0 = empty, 1 = full)
- [ ] `StackPanel` correctly spaces children vertically and horizontally
- [ ] The Spot demo can display a simple HUD (player turn indicator + a label) without visual regression

## Key Files / References
- `Gondwana/Input/Mouse/`
- `Gondwana/Drawing/Direct/TextBlock.cs`
- `Gondwana/Assets/` (FontManager)
- FlatRedBall.Forms docs: https://docs.flatredball.com/flatredball/gui/forms

</details>

<details>
<summary><strong>#33 Slider todo</strong></summary>

- **Issue:** [#33](https://github.com/Isthimius/Gondwana/issues/33)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-02-20T16:19:00Z
- **Updated:** 2026-02-20T16:19:00Z

### Ticket Details

- high scores (total time; total slides, reset after running Shuffle or Load New puzzle)
- save current puzzle (script TotalTimeRunning)
- icon
- Progress bar when loading puzzle
- shrink pics that are bigger than screen on load
- Hold shift to display (DirectRectangle and DirectText) coordinates

</details>

<details>
<summary><strong>#32 BUG: deadlock(?) issue on Slider</strong></summary>

- **Issue:** [#32](https://github.com/Isthimius/Gondwana/issues/32)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-02-04T16:35:51Z
- **Updated:** 2026-02-04T16:37:49Z

### Ticket Details

Slider will occasionally freeze. Previously double-click would cause, but that appears to have been fixed. However, clicking the "empty" square seems to trigger it occasionally. Not easily reproducible. Suspected race-condition with Winform mouse event / Gondwana mouse event.

</details>

<details>
<summary><strong>#24 UI tooling</strong></summary>

- **Issue:** [#24](https://github.com/Isthimius/Gondwana/issues/24)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-02-03T20:57:19Z
- **Updated:** 2026-02-04T16:47:58Z

### Ticket Details

- Avalonia skeleton: https://chatgpt.com/c/69811db9-5e64-8332-a5df-12bd47b485e6 
- scripting: https://chatgpt.com/c/69812496-6e6c-832d-a0dd-8cab90a3e4cc

</details>

## Rendering & Visual Systems (10)

<details>
<summary><strong>#115 feat: Rendering pipeline extensibility — IRenderPass abstraction</strong></summary>

- **Issue:** [#115](https://github.com/Isthimius/Gondwana/issues/115)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:44Z
- **Updated:** 2026-05-03T22:24:44Z

### Ticket Details

## Summary
This is listed in the README roadmap as "Enhancing rendering pipeline extensibility." FlatRedBall and GameMaker both allow developers to inject custom draw code at defined stages. Currently `ViewRenderer` iterates scene layers in a fixed order with no insertion points for custom draw callbacks.

## Problem
There is no way to:
- Draw a custom parallax background before scene layers
- Insert a lighting pass between two tile layers
- Draw screen-space UI on top of everything without reimplementing the render loop

## Scope of Work

### `IRenderPass` Interface
```csharp
public interface IRenderPass
{
    int Order { get; }   // lower = earlier; ties are stable-sorted by registration order
    void Draw(RenderContext context, SKCanvas canvas);
}
```

### Wiring
- Add `View.RenderPasses` (or `Scene.GlobalRenderPasses`) collection
- Wrap existing draw logic into built-in pass implementations:
  - `SceneLayerRenderPass` (wraps current SceneLayer draw loop)
  - `DirectDrawingRenderPass` (wraps current DirectDrawingManager.Draw call)
- Named insertion points as constants: `RenderOrder.BeforeScene = 0`, `RenderOrder.AfterScene = 1000`, `RenderOrder.Overlay = 2000`

### Custom Pass Registration
```csharp
myView.RenderPasses.Add(new LightingRenderPass { Order = RenderOrder.AfterScene + 1 });
myView.RenderPasses.Add(new HudRenderPass    { Order = RenderOrder.Overlay });
```

## Acceptance Criteria
- [ ] A custom `IRenderPass` added to a `View` draws at the correct stage relative to scene layers
- [ ] Removing a pass at runtime takes effect on the next frame (no stale references)
- [ ] Existing rendering in the Spot demo is pixel-identical before/after this change
- [ ] At least two of the built-in systems (SceneLayer, DirectDrawing) are migrated to use `IRenderPass` internally

## Key Files / References
- `Gondwana/Rendering/Views/`
- `Gondwana/Rendering/RenderContext.cs`
- README roadmap entry: _"Enhancing rendering pipeline extensibility"_

</details>

<details>
<summary><strong>#114 feat: Basic 2D lighting system (point lights, darkness layer, additive blend)</strong></summary>

- **Issue:** [#114](https://github.com/Isthimius/Gondwana/issues/114)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:43Z
- **Updated:** 2026-05-03T22:24:43Z

### Ticket Details

## Summary
Neither FlatRedBall nor GameMaker ships a deeply sophisticated 2D lighting engine, but both support sprite tinting, additive blending, and simple composited light sources. Gondwana has no lighting layer at all. This issue tracks adding a lightweight layered 2D lighting system.

## Approach: Darkness-Layer Compositing
1. Render the scene normally to the main backbuffer
2. Fill a separate **darkness layer** (solid black with configurable alpha = ambient darkness)
3. For each `LightSource`, punch a radial-gradient "hole" in the darkness layer using multiply blend
4. Composite the darkness layer over the scene using multiply blend

This avoids shadow geometry entirely and is fast enough for many 2D games.

## Scope of Work

### `Gondwana.Drawing.LightSource`
```csharp
public class LightSource : IDirectDrawable
{
    public LightType Type { get; set; }   // Point | Directional | Ambient
    public SKColor Color { get; set; }
    public float Intensity { get; set; } // 0–1
    public float Radius { get; set; }    // world units (point lights)
    public float SoftEdgeFalloff { get; set; }
}
```

### `LightingLayer` (scene layer)
- Aggregates all `LightSource` instances registered in a `Scene`
- Renders the darkness surface via SkiaSharp radial gradient `SKShader` + multiply `SKBlendMode`
- `Scene.AmbientLight` property (0.0 = pitch black, 1.0 = fully lit, no darkness layer drawn)

## Acceptance Criteria
- [ ] A single point light produces a visible lit circle with soft edges
- [ ] Multiple lights composite correctly without blending artefacts
- [ ] `AmbientLight = 1.0f` is a zero-cost no-op (darkness layer not rendered)
- [ ] Works with both BitmapBackbuffer and GpuBackbuffer
- [ ] No visible frame-rate regression with up to 16 light sources in a scene

## Key Files / References
- `Gondwana/Scenes/SceneLayer.cs`
- `Gondwana/Drawing/Direct/DirectDrawingManager.cs`
- `Gondwana/Rendering/Backbuffers/` (both backbuffer implementations)

</details>

<details>
<summary><strong>#113 feat: Shader / post-process effect pipeline (SKRuntimeEffect / SkSL)</strong></summary>

- **Issue:** [#113](https://github.com/Isthimius/Gondwana/issues/113)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:42Z
- **Updated:** 2026-05-03T22:24:42Z

### Ticket Details

## Summary
FlatRedBall supports post-process shaders (bloom, CRT, custom). GameMaker ships GLSL ES. Gondwana uses SkiaSharp / Skia, which exposes `SKRuntimeEffect` (SkSL shader language) for per-pixel and full-screen effects. This issue tracks a first-class shader/effect API.

## Scope of Work

### Per-Sprite Color Filters
Add `ShaderEffect` abstract base class wrapping `SKColorFilter` or `SKImageFilter`, and apply via `Sprite.ShaderEffect` (null = no effect).

Built-in implementations:
| Class | Description |
|---|---|
| `GrayscaleEffect` | Full desaturation |
| `TintEffect(SKColor color, float strength)` | Colour tint overlay |
| `ChromaticAberrationEffect(float strength)` | RGB channel offset |
| `OutlineEffect(SKColor color, float thickness)` | Sprite outline |

### Full-Screen Post-Process Passes (GPU Backbuffer)
Add a `PostProcessPass` list to `GpuBackbuffer`, applied after scene composition.

Built-in passes:
| Class | Description |
|---|---|
| `BloomPass` | Brighten over-exposed areas |
| `CrtScanlinePass` | Classic CRT scanlines |
| `VignettePass` | Darken screen edges |

Custom: implement `IPostProcessPass.Apply(SKSurface input, SKSurface output)`.

### SkSL Custom Shaders
For advanced users: `SkslShaderEffect` compiles a user-supplied SkSL string and binds named uniform parameters:
```csharp
var effect = new SkslShaderEffect("""
    uniform float time;
    half4 main(float2 fragCoord) {
        return half4(sin(time + fragCoord.x * 0.01), 0, 0, 1);
    }
""");
effect.SetUniform("time", elapsedSec);
```

## Acceptance Criteria
- [ ] `TintEffect` correctly tints a single sprite without affecting others in the same frame
- [ ] `BloomPass` visibly brightens over-exposed areas in the GPU backbuffer path
- [ ] A custom `SkslShaderEffect` with a simple SkSL program compiles and renders without crashing
- [ ] No regression in the existing Spot demo render output

## Key Files / References
- `Gondwana/Rendering/Backbuffers/GpuBackbuffer.cs`
- `Gondwana/Drawing/Sprites/Sprite.cs`
- SkiaSharp SKRuntimeEffect: https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skruntimeeffect
- SkSL language reference: https://skia.org/docs/user/sksl/

</details>

<details>
<summary><strong>#40 make DirectDrawing / ParticleEmitters serializable</strong></summary>

- **Issue:** [#40](https://github.com/Isthimius/Gondwana/issues/40)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-04-15T15:52:20Z
- **Updated:** 2026-04-15T15:52:53Z

### Ticket Details

_No ticket description provided._

</details>

<details>
<summary><strong>#39 Viewport scaling</strong></summary>

- **Issue:** [#39](https://github.com/Isthimius/Gondwana/issues/39)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-03-28T20:15:27Z
- **Updated:** 2026-03-28T20:15:27Z

### Ticket Details

- Viewport Scaling (https://chatgpt.com/c/6967f8c8-b698-832e-99a6-c5c68d8ec862)
	- cheap resolution scaling
		- https://chatgpt.com/c/6933640b-0b94-8332-b525-fbe2ee1fad39 
		- https://chatgpt.com/c/6946f12f-ceec-832e-9054-ae5af429fdfa

</details>

<details>
<summary><strong>#38 Sprite rotation</strong></summary>

- **Issue:** [#38](https://github.com/Isthimius/Gondwana/issues/38)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-03-28T20:13:26Z
- **Updated:** 2026-03-28T20:13:26Z

### Ticket Details

implement Sprite rotation, ensure serializable

</details>

<details>
<summary><strong>#31 re-introduce Effects</strong></summary>

- **Issue:** [#31](https://github.com/Isthimius/Gondwana/issues/31)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-02-04T16:26:24Z
- **Updated:** 2026-02-04T16:46:56Z

### Ticket Details

Some already implemented, this is the list from way back:
 
- Earthquake
- Erase
- FadeIn - View
- FadeIn - SceneLayer
- FadeOut - View
- FadeOut - SceneLayer
- Fill
- SlideIn
- SlideOut
- Zoom In
- Zoom Out

</details>

<details>
<summary><strong>#26 implement per-View Camera rotation</strong></summary>

- **Issue:** [#26](https://github.com/Isthimius/Gondwana/issues/26)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-02-03T21:03:59Z
- **Updated:** 2026-02-04T16:46:54Z

### Ticket Details

need reqs

</details>

<details>
<summary><strong>#21 fog enhancements</strong></summary>

- **Issue:** [#21](https://github.com/Isthimius/Gondwana/issues/21)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-01-30T20:48:45Z
- **Updated:** 2026-02-04T16:46:52Z

### Ticket Details

After moving the fogging to a separate layer, add the following:

- different fog pens / paints / translucency
- if / where fogging is applied to a specific Tile, add the ability to apply custom fog-of-war polygon / include extra top space

</details>

<details>
<summary><strong>#20 create fog compositing layer</strong></summary>

- **Issue:** [#20](https://github.com/Isthimius/Gondwana/issues/20)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-01-30T20:31:24Z
- **Updated:** 2026-02-04T16:46:52Z

### Ticket Details

Currently fog is applied in PostDrawTiles at a per-frame basis at the individual Tile (i.e., ScenceLayerTile and Sprite):

```
if (tile.EnableFog)
{
    using var path = new SKPath();
    path.AddPoly(ptsScreen, close: true);
    Canvas.DrawPath(path, FogPaint);
}
```

The issue is that the rendering is not idempotent, and is drawn multiple times when a sprite moves across a "fogged" tile, and in other corner cases. This results in darkening of the "fog".

The desired solution is introduce an additional "fog" / "effects" layer that adds per viewport, after the Scene has been composited, but before the View-based DirectDrawing instances have been overlayed.

-----

_Copied from ChatGPT; YMMV:_

For translucent fog to be idempotent under partial redraw, you need the fog to be composited from a buffer that doesn’t contain last frame’s fog.
Minimal correct approach (no pipeline refactor): draw fog into a separate “fog mask” surface, then composite it
•	Keep your existing render pipeline exactly as-is.
•	Stop drawing fog directly onto Backbuffer.Canvas.
•	Instead:
1.	maintain a persistent SKSurface _fogSurface the same size as the backbuffer
2.	for each fogged tile, clear only that tile polygon on _fogSurface (so fog doesn’t accumulate on the fog surface)
3.	redraw the translucent fog polygon onto _fogSurface
4.	after you finish iterating tiles, draw _fogSurface onto the backbuffer canvas (SrcOver), clipped to the current clipRect
This preserves translucency because the scene remains on the backbuffer; only the fog overlay is reset/rebuilt.
________________________________________
Patch: BackbufferBase (drop-in)

**1) Add fields**
```
private SKSurface? _fogSurface;

private static readonly SKPaint FogMaskClearPaint = new SKPaint
{
    BlendMode = SKBlendMode.Clear,
    IsAntialias = true
};
```

**2) Ensure _fogSurface exists and matches the backbuffer size**
Call this at the start of DrawDrawables (or wherever you know Canvas size is valid). Use your backbuffer width/height members.

```
private void EnsureFogSurface()
{
    if (_fogSurface != null &&
        _fogSurface.Canvas != null &&
        _fogSurface.Width == Width &&
        _fogSurface.Height == Height)
        return;

    _fogSurface?.Dispose();

    var info = new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);
    _fogSurface = SKSurface.Create(info);
    _fogSurface!.Canvas.Clear(SKColors.Transparent);
}
```

**3) Modify DrawDrawables to composite fog surface after PostDrawTiles**

```
internal void DrawDrawables(View view, IEnumerable drawables, Rectangle clipRect)
{
    EnsureFogSurface();

    Canvas.Save();
    Canvas.ClipRect(clipRect.ToSKRect());

    var tiles = new List();

    foreach (var drawable in drawables)
    {
        if (!drawable.Visible)
            continue;

        var destRectScreen = drawable.GetDrawLocationScreen(view);
        drawable.Draw(this, destRectScreen);

        AddToBackbufferDirtyRectangle(destRectScreen.ToPixelAlignedRect());

        if (drawable is Tile tile)
            tiles.Add(tile);
    }

    PostDrawTiles(view, tiles, clipRect);

    Canvas.Restore();
}
```

**4) Update PostDrawTiles to write fog into _fogSurface, not Canvas**
Change signature to accept clipRect:
```
private void PostDrawTiles(View view, List tiles, Rectangle clipRect)
{
    var fogCanvas = _fogSurface!.Canvas;

    // Clip fog work to the same rect to avoid touching unrelated pixels
    fogCanvas.Save();
    fogCanvas.ClipRect(clipRect.ToSKRect());

    foreach (var tile in tiles)
    {
        var worldPts = tile.OutlinePointsWorld;
        var ptsScreen = new SKPoint[worldPts.Length];

        for (int i = 0; i < worldPts.Length; i++)
        {
            var p = worldPts[i];
            var sp = view.WorldPxToScreenPx(tile.SceneLayer, new PointF(p.X, p.Y));
            ptsScreen[i] = new SKPoint(sp.X, sp.Y);
        }

        static SKPoint[] Enclose(SKPoint[] pts)
        {
            if (pts.Length == 0) return pts;
            var arr = new SKPoint[pts.Length + 1];
            Array.Copy(pts, arr, pts.Length);
            arr[^1] = pts[0];
            return arr;
        }

        if (tile.EnableFog)
        {
            using var path = new SKPath();
            path.AddPoly(ptsScreen, close: true);

            // IMPORTANT: clear fog *mask* region (not the scene) so fog is idempotent
            fogCanvas.DrawPath(path, FogMaskClearPaint);

            // redraw translucent fog into fog surface
            fogCanvas.DrawPath(path, FogPaint);
        }

        // Keep grid/collision on the main canvas (or move them too if desired)
        if (tile.SceneLayer.ShowGridLines && tile.Visible && tile.IsPositionFixed)
            Canvas.DrawPoints(SKPointMode.Polygon, Enclose(ptsScreen), GridLinePaint);

        if (tile.SceneLayer.ShowCollisionBoxes && tile.Visible)
            Canvas.DrawPoints(SKPointMode.Polygon, Enclose(ptsScreen), CollisionBoxPaint);
    }

    fogCanvas.Restore();

    // Composite fog surface onto the scene (preserves translucency)
    Canvas.DrawSurface(_fogSurface, 0, 0);
}
```
________________________________________
Why this fixes idempotence (with translucency)
•	The fog overlay is rebuilt from a clean baseline on the fog surface, not on the scene pixels.
•	Your scene drawing can remain partial/dirty; fog no longer “double-applies” onto itself on the main backbuffer.
________________________________________
Two practical notes
1.	If fog should also apply to sprites, you’ll need to add fog shapes for sprites the same way you do tiles. (The mechanism remains identical.)
2.	On resize/dispose, dispose _fogSurface alongside other surfaces.

</details>

## Content Pipeline & Assets (4)

<details>
<summary><strong>#111 feat: Aseprite (.aseprite) importer for sprites and animations</strong></summary>

- **Issue:** [#111](https://github.com/Isthimius/Gondwana/issues/111)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:40Z
- **Updated:** 2026-05-03T22:24:40Z

### Ticket Details

## Summary
FlatRedBall has native drag-and-drop `.aseprite` support. Gondwana has no importer for this format. Aseprite is the most popular pixel-art editor in 2D game development. This issue tracks adding a `Gondwana.Assets.Aseprite` package.

## Scope of Work

### New NuGet Package: `Gondwana.Assets.Aseprite`
- Parse the `.aseprite` binary format (spec link below)
- Extract individual frames as composited bitmaps (respect layer blend modes and visibility)
- Extract named tags → `FrameSequence` objects
- Output a `TilesheetMemory` (in-memory tilesheet) + `IDictionary` keyed by tag name

### Public API
```csharp
var imported = AsepriteImporter.Load("hero.aseprite");
Tilesheet tilesheet = imported.Tilesheet;
FrameSequence walkRight = imported.Animations["walk_right"];
FrameSequence idle = imported.Animations["idle"];

// Optional: save to disk
imported.ExportTilesheet("hero.gondwana-tilesheet");
imported.ExportAnimations("hero/");
```

### Notes
- The package must be standalone (no Gondwana.Tooling.Studio dependency)
- Layer compositing uses Aseprite's blend modes (Normal, Multiply, Screen, etc.)
- Only RGB and RGBA colour modes need to be supported in v1 (Indexed mode is optional)

## Acceptance Criteria
- [ ] Loads an `.aseprite` file with multiple named tags and at least 2 layers
- [ ] Produced tilesheet frames render correctly in the engine (no visual artifacts)
- [ ] Tag-to-`FrameSequence` mapping matches Aseprite tag frame boundaries exactly
- [ ] Works with both flat (single layer) and multi-layer Aseprite files
- [ ] Package has no transitive dependencies beyond Gondwana core + SkiaSharp

## Key Files / References
- Aseprite file spec: https://github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md
- `Gondwana/Drawing/Tilesheets/Tilesheet.cs`
- `Gondwana/Drawing/Animation/FrameSequence.cs`
- Existing asset package for reference: `Tooling/Gondwana.Tooling.Assets.WinForms/`

</details>

<details>
<summary><strong>#104 feat: Tilemap / .tmx support (Tiled integration)</strong></summary>

- **Issue:** [#104](https://github.com/Isthimius/Gondwana/issues/104)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:31Z
- **Updated:** 2026-05-03T22:24:31Z

### Ticket Details

## Summary
Gondwana currently has no way to load levels authored in [Tiled](https://www.mapeditor.org/) (.tmx files). Both FlatRedBall and GameMaker offer native Tiled/room-editor integration, giving developers a visual level-design workflow. This issue tracks adding a first-class `.tmx` import pipeline.

## Background
Gondwana already has `SceneLayer`, `Tile`, and `TilesheetRegistry` primitives. The missing piece is a parser that maps Tiled's XML layer structure onto these abstractions.

## Scope of Work
- Add a `TmxImporter` class (or static method) in `Gondwana.Drawing` / `Gondwana.Scenes` that:
  - Parses `.tmx` XML (tile layers → `SceneLayer`, tile instances → world-space `Tile` placements)
  - Maps Tiled object layers to engine entity-spawn lists (returns `IEnumerable` so game code can instantiate `Sprite` objects)
  - Maps Tiled collision layers / object shapes to `Aabb` collision data registered with `ColliderRegistry`
  - Handles external tilesheet references (`.tsx` files) through the existing `TilesheetRegistry`
- Expose a `TmxMapResource` asset type loadable from the extensible resource pipeline
- Add a demo or unit test that loads a sample `.tmx` and verifies tile count / object placement

## Acceptance Criteria
- [ ] A `.tmx` file (Tiled 1.x format) loads into a `Scene` without manual configuration
- [ ] Tile layers render correctly with the existing dirty-region renderer
- [ ] Object-layer spawns return parseable descriptor data
- [ ] Collision data from object / tile-collision layers is accessible as `Aabb` instances
- [ ] Tested with at least one sample Tiled map committed to `Demos/`

## Key Files / References
- `Gondwana/Scenes/SceneLayer.cs`
- `Gondwana/Drawing/Tile.cs`
- `Gondwana/Drawing/Tilesheets/TilesheetRegistry.cs`
- FlatRedBall Tiled docs: https://docs.flatredball.com/tiled
- Tiled XML spec: https://doc.mapeditor.org/en/stable/reference/tmx-map-format/

</details>

<details>
<summary><strong>#28 wrapping, tiles and SceneLayers</strong></summary>

- **Issue:** [#28](https://github.com/Isthimius/Gondwana/issues/28)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-02-04T01:07:37Z
- **Updated:** 2026-02-04T16:46:54Z

### Ticket Details

- test wrapping tiles (i.e., tile goes off the right and appears on the left)
- implement SceneLayer wrapping (world map, toroidal map)

</details>

<details>
<summary><strong>#23 animation graph enhancement</strong></summary>

- **Issue:** [#23](https://github.com/Isthimius/Gondwana/issues/23)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-02-03T20:55:29Z
- **Updated:** 2026-03-28T20:17:16Z

### Ticket Details

[more robust Animator / animation graphs](https://chatgpt.com/c/69330d23-c550-832a-894b-93d1d462082d)

?? include Sprite.Resize as part of Animation ??
?? Sprite rotation as part of Animation ??

</details>

## Networking & Multiplayer (2)

<details>
<summary><strong>#119 feat: Gondwana.Networking — client/server message loop and lobby primitives</strong></summary>

- **Issue:** [#119](https://github.com/Isthimius/Gondwana/issues/119)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:48Z
- **Updated:** 2026-05-03T22:24:48Z

### Ticket Details

## Summary
This is listed in the README roadmap as _"Initial client/server networking support."_ Both FlatRedBall and GameMaker provide multiplayer/network primitives. This issue tracks creating a `Gondwana.Networking` package as the starting point for networked games.

## Non-Goals (v1)
- No relay/matchmaking server infrastructure
- No authoritative server physics (client-side prediction is the game's responsibility)
- No cloud saves or leaderboards

## Scope of Work

### Core Transport
```csharp
public interface IGameTransport
{
    Task ConnectAsync(string host, int port, CancellationToken ct);
    Task SendAsync(GameMessage msg, CancellationToken ct);
    IAsyncEnumerable ReceiveAsync(CancellationToken ct);
    void Disconnect();
}
```
Implementations: `TcpGameTransport` (reliable, ordered), `UdpGameTransport` (unreliable, unordered).

### Messaging
- `GameMessage` — typed byte-array envelope with a `ushort MessageType` header
- `MessageRouter` — dispatches incoming messages to handlers registered by type ID

### Lobby / Room
- `LobbyHost` — creates a TCP listener, manages peer connections, broadcasts events
- `LobbyClient` — connects to a host, sends/receives lobby events
- `GameLobby` — tracks connected peers and their metadata (name, ready state)
- Events: `PlayerJoined`, `PlayerLeft`, `LobbyReady`

### Engine Integration
Received messages arrive on a background thread; they must be queued and dispatched on the engine cycle thread:
```csharp
// In network receive callback:
engine.Dispatcher.InvokeOnCycle(() => messageRouter.Dispatch(msg));
```

## Acceptance Criteria
- [ ] Two instances of a game can exchange `GameMessage`s over localhost TCP
- [ ] `LobbyHost` broadcasts `PlayerJoined` to all existing clients when a new one connects
- [ ] Messages enqueued from the receive thread are dispatched safely within the engine cycle
- [ ] A minimal two-player demo (e.g., synchronized sprite positions) works over localhost

## Key Files / References
- README roadmap entry: _"Initial client/server networking support"_
- `Gondwana/EngineDispatcher.cs`
- `Gondwana/IEngineDispatcher.cs`

</details>

<details>
<summary><strong>#41 client / server networking poc</strong></summary>

- **Issue:** [#41](https://github.com/Isthimius/Gondwana/issues/41)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-04-15T15:57:49Z
- **Updated:** 2026-04-15T15:57:49Z

### Ticket Details

using LiteNetLib or RiptideNetworking

Gondwana.Networking
├── INetworkPeer
├── INetworkMessage
├── NetworkManager
├── NetworkClient
├── NetworkServer
├── MessageRegistry
├── NetSerializer
├── Transport
│   └── LiteNetLibTransport
└── Messages
    ├── ConnectRequest
    ├── ConnectAccepted
    ├── ChatMessage
    ├── InputMessage
    ├── StateSnapshotMessage
    └── DisconnectNotice

https://chatgpt.com/c/69d94dc9-3318-832e-9472-028b0cf3605b

</details>

## General Backlog (2)

<details>
<summary><strong>#120 feat: Live asset hot-reload via FileSystemWatcher in Gondwana.Hosting</strong></summary>

- **Issue:** [#120](https://github.com/Isthimius/Gondwana/issues/120)
- **State:** OPEN
- **Author:** @github-actions
- **Created:** 2026-05-03T22:24:49Z
- **Updated:** 2026-05-03T22:24:49Z

### Ticket Details

## Summary
FlatRedBall's "Live Edit" lets developers change assets while the game is running and see changes instantly. This is a significant iteration-speed improvement. This issue tracks adding a `HotReloadWatcher` to `Gondwana.Hosting` that automatically re-imports changed asset files.

## Scope of Work

### `Gondwana.Hosting.HotReloadWatcher`
```csharp
public class HotReloadWatcher : IDisposable
{
    public HotReloadWatcher(IEngineDispatcher dispatcher, string assetRoot);

    // Register a reload handler for a file extension
    public void Register(string extension, Action reloadAction);

    public void Start();
    public void Stop();
}
```

- Uses `System.IO.FileSystemWatcher` internally
- Debounces file-change events (default 100 ms) to avoid partial-write races
- Dispatches reload actions via `IEngineDispatcher.InvokeOnCycle` so all reloads happen on the engine cycle thread (thread-safe)
- Logs each reload to the engine's diagnostic output

### Built-in Handlers
| Extension | Handler |
|---|---|
| `.gondwana-tilesheet` | `TilesheetRegistry.Reload(path)` — reloads image + metadata |
| `.wav`, `.ogg`, `.mp3` | `AudioResourceManager.Reload(path)` |
| `.gondwana-animation` | Animation cache invalidation |

### `GameHostBase` Integration
```csharp
// In game host setup:
host.EnableHotReload(assetRoot: "Assets/");
```
`EnableHotReload` is a no-op on platforms that don't support `FileSystemWatcher` (WASM).

## Acceptance Criteria
- [ ] Overwriting a tilesheet PNG on disk causes the engine to re-render using the new image within 500 ms
- [ ] No crash or visual artefact during the reload transition
- [ ] Hot-reload is a no-op (silent) on WASM / platforms without filesystem watch support
- [ ] No measurable frame-rate impact when no files are being changed

## Key Files / References
- `Gondwana.Hosting/GameHostBase.cs`
- `Gondwana/Drawing/Tilesheets/TilesheetRegistry.cs`
- `Gondwana/Assets/Audio/AudioResourceManager.cs`
- `Gondwana/EngineDispatcher.cs`
- `Gondwana/IEngineDispatcher.cs`

</details>

<details>
<summary><strong>#29 View-based "full" redraw</strong></summary>

- **Issue:** [#29](https://github.com/Isthimius/Gondwana/issues/29)
- **State:** OPEN
- **Author:** @Isthimius
- **Created:** 2026-02-04T01:08:49Z
- **Updated:** 2026-02-04T16:46:55Z

### Ticket Details

- could / should: forceFullRedraw -> should it be based on View now? Camera move only force View to full redraw?

</details>
