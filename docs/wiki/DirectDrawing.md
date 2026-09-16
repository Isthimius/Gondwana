`DirectDrawing` is Gondwana’s system for engine-managed visuals that are not tiles in a layer grid.

This includes things like:

- images
- rectangles and geometric overlays
- text
- SVG graphics
- particles
- ambient image effects
- debug visuals
- HUD and screen-space overlays
- world-space decorative elements

Direct drawings are **retained objects**, not one-off drawing commands. Once created, they participate in the engine’s update and rendering lifecycle until they are disposed.

---

## Two important modes

Every direct drawing belongs to one of two coordinate modes:

- `DirectDrawingMode.SceneLayer`
- `DirectDrawingMode.View`

### SceneLayer mode

A drawing associated with a `SceneLayer` exists in **world space**.

Its bounds are expressed in world pixels, and rendering is affected by:

- camera position
- zoom
- viewport projection
- the owning layer's parallax

This is appropriate for things that should appear to exist in the game world:

- world labels
- selection rectangles
- decorative images
- particle effects
- debugging markers

For example:

```csharp
var marker = new DirectRectangle(
    Color.Red,
    renderSurfaceHost,
    sceneLayer,
    new Rectangle(500, 300, 64, 64),
    "target-marker");
```

The rectangle is positioned at `(500, 300)` **in world pixels**.

### View mode

A drawing associated with a `View` exists in **screen space**.

Its bounds are expressed in screen pixels and are not affected by camera movement.

This is useful for:

- HUD elements
- status displays
- screen-space text
- diagnostic overlays
- viewport decorations

```csharp
var panel = new DirectRectangle(
    Color.Black,
    renderSurfaceHost,
    view,
    new Rectangle(20, 20, 240, 80),
    "status-panel");
```

That panel remains at the same screen location even while the camera moves through the world.

---

## Core type hierarchy

The DirectDrawing system is built around a small set of interfaces and base classes.

```text
IDirectDrawable
    |
    +-- DirectDrawingBase
    |       |
    |       +-- DirectDrawingMovableBase
    |               |
    |               +-- DirectRectangle
    |               +-- DirectImage
    |               +-- DirectSvg
    |               +-- TextBlock
    |               +-- ImageInstanceLayer
    |               +-- ParticleSurface
    |
    +-- IDirectCompositeChild
            |
            +-- DirectDrawingMovableBase
            +-- DirectComposite
```

`DirectComposite` is slightly different from the normal drawing classes: it participates in the DirectDrawing lifecycle but does not itself render pixels. Instead, it manages other direct drawings as a group.

---

## IDirectDrawable

`IDirectDrawable` is the basic contract for anything managed by the DirectDrawing system.

Among other things, it exposes:

- the owning `RenderSurfaceHostBase`
- the `DirectDrawingMode`
- `ScreenBounds`
- `WorldBounds`
- visibility and Z-order through `IDrawable`
- per-frame `Update()`
- disposal/lifetime notification

Most applications will not implement `IDirectDrawable` directly. The provided base classes handle the engine integration for you.

---

## DirectDrawingBase

`DirectDrawingBase` provides the shared rendering behavior for concrete direct drawings.

It handles:

- automatic registration with `DirectDrawingManager`
- visibility
- opacity
- fade transitions
- reveal animations
- Z-order
- world-space vs screen-space positioning
- projection of world bounds through a `View`
- dirty-region invalidation
- disposal and manager cleanup

Concrete classes are mainly responsible for implementing the actual Skia drawing operation through `OnDraw()`.

Conceptually, a custom direct drawing can be as simple as:

```csharp
public sealed class CrosshairDrawing : DirectDrawingBase
{
    public CrosshairDrawing(
        RenderSurfaceHostBase host,
        View view,
        Rectangle bounds)
        : base(
            host,
            DirectDrawingMode.View,
            sceneLayer: null,
            view,
            screenBounds: bounds,
            worldBounds: null,
            nickname: "crosshair")
    {
    }

    protected override void OnDraw(
        BackbufferBase backbuffer,
        RectangleF bounds)
    {
        using var paint = new SKPaint
        {
            Color = SKColors.Red,
            StrokeWidth = 2
        };

        float cx = bounds.Left + bounds.Width / 2f;
        float cy = bounds.Top + bounds.Height / 2f;

        backbuffer.Canvas.DrawLine(
            bounds.Left, cy,
            bounds.Right, cy,
            paint);

        backbuffer.Canvas.DrawLine(
            cx, bounds.Top,
            cx, bounds.Bottom,
            paint);
    }
}
```

The base class still handles where and when it is rendered.

---

## DirectDrawingMovableBase

`DirectDrawingMovableBase` extends `DirectDrawingBase` with the standard Gondwana movement system.

It implements `IDirectCompositeChild` and owns a `MovementController`.

Direct drawings always move in pixel space:

- **world pixels** for `SceneLayer` drawings
- **screen pixels** for `View` drawings

This means normal movement features can be applied to a direct drawing without the drawing implementation having to manage its own position integration.

```csharp
marker.Movement.MoveTo(
    new Vector2(800, 450),
    durationSec: 1.0f);
```

Movement updates the drawing's position, which in turn invalidates the appropriate drawing regions.

---

## Built-in direct drawings

Gondwana includes several concrete DirectDrawing types.

### `DirectRectangle`

A general-purpose rectangle primitive supporting features such as:

- fill and outline
- independent border color
- stroke width
- rounded corners
- dashed borders
- pattern fills
- blend modes
- color pulsing

Useful for panels, selection boxes, debug bounds, indicators, backgrounds, and other geometric visuals.

### `DirectImage`

Draws an `SKImage` or `SKBitmap`.

It supports:

- stretching, fitting and filling
- centered and pixel-perfect scaling
- source rectangles
- tinting
- rotation
- configurable anchors
- blend modes
- filtering

It is useful when you need an image as an independently managed drawable rather than as part of a tilesheet/layer structure.

### `DirectSvg`

Draws an `SvgResource`.

SVG content is rasterized lazily and cached according to the destination size, allowing vector assets to participate in the same DirectDrawing system as bitmap images.

### `TextBlock`

A retained text drawing with considerably more functionality than simply calling `DrawText`.

It supports:

- multiline text
- wrapping
- horizontal and vertical alignment
- padding
- text backgrounds
- shadow and outline effects
- automatic font shrinking
- text reveal/typewriter effects
- text color pulsing

### `ImageInstanceLayer`

`ImageInstanceLayer` manages a collection of lightweight `ImageInstance` objects inside one direct drawing.

Each instance can maintain its own:

- bounds
- velocity
- rotation
- tint

This is intended for effects involving a relatively small number of persistent visual objects, such as:

- drifting clouds
- fog patches
- leaves
- embers
- background debris

It sits conceptually between a single `DirectImage` and a full particle system.

### `ParticleSurface`

`ParticleSurface` is Gondwana's built-in DirectDrawing particle system.

It maintains a pool of `Particle` instances and one or more `ParticleEmitter` configurations.

Emitters control values such as:

- emission rate
- spawn position and distribution
- lifetime
- velocity
- size
- color and tint
- gravity
- blend mode
- optional particle images

Multiple emitters can be combined in one `ParticleSurface`, allowing effects such as smoke and sparks to share the same drawing surface.

---

## DirectComposite

`DirectComposite` groups multiple movable direct drawings around a shared anchor.

For example, suppose a status display consists of:

- a background rectangle
- an icon
- a text label

Instead of moving and fading each object separately, they can be placed in a composite.

```csharp
var group = new DirectComposite(
    renderSurfaceHost,
    DirectDrawingMode.View,
    new PointF(100, 40),
    "status-display");

group.Add(
    panel,
    keepCurrentOffset: false,
    explicitLocalOffsetPx: Vector2.Zero);

group.Add(
    label,
    keepCurrentOffset: false,
    explicitLocalOffsetPx: new Vector2(12, 8));
```

Moving the composite moves its children while preserving their local offsets:

```csharp
group.SetPosition(300, 40);
```

Operations can also be applied recursively:

```csharp
group.SetOpacity(0.75f);
group.FadeOut(0.25f);
group.SetIsVisible(false);
group.SetZOrder(10_000);
```

Composites can contain other composites, making nested groups possible.

There are several deliberate constraints:

- all children must belong to the same `RenderSurfaceHostBase`
- all children must use the same `DirectDrawingMode`
- scene-layer children must belong to the same `SceneLayer`
- view children must belong to the same `View`
- a child can belong to only one composite
- cyclic composite relationships are rejected

These rules keep coordinate ownership unambiguous.

---

## Composite interfaces

Two small interfaces support composition without requiring `DirectComposite` to know about every concrete drawing class.

### `IDirectCompositeChild`

An `IDirectCompositeChild` is a drawable that can participate in a composite.

In addition to `IDirectDrawable`, it is an `IMovable` and exposes the operations a parent needs to manage it:

- position
- visibility
- Z-order
- opacity
- fading
- `SceneLayer` or `View` association

`DirectDrawingMovableBase` implements this interface, which is why the normal built-in drawing primitives can be added directly to a `DirectComposite`.

### `IDirectCompositeContainer`

`IDirectCompositeContainer` represents something that owns composite children.

Its basic contract is deliberately small:

```csharp
Children
Add(...)
Remove(...)
Clear()
```

`DirectComposite` implements both `IDirectCompositeChild` and `IDirectCompositeContainer`.

That is what allows composites to be nested recursively.

---

## DirectDrawingManager

All direct drawings are centrally tracked by `DirectDrawingManager`.

The manager:

- automatically receives newly created direct drawings
- removes them when they are disposed
- updates registered drawings each engine update
- filters drawings by `SceneLayer`
- filters drawings by `View`
- provides lookup by nickname
- produces deterministic render ordering

For rendering, drawings are ordered by:

1. `ZOrder`
2. `Nickname`

Higher Z-order values therefore render later and appear above lower values.

Normally, application code does not need to manually register a direct drawing. Construction and disposal take care of manager membership automatically.

```csharp
using var marker = new DirectRectangle(
    Color.Yellow,
    renderSurfaceHost,
    sceneLayer,
    new Rectangle(100, 100, 32, 32),
    "spawn-point");
```

Creating `marker` registers it.

Disposing it removes it.

---

## Why DirectDrawing exists

Tiles and sprites cover many game-engine rendering needs, but not all visual content naturally belongs in a tile grid or sprite abstraction.

DirectDrawing provides an engine-native escape hatch without bypassing the engine itself.

A direct drawing still gets:

- lifecycle management
- update participation
- coordinate-space handling
- camera projection
- dirty-region integration
- Z-ordering
- movement
- opacity and animation
- composition

The drawing implementation gets freedom over **what** is rendered while Gondwana continues to manage **where, when, and in what order** it is rendered.

---

## Mental model

A useful way to think about the major drawing systems is:

- **tiles** — structured scene cells
- **sprites** — engine-managed world actors
- **direct drawings** — engine-managed custom visuals
- **direct composites** — groups of custom visuals sharing position and behavior

---

## Where to read next

Core infrastructure:

- `Gondwana/Drawing/Direct/IDirectDrawable.cs`
- `Gondwana/Drawing/Direct/IDirectCompositeChild.cs`
- `Gondwana/Drawing/Direct/IDirectCompositeContainer.cs`
- `Gondwana/Drawing/Direct/DirectDrawingBase.cs`
- `Gondwana/Drawing/Direct/DirectDrawingMovableBase.cs`
- `Gondwana/Drawing/Direct/DirectDrawingManager.cs`
- `Gondwana/Drawing/Direct/DirectComposite.cs`

Built-in drawings:

- `Gondwana/Drawing/Direct/DirectRectangle.cs`
- `Gondwana/Drawing/Direct/DirectImage.cs`
- `Gondwana/Drawing/Direct/DirectSvg.cs`
- `Gondwana/Drawing/Direct/TextBlock.cs`
- `Gondwana/Drawing/Direct/ImageLayer/*`
- `Gondwana/Drawing/Direct/Particles/*`

---

## Periodic layer content

SceneLayer-bound drawings repeat with their layer's world-space period vectors, including copies several periods away. View-bound drawings remain fixed. See [[SceneLayer Wrapping]] for instance selection and refresh behavior.
