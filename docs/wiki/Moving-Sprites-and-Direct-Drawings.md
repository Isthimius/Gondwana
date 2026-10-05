> **Movement and Controllers**
>
> This page applies Gondwana's movement system to its two most common movable visual types: `Sprite` and movable direct drawings. For the movement system itself—follow, scripted movement, integrated movement, priority, cancellation, events, and controller APIs—see [[Movement and Controllers]].

---

## Table of Contents

- [What This Page Covers](#what-this-page-covers)
- [Sprite vs. Direct-Drawing Movement](#sprite-vs-direct-drawing-movement)
- [How Movement Is Attached](#how-movement-is-attached)
- [Moving Sprites](#moving-sprites)
- [Moving Direct Drawings](#moving-direct-drawings)
- [Following Across Movement Spaces](#following-across-movement-spaces)
- [Movement and Rendering](#movement-and-rendering)
- [Movement and Collision](#movement-and-collision)
- [Update Timing](#update-timing)
- [Practical Examples](#practical-examples)
- [Common Mistakes](#common-mistakes)
- [Troubleshooting](#troubleshooting)
- [Quick Reference](#quick-reference)
- [Related Documentation](#related-documentation)
- [Related Source Files](#related-source-files)

---

## What This Page Covers

`MovementController` works through the `IMovable` contract, so the same high-level movement APIs can drive objects with different coordinate systems.

The important part is understanding what position means for each object.

This page focuses on:

- `Sprite`;
- classes derived from `DirectDrawingMovableBase`, including movable images, rectangles, text, SVG drawings, particle surfaces, and related direct-drawing types.

It explains:

- what coordinate space each type uses;
- what a reported position represents;
- how movement affects rendering;
- how cameras affect each type;
- how one type can follow the other;
- what collision behavior movement does and does not imply.

It does **not** duplicate the complete `MovementController` API. See [[Movement and Controllers]] for:

- follow, scripted, and integrated movement;
- movement priority and ownership;
- cancellation rules;
- velocity, acceleration, damping, and max speed;
- status properties and events;
- world wrapping;
- general movement troubleshooting.

---

## Sprite vs. Direct-Drawing Movement

| Behavior | `Sprite` | `DirectDrawingMovableBase` |
|---|---|---|
| Position space | `MovementSpace.Grid` | `MovementSpace.Pixel` |
| Stored position | Scene-layer coordinates | Bounds top-left |
| Movement units | Grid units | Pixels |
| Velocity units | Grid units/sec | Pixels/sec |
| Position may be fractional | Yes | Yes internally |
| Belongs to a `SceneLayer` | Always | Only in `SceneLayer` mode |
| Can be view-bound | No | Yes |
| Camera affects it | Yes | Scene-layer mode: yes; view mode: no |
| Rendering space | Grid converted to world pixels | World or screen pixels depending on mode |
| Automatic movement updates | `SpriteManager` | Direct-drawing update cycle |
| Built-in sprite collision participation | Yes, when configured | No; movement alone does not imply collision |

The most important distinction is:

```text
Sprite position
    = scene-layer grid coordinates

Direct-drawing position
    = pixel coordinates
```

A sprite at:

```text
(5, 3)
```

is normally at scene-layer coordinate `(5, 3)`.

A direct drawing at:

```text
(5, 3)
```

is positioned five pixels from its applicable pixel-space origin.

The APIs may look identical:

```csharp
sprite.Movement.MoveTo(...);

drawing.Movement.MoveTo(...);
```

The units are not identical.

---

## How Movement Is Attached

You normally do not create a `MovementController` yourself.

Gondwana creates and exposes one through each movable object.

### Sprite

```csharp
Sprite sprite =
    SpriteManager.Instance.CreateSprite(
        sceneLayer,
        frame,
        "player");

MovementController movement =
    sprite.Movement;
```

A sprite is a grid-space mover:

```csharp
sprite.PositionSpace
// MovementSpace.Grid
```

Its owning `SceneLayer` gives the engine the coordinate-system context needed to convert logical grid positions into world-pixel drawing positions.

### Movable direct drawing

Concrete movable direct drawings inherit movement behavior from `DirectDrawingMovableBase`.

For example:

```csharp
var marker =
    new DirectRectangle(
        Color.Gold,
        renderSurfaceHost,
        sceneLayer,
        new Rectangle(300, 200, 64, 64),
        "objective-marker");

MovementController movement =
    marker.Movement;
```

A movable direct drawing is a pixel-space mover:

```csharp
marker.PositionSpace
// MovementSpace.Pixel
```

The direct drawing's mode determines what those pixels mean:

- `DirectDrawingMode.SceneLayer` → world pixels;
- `DirectDrawingMode.View` → absolute screen/backbuffer pixels.

### No manual movement update call

For both object families, game code normally configures movement:

```csharp
sprite.Movement.SetVelocity(...);

marker.Movement.MoveTo(...);
```

The engine advances movement as part of its normal update cycle.

Do not manually call internal movement-advance methods.

---

## Moving Sprites

A `Sprite` is:

- attached to one `SceneLayer`;
- stored in scene-layer coordinates;
- rendered after the layer converts those coordinates to world pixels;
- updated automatically by `SpriteManager`.

### Sprite position is grid position

```csharp
sprite.SetPosition(
    new Vector2(8, 4));
```

The position `(8, 4)` is in the layer's scene-coordinate space.

For an orthogonal map, that usually corresponds to column 8, row 4.

For other coordinate systems, the owning layer determines how that logical position maps to world pixels.

### Fractional grid movement

Sprite coordinates may be fractional:

```csharp
sprite.SetPosition(
    new Vector2(8.25f, 4f));
```

or may become fractional through movement:

```csharp
sprite.Movement.SetVelocity(
    new Vector2(1.5f, 0f));
```

That allows smooth motion through positions such as:

```text
(8.00, 4.00)
(8.10, 4.00)
(8.20, 4.00)
...
```

while retaining a grid-aware logical position.

### Rendered position is derived from logical position

A sprite's movement position is not necessarily the top-left pixel of its artwork.

The final world drawing rectangle also accounts for:

- the layer's coordinate system;
- tile width and height;
- `HorizAlign`;
- `VertAlign`;
- `NudgeX`;
- `NudgeY`;
- `RenderSize`.

Conceptually:

```text
scene-layer coordinate
        ↓
coordinate-system world anchor
        ↓
alignment
        ↓
nudge
        ↓
rendered world rectangle
```

This allows a sprite to remain logically at grid coordinate `(8, 4)` while its artwork is:

- centered in the cell;
- bottom-aligned;
- larger than the tile;
- nudged upward;
- rendered at a custom size.

### Alignment is not movement

These change visual placement:

```csharp
sprite.VertAlign =
    VerticalAlignment.Bottom;

sprite.NudgeY = -8;
```

They do not change:

```csharp
sprite.GetPosition()
```

Likewise, movement does not erase alignment or nudge settings.

Use movement for **where the sprite exists logically**.

Use alignment, nudge, and render size for **how the artwork is placed around that logical location**.

### Sprite movement raises `SpriteMoved`

When `SetPosition` changes the sprite's coordinate, the sprite raises:

```csharp
sprite.SpriteMoved += args =>
{
    // Respond to logical sprite movement.
};
```

The sprite also refreshes the appropriate old/new drawing regions for rendering.

---

## Moving Direct Drawings

Movable direct drawings derive from:

```csharp
DirectDrawingMovableBase
```

Common examples include:

- `DirectImage`;
- `DirectSvg`;
- `DirectRectangle`;
- `TextBlock`;
- `ParticleSurface`;
- other movable direct-drawing types.

Their movement position is always pixel-based, but the meaning of those pixels depends on how the drawing is attached.

### Scene-layer direct drawing

A scene-layer direct drawing uses **world pixels**.

```csharp
var marker =
    new DirectRectangle(
        Color.Gold,
        renderSurfaceHost,
        sceneLayer,
        new Rectangle(
            x: 300,
            y: 200,
            width: 64,
            height: 64),
        "marker");
```

Its position is:

```text
(300, 200) world pixels
```

Moving it by 100 X units means 100 world pixels.

Because it belongs to a scene layer, camera movement and layer projection affect where it appears on screen.

### View-mode direct drawing

A view-bound direct drawing uses **absolute screen/backbuffer pixels**.

```csharp
var panel =
    new DirectRectangle(
        Color.Black,
        renderSurfaceHost,
        view,
        new Rectangle(
            x: 20,
            y: 20,
            width: 300,
            height: 80),
        "status-panel");
```

Camera movement does not affect it.

> [!IMPORTANT]
> View-mode bounds are absolute adapter/backbuffer coordinates. They are not automatically relative to the viewport's upper-left.

If a view begins at screen coordinate `(800, 0)`, a panel intended to appear 20 pixels inside that view should use the viewport origin:

```csharp
new Rectangle(
    view.Viewport.TargetRectPx.Left + 20,
    view.Viewport.TargetRectPx.Top + 20,
    300,
    80);
```

### Direct-drawing position is the bounds' upper-left

For a movable direct drawing:

```csharp
Vector2 position =
    drawing.GetPosition();
```

the movement position represents the precise upper-left coordinate of its bounds.

Movement changes that position while preserving width and height.

This differs from sprites, whose logical position is a scene-layer coordinate rather than a rendered rectangle's top-left pixel.

### Fractional movement, integer render bounds

`DirectDrawingMovableBase` stores movement position as a `Vector2`.

That internal value can remain fractional:

```text
100.25
100.50
100.75
```

When render bounds are updated, X and Y are rounded to integer pixels.

This provides:

- smooth movement calculations;
- stable pixel-aligned drawing bounds;
- predictable raster rendering.

At very low speeds, the visible drawing may remain on one pixel for several updates and then advance by one pixel. That is expected.

---

## Following Across Movement Spaces

The follow APIs are particularly useful when the target and follower use different position spaces.

The complete follow API is documented in [[Movement and Controllers]]. This section focuses only on the sprite/direct-drawing conversion cases.

### Pixel drawing follows a sprite

A common example is a name tag or marker following a sprite.

```csharp
nameTag.Movement.FollowTileSoft(
    tileTarget: player,
    speedTilesPerSec: 10f,
    snapTiles: 0.02f,
    gridOffset: new Vector2(0, -0.75f),
    pixelOffset: new Vector2(0, -6f));
```

The controller:

1. reads the sprite's grid position;
2. applies `gridOffset`;
3. converts the grid coordinate to world pixels using the target's scene layer;
4. applies `pixelOffset` for the pixel-space follower;
5. moves the direct drawing.

For rigid attachment:

```csharp
nameTag.Movement.FollowTileHard(
    tileTarget: player,
    gridOffset: new Vector2(0, -0.75f),
    pixelOffset: new Vector2(0, -6f));
```

### Why both offsets exist

| Offset | Units | Purpose |
|---|---|---|
| `gridOffset` | grid units | Move the logical attachment point before coordinate conversion |
| `pixelOffset` | pixels | Fine-tune the final visual placement after conversion |

For example, a name tag may logically sit three-quarters of a tile above a sprite, then need another six-pixel visual nudge.

### Pixel target followed by a grid mover

The reverse is also supported when the grid mover has the scene-layer coordinate context needed to convert a pixel target into grid space.

The same rule remains:

> **The follower ultimately moves in the follower's own `PositionSpace`.**

---

## Movement and Rendering

Movement changes logical position.

Each object type is responsible for translating that position into visible rendering state.

### Sprite rendering

When a sprite moves, it:

1. remembers its old world drawing rectangle;
2. updates its scene-layer coordinate;
3. recalculates its world drawing rectangle;
4. refreshes the old/new drawing region as required;
5. raises `SpriteMoved`.

For a sprite:

```csharp
sprite.GetPosition()
```

returns logical grid coordinates.

It does **not** return:

```csharp
sprite.DrawLocationWorld.Location
```

### Direct-drawing rendering

When a movable direct drawing changes position, it refreshes its old and new bounds.

For:

- scene-layer mode, those bounds are world-space bounds;
- view mode, those bounds are screen-space bounds.

For direct drawings, the movement position corresponds to the bounds' upper-left, but the complete rectangle also includes width and height.

### Camera behavior

| Mover | Camera movement changes screen position? |
|---|:---:|
| `Sprite` | Yes |
| Scene-layer direct drawing | Yes |
| View-mode direct drawing | No |

A view-mode direct drawing may still move through its own `MovementController`. It simply does not move because the world camera moved.

---

## Movement and Collision

Movement support does not imply identical collision support.

### Sprites

Sprites participate in Gondwana's sprite/tile collision architecture when collision is configured.

Collision response may cancel one velocity component while preserving another.

Conceptually:

```text
horizontal collision
    → cancel X velocity
    → preserve Y velocity
```

This supports behaviors such as:

- wall sliding;
- floor contact;
- ceiling contact;
- movement along an unblocked axis.

### Direct drawings

A direct drawing receives movement because it derives from `DirectDrawingMovableBase`.

That does **not** automatically make it part of sprite collision detection.

Direct drawings are well suited for:

- visuals;
- overlays;
- effects;
- indicators;
- custom engine-native drawing.

Use a sprite or an explicit collision component/system when collision semantics are required.

### Scripts and follow are not pathfinding

A sprite using `MoveTo` does not automatically navigate around walls.

A companion using follow movement does not automatically find a route around obstacles.

See [[Movement and Controllers]] and [[Collision Detection and Resolution]] for the separation between movement, collision response, and pathfinding.

---

## Update Timing

Both object families use elapsed-time movement, but they enter the movement system through different update paths.

### Sprites

`SpriteManager` advances registered sprite movement as part of the sprite update cycle.

A sprite's movement controller therefore receives elapsed time automatically.

### Direct drawings

`DirectDrawingMovableBase` advances movement from its normal direct-drawing `Update` path.

Again, elapsed time is supplied automatically.

### What this means

For normal game code:

- movement values are expressed per second;
- the engine advances movement;
- you do not multiply speeds by frame rate;
- you do not manually advance the controller.

The fact that sprites and direct drawings use different update owners does not change the public movement model.

---

## Practical Examples

### Move a sprite three tiles to the right

```csharp
sprite.Movement.Unfollow();

sprite.Movement.MoveBy(
    delta: new Vector2(3, 0),
    durationSec: 0.75f,
    easingKind: EasingKind.EaseInOutQuad);
```

The delta is three grid units.

### Move a world-space marker 200 pixels upward

```csharp
marker.Movement.Unfollow();

marker.Movement.MoveBy(
    delta: new Vector2(0, -200),
    durationSec: 0.5f,
    easingKind: EasingKind.EaseOutCubic);
```

The delta is 200 world pixels.

### Slide a HUD panel onto the screen

```csharp
var panel =
    new DirectRectangle(
        Color.Black,
        renderSurfaceHost,
        view,
        new Rectangle(
            x: -320,
            y: 20,
            width: 300,
            height: 80),
        "status-panel");

panel.Movement.MoveTo(
    target: new Vector2(20, 20),
    durationSec: 0.35f,
    easingKind: EasingKind.EaseOutCubic);
```

For a viewport whose target rectangle does not begin at screen `(0, 0)`, include its absolute screen origin in both starting and target coordinates.

### Attach a name tag to a sprite

```csharp
nameTag.Movement.FollowTileHard(
    tileTarget: player,
    gridOffset: new Vector2(0, -0.75f),
    pixelOffset: new Vector2(0, -6f));
```

### Accelerate a player sprite

```csharp
player.Movement.Unfollow();

player.Movement.SetAcceleration(
    input * 10f);

player.Movement.SetMaxSpeed(
    4f);

player.Movement.SetLinearDamping(
    6f);
```

All of those numeric motion values are in the sprite's grid-space scale.

### Move a pixel-space effect at constant speed

```csharp
effect.Movement.Unfollow();

effect.Movement.SetVelocity(
    direction * 500f);

effect.Movement.SetMaxSpeed(
    500f);

effect.Movement.SetLinearDamping(
    0f);
```

For a direct drawing, that speed is 500 pixels per second.

---

## Common Mistakes

### Treating sprite movement as pixels

This:

```csharp
sprite.Movement.SetVelocity(
    new Vector2(200, 0));
```

means 200 grid units per second, not 200 pixels per second.

Use values appropriate to the layer's grid scale.

### Treating a scene-layer direct drawing as grid-space

This:

```csharp
marker.Movement.MoveTo(
    new Vector2(10, 5),
    durationSec: 1f);
```

means world pixel position `(10, 5)` for a direct drawing.

It does not mean tile `(10, 5)`.

Convert the desired grid coordinate to world pixels or use tile-follow helpers for live grid targets.

### Treating view-mode coordinates as viewport-local

View-mode direct drawings use absolute screen/backbuffer coordinates.

If a viewport begins at screen X 800, screen X 20 is near the adapter's left edge—not 20 pixels inside that viewport.

### Forgetting anchor differences

A direct drawing moves by its upper-left bounds position.

A sprite moves by its logical scene-layer coordinate.

When attaching visuals, offsets must account for the desired visual anchor.

### Comparing raw movement vectors between sprites and drawings

A sprite velocity of:

```csharp
new Vector2(3, 0)
```

means three grid units per second.

The same direct-drawing velocity means three pixels per second.

Compare intended visual behavior after converting values to each object's native units.

### Treating alignment as sprite movement

`HorizAlign`, `VertAlign`, `NudgeX`, `NudgeY`, and `RenderSize` affect drawing placement.

They do not change the sprite's logical movement position.

---

## Troubleshooting

### A sprite moves far too quickly

The velocity or script speed may have been supplied as pixels per second.

Check:

```csharp
sprite.PositionSpace
// MovementSpace.Grid
```

Use a grid-scale speed.

### A direct drawing barely moves

The value may have been chosen as though it were tiles per second.

A pixel mover with velocity `(3, 0)` moves only three pixels per second.

### A sprite reaches the right logical coordinate but looks offset

Inspect:

```csharp
HorizAlign
VertAlign
NudgeX
NudgeY
RenderSize
```

Those affect rendering without changing the sprite's logical grid position.

### A direct drawing is in the wrong place inside a secondary viewport

View-mode positions are absolute screen/backbuffer coordinates.

Include:

```csharp
view.Viewport.TargetRectPx.Left
view.Viewport.TargetRectPx.Top
```

when you want viewport-relative visual placement.

### A name tag follows at the wrong offset

Separate:

```text
gridOffset
    → before grid-to-pixel conversion

pixelOffset
    → after conversion
```

Also remember that a sprite's logical grid coordinate may not correspond to the visual center or top of its artwork.

### A direct drawing jitters one pixel at low speed

Movement stores a fractional `Vector2` position, while render bounds are rounded to integer pixels.

At sufficiently low speed, the visible object may remain on a pixel for multiple updates before advancing.

### Movement behavior itself is not doing what you expect

If the problem involves:

- follow versus scripted priority;
- scripts not running;
- velocity having no effect;
- cancellation;
- `OnComplete`;
- damping;
- world wrapping;

see [[Movement and Controllers]]. Those are controller-level concerns rather than sprite/direct-drawing differences.

---

## Quick Reference

### Sprite

```csharp
sprite.PositionSpace
// MovementSpace.Grid

sprite.GetPosition()
// Scene-layer coordinate

sprite.SetPosition(
    new Vector2(column, row));
```

Movement values are grid-space values.

### Direct drawing

```csharp
drawing.PositionSpace
// MovementSpace.Pixel

drawing.GetPosition()
// Bounds upper-left

drawing.SetPosition(
    new Vector2(xPx, yPx));
```

Movement values are pixel-space values.

### Camera behavior

```text
Sprite
    → camera affects screen position

Scene-layer direct drawing
    → camera affects screen position

View-mode direct drawing
    → camera does not affect screen position
```

### Cross-space follow

```csharp
nameTag.Movement.FollowTileSoft(
    tileTarget: player,
    speedTilesPerSec: 8f,
    snapTiles: 0.05f,
    gridOffset: new Vector2(0, -0.5f),
    pixelOffset: new Vector2(0, -8f));
```

For the complete movement API, see [[Movement and Controllers]].

---

## Related Documentation

- [[Movement and Controllers]] — authoritative guide to `MovementController` behavior and APIs
- [[Sprites]] — sprite creation, rendering, animation, events, and lifetime
- [[DirectDrawing]] — direct-drawing types, modes, positioning, and composition
- [[Using Views and Cameras]] — camera projection and view behavior
- [[Coordinate Systems]] — how scene-layer coordinates map to world space
- [[Collision Detection and Resolution]] — collision behavior separate from movement
- [[Input Handling]] — driving movement from user input

---

## Related Source Files

- [`Gondwana/Drawing/Sprites/Sprite.cs`](https://isthimius.github.io/Gondwana/api/latest/Sprite_8cs_source.html)
- [`Gondwana/Drawing/Sprites/SpriteManager.cs`](https://isthimius.github.io/Gondwana/api/latest/SpriteManager_8cs_source.html)
- [`Gondwana/Drawing/Direct/DirectDrawingMovableBase.cs`](https://isthimius.github.io/Gondwana/api/latest/DirectDrawingMovableBase_8cs_source.html)
- [`Gondwana/Physics/Movement/MovementController.cs`](https://isthimius.github.io/Gondwana/api/latest/MovementController_8cs_source.html)
- [`Gondwana/Physics/Movement/MovementSpace.cs`](https://isthimius.github.io/Gondwana/api/latest/MovementSpace_8cs_source.html)
- [`Gondwana/Physics/Movement/IMovable.cs`](https://isthimius.github.io/Gondwana/api/latest/IMovable_8cs_source.html)
- [`Gondwana/Physics/Movement/IMovableOnSceneLayer.cs`](https://isthimius.github.io/Gondwana/api/latest/IMovableOnSceneLayer_8cs_source.html)

---

## Final Mental Model

Keep these object-specific rules in mind:

> **Sprites move in scene-layer grid coordinates.**

> **Movable direct drawings move in pixels.**

> **Scene-layer direct-drawing pixels are world pixels; view-mode pixels are absolute screen/backbuffer pixels.**

> **A sprite's logical position is not necessarily the top-left of its rendered artwork.**

> **A direct drawing's movement position is its bounds' upper-left.**

> **The same `MovementController` API can drive both because each object defines its own position space.**

For everything about movement-family behavior itself, continue with [[Movement and Controllers]].
