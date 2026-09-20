`ImageInstanceLayer` is Gondwana's DirectDrawing type for a relatively small number of persistent, independently managed bitmap instances.

It sits between two other common rendering models:

- a single custom `DirectDrawing`
- a high-volume `ParticleSurface`

Use it when the things being drawn are still lightweight visuals, but each one has enough identity and persistence that treating it as a disposable particle would be awkward.

Typical uses include:

- drifting clouds
- large fog patches
- floating leaves
- birds or distant silhouettes
- background debris
- moving decorative props
- embers or motes that must remain individually addressable
- persistent screen-space overlays made from several images

The basic relationship is:

```text
ImageInstanceLayer
    |
    +-- ImageInstance
    |       +-- bitmap
    |       +-- bounds
    |       +-- velocity
    |       +-- rotation
    |       +-- tint
    |       +-- tag
    |
    +-- ImageInstance
    |
    +-- ImageInstance
```

The `ImageInstanceLayer` owns update and rendering behavior.

Each `ImageInstance` owns the state of one visual element.

Unlike `ParticleSurface`, there is no emitter, no automatic lifetime, and no particle pool. Instances remain in the layer until game code removes, replaces, or recycles them.

---

## The important types

The system primarily revolves around:

- `ImageInstanceLayer`
- `ImageInstance`
- `DirectDrawingMovableBase`

`ImageInstanceLayer` is the actual engine-managed DirectDrawing.

`ImageInstance` is the lightweight object describing one bitmap instance inside it.

---

# ImageInstanceLayer

`ImageInstanceLayer` derives from `DirectDrawingMovableBase`, so the layer participates in the normal DirectDrawing systems:

- automatic registration
- visibility
- Z-order
- opacity
- fading
- reveal behavior
- SceneLayer or View rendering
- dirty-region handling
- movement-controller infrastructure
- disposal

What makes it different is that the DirectDrawing contains a collection of separate images:

```csharp
public List<ImageInstance> Instances { get; }
```

Those instances are updated and rendered together by one `ImageInstanceLayer`.

There is no explicit registration step. Constructing the layer registers it with `DirectDrawingManager` through the normal DirectDrawing base behavior.

---

# When to use ImageInstanceLayer

A good rule is:

```text
one custom visual
    -> DirectDrawing

few persistent image objects
    -> ImageInstanceLayer

many transient effect objects
    -> ParticleSurface
```

`ImageInstanceLayer` is especially useful when the individual images matter after creation.

For example, if a cloud is still meaningful as "that cloud" several minutes later, an `ImageInstance` is usually a better fit than a particle.

If the object needs full game-object behavior such as collision, animation cycles, game logic, or sprite-specific systems, it may instead belong in the Sprite system.

---

# World-space vs screen-space image layers

`ImageInstanceLayer` supports both DirectDrawing coordinate modes.

The coordinate mode determines how `ImageInstance.Bounds` is interpreted.

```text
SceneLayer mode
    ImageInstance.Bounds = world pixels

View mode
    ImageInstance.Bounds = screen pixels
```

This distinction is fundamental.

---

## View mode

Use a `View` when the instances should remain attached to the display rather than the game world:

```csharp
var view = renderSurfaceHost.ViewManager.Views[0];

var layer = new ImageInstanceLayer(
    renderSurfaceHost,
    view,
    new Rectangle(0, 0, 1280, 720),
    "screen-images");
```

Instance bounds are then screen-space pixels:

```csharp
layer.Instances.Add(new ImageInstance
{
    Bitmap = cloudBitmap,
    Bounds = new RectangleF(900, 80, 240, 120)
});
```

This is useful for:

- display-space atmosphere
- menu decoration
- HUD decoration
- screen-space weather layers
- camera-independent overlays

Camera movement does not move View-mode instances.

---

## SceneLayer mode

Use a `SceneLayer` when the images belong to the game world:

```csharp
var layer = new ImageInstanceLayer(
    renderSurfaceHost,
    sceneLayer,
    new Rectangle(0, 0, worldWidth, worldHeight),
    "world-clouds");
```

Instance bounds are then world-space pixels:

```csharp
layer.Instances.Add(new ImageInstance
{
    Bitmap = cloudBitmap,
    Bounds = new RectangleF(1400, 180, 320, 140)
});
```

The layer participates in the normal transformation for that `SceneLayer`:

- camera position
- viewport
- zoom
- parallax

This makes SceneLayer mode the natural choice for:

- clouds positioned in the world
- distant birds
- fog banks tied to map regions
- large environmental debris
- decorative objects that should scroll with the camera

---

# The layer bounds

The `ImageInstanceLayer` itself has one bounding rectangle.

In View mode this is:

```csharp
ScreenBounds
```

In SceneLayer mode this is:

```csharp
WorldBounds
```

The bounds serve several purposes:

- they define the coordinate region owned by the layer
- they are supplied to initialization and recycling callbacks
- they define the layer's clipping region
- they provide the source region used when mapping to the rendered destination rectangle
- they define the whole-layer region used by `ForceRefresh()`

An instance's bounds are expressed in the **same coordinate system** as the containing layer.

For example:

```text
SceneLayer WorldBounds
    (0, 0) -> (4000, 2000)

ImageInstance.Bounds
    (1200, 300, 300, 120)

means:
    one image at world pixel 1200,300
```

The instance is not positioned in a separate child-local coordinate system.

---

# The ImageInstance class

An `ImageInstance` contains the state of one rendered image:

```text
Bitmap
Bounds
VelocityX
VelocityY
Rotation
AngularVelocity
Tint
Tag
```

It is intentionally much smaller than a Sprite or game entity.

---

## Bitmap

Every instance requires an `SKBitmap`:

```csharp
var cloud = new ImageInstance
{
    Bitmap = cloudBitmap,
    Bounds = new RectangleF(100, 80, 300, 140)
};
```

Different instances in the same layer may use different bitmaps:

```csharp
layer.Instances.Add(new ImageInstance
{
    Bitmap = cloudA,
    Bounds = new RectangleF(100, 60, 260, 120)
});

layer.Instances.Add(new ImageInstance
{
    Bitmap = cloudB,
    Bounds = new RectangleF(700, 140, 340, 150)
});
```

This is one of the major differences from treating the entire effect as one bitmap-backed DirectDrawing.

---

## Bounds

`Bounds` controls both position and rendered size:

```csharp
Bounds = new RectangleF(
    x,
    y,
    width,
    height);
```

The coordinate meaning depends on the layer mode:

```text
View mode       -> screen pixels
SceneLayer mode -> world pixels
```

The bitmap is scaled into this rectangle when drawn.

That means source bitmap dimensions do not have to match the destination dimensions.

---

## Velocity

Each instance has independent linear velocity:

```csharp
VelocityX = -20f;
VelocityY = 3f;
```

Velocity is measured in pixels per second in the layer's coordinate space.

For a View-mode layer:

```text
pixels = screen pixels
```

For a SceneLayer-mode layer:

```text
pixels = world pixels
```

During each update Gondwana applies:

```text
X += VelocityX * deltaTime
Y += VelocityY * deltaTime
```

For example, a cloud drifting left:

```csharp
layer.Instances.Add(new ImageInstance
{
    Bitmap = cloudBitmap,
    Bounds = new RectangleF(1000, 120, 300, 140),
    VelocityX = -18f
});
```

---

## Rotation and angular velocity

An instance can also rotate independently:

```csharp
Rotation = 15f;
AngularVelocity = 8f;
```

Rotation is measured in degrees.

Angular velocity is degrees per second.

During update:

```text
Rotation += AngularVelocity * deltaTime
```

Rendering rotates the bitmap around the center of its destination rectangle.

For example:

```csharp
layer.Instances.Add(new ImageInstance
{
    Bitmap = leafBitmap,
    Bounds = new RectangleF(500, 100, 48, 48),
    VelocityX = -12f,
    VelocityY = 25f,
    AngularVelocity = 90f
});
```

---

## Tint

Every instance has its own `Tint`:

```csharp
Tint = new SKColor(255, 255, 255, 140);
```

The default is:

```csharp
SKColors.White
```

The tint is assigned to the `SKPaint` used while drawing that instance.

Alpha is useful for translucent imagery such as:

- clouds
- fog
- ghosts
- distant silhouettes
- overlays

For conventional image compositing, the layer uses:

```csharp
SKBlendMode.SrcOver
```

---

## Tag

`Tag` is optional user-defined data:

```csharp
Tag = "high-cloud";
```

or:

```csharp
Tag = weatherCell;
```

Because the instances remain individually addressable, `Tag` can be useful when game code needs to associate a lightweight visual with some external state without turning the instance into a full game object.

Example:

```csharp
var cloud = layer.Instances
    .FirstOrDefault(i => Equals(i.Tag, "storm-front"));
```

---

# Manual population

The simplest style is to create the layer and add instances directly:

```csharp
var clouds = new ImageInstanceLayer(
    renderSurfaceHost,
    sceneLayer,
    new Rectangle(0, 0, worldWidth, worldHeight),
    "clouds");

clouds.Instances.Add(new ImageInstance
{
    Bitmap = cloudA,
    Bounds = new RectangleF(400, 120, 260, 110),
    VelocityX = -8f,
    Tint = new SKColor(255, 255, 255, 150)
});

clouds.Instances.Add(new ImageInstance
{
    Bitmap = cloudB,
    Bounds = new RectangleF(1500, 220, 380, 170),
    VelocityX = -14f,
    Tint = new SKColor(255, 255, 255, 110)
});
```

The `Instances` collection is a normal `List<ImageInstance>`.

That makes individual instances easy to inspect and modify:

```csharp
var first = clouds.Instances[0];
first.VelocityX = -25f;
first.Tint = new SKColor(255, 255, 255, 90);
```

This direct access is intentional.

It is one of the main reasons to use `ImageInstanceLayer` instead of `ParticleSurface`.

---

# Delegate-assisted population

For reusable effects, the layer can initialize itself from a callback.

The initializer receives:

```csharp
Rectangle bounds
Random rng
```

and returns instances:

```csharp
var clouds = new ImageInstanceLayer(
    renderSurfaceHost,
    sceneLayer,
    new Rectangle(0, 0, worldWidth, worldHeight),
    initializer: (bounds, rng) =>
    {
        var result = new List<ImageInstance>();

        for (int i = 0; i < 8; i++)
        {
            float width = rng.Next(180, 360);
            float height = width * 0.45f;

            result.Add(new ImageInstance
            {
                Bitmap = variants[rng.Next(variants.Length)],
                Bounds = new RectangleF(
                    bounds.Left + (float)rng.NextDouble() * bounds.Width,
                    bounds.Top + (float)rng.NextDouble() * bounds.Height,
                    width,
                    height),
                VelocityX = -8f - (float)rng.NextDouble() * 12f,
                Tint = new SKColor(
                    255,
                    255,
                    255,
                    (byte)rng.Next(80, 161))
            });
        }

        return result;
    },
    nickname: "ambient-clouds");
```

The delegate-assisted constructor calls `InitializeInstances()` automatically.

---

# InitializeInstances

`InitializeInstances()` performs a complete rebuild:

```text
1. clear Instances
2. choose the layer's active coordinate bounds
3. call Initializer, if assigned
4. add returned instances
5. ForceRefresh the entire layer
```

You can also assign the initializer after construction:

```csharp
layer.Initializer = (bounds, rng) =>
{
    // build instances
    return instances;
};

layer.InitializeInstances();
```

The bounds passed to the callback are automatically selected from the layer mode:

```text
SceneLayer mode -> WorldBounds
View mode       -> ScreenBounds
```

That allows the same initialization pattern to work in either coordinate space.

---

# UpdateInstance

`UpdateInstance` provides custom per-instance behavior after the built-in velocity and rotation update.

For example, a gentle vertical drift:

```csharp
layer.UpdateInstance = (instance, dt) =>
{
    instance.Bounds = new RectangleF(
        instance.Bounds.X,
        instance.Bounds.Y + MathF.Sin(instance.Bounds.X * 0.01f) * 6f * dt,
        instance.Bounds.Width,
        instance.Bounds.Height);
};
```

The built-in order is important:

```text
apply VelocityX / VelocityY
apply AngularVelocity
call UpdateInstance
check recycling
mark old/new refresh regions
```

So `UpdateInstance` sees the position after normal motion has already been applied.

It may modify more than position:

```csharp
layer.UpdateInstance = (instance, dt) =>
{
    instance.Rotation += 10f * dt;

    if (instance.Bounds.Y < 100)
        instance.Tint = new SKColor(255, 255, 255, 100);
};
```

Use this hook for lightweight behavior that does not justify a separate game entity.

---

# Recycling instances

Persistent ambient visuals often need to wrap around rather than disappear.

For that, `ImageInstanceLayer` provides two callbacks:

```csharp
ShouldRecycle
RecycleInstance
```

Both must be assigned for automatic recycling to occur.

---

## ShouldRecycle

`ShouldRecycle` answers whether an instance should be replaced or repositioned:

```csharp
layer.ShouldRecycle = (instance, bounds) =>
    instance.Bounds.Right < bounds.Left;
```

The `bounds` argument is:

```text
WorldBounds  in SceneLayer mode
ScreenBounds in View mode
```

This makes edge-based recycling straightforward.

---

## RecycleInstance

`RecycleInstance` receives:

```text
the old instance
layer bounds
shared Random
```

and returns the instance that should occupy that slot afterward.

It may return a new object:

```csharp
layer.RecycleInstance = (old, bounds, rng) =>
{
    return new ImageInstance
    {
        Bitmap = old.Bitmap,
        Bounds = new RectangleF(
            bounds.Right + rng.Next(20, 120),
            bounds.Top + rng.Next(0, Math.Max(1, bounds.Height - 120)),
            old.Bounds.Width,
            old.Bounds.Height),
        VelocityX = old.VelocityX,
        Tint = old.Tint,
        Tag = old.Tag
    };
};
```

Or it may reuse the same instance:

```csharp
layer.RecycleInstance = (instance, bounds, rng) =>
{
    instance.Bounds = new RectangleF(
        bounds.Right + rng.Next(20, 120),
        bounds.Top + rng.Next(0, Math.Max(1, bounds.Height - (int)instance.Bounds.Height)),
        instance.Bounds.Width,
        instance.Bounds.Height);

    return instance;
};
```

Reusing the object is appropriate when its identity should remain stable.

Creating a replacement is convenient when the recycled visual should receive entirely new state.

---

# Recipe: continuously wrapping clouds

A typical world-space cloud layer can be built entirely from initialization and recycling callbacks:

```csharp
var clouds = new ImageInstanceLayer(
    renderSurfaceHost,
    sceneLayer,
    new Rectangle(0, 0, worldWidth, worldHeight),
    initializer: (bounds, rng) =>
    {
        var instances = new List<ImageInstance>();

        for (int i = 0; i < 10; i++)
        {
            float w = rng.Next(220, 460);
            float h = w * 0.4f;

            instances.Add(new ImageInstance
            {
                Bitmap = cloudBitmaps[rng.Next(cloudBitmaps.Length)],
                Bounds = new RectangleF(
                    bounds.Left + (float)rng.NextDouble() * bounds.Width,
                    bounds.Top + rng.Next(40, 400),
                    w,
                    h),
                VelocityX = -6f - (float)rng.NextDouble() * 14f,
                Tint = new SKColor(
                    255,
                    255,
                    255,
                    (byte)rng.Next(80, 150))
            });
        }

        return instances;
    },
    shouldRecycle: (instance, bounds) =>
        instance.Bounds.Right < bounds.Left,
    recycleInstance: (instance, bounds, rng) =>
    {
        instance.Bounds = new RectangleF(
            bounds.Right + rng.Next(30, 180),
            bounds.Top + rng.Next(40, 400),
            instance.Bounds.Width,
            instance.Bounds.Height);

        instance.VelocityX =
            -6f - (float)rng.NextDouble() * 14f;

        instance.Tint = new SKColor(
            255,
            255,
            255,
            (byte)rng.Next(80, 150));

        return instance;
    },
    nickname: "cloud-layer");
```

The instances can live indefinitely.

No artificial lifetime is required.

---

# Recipe: drifting leaves

For images that move and rotate:

```csharp
var leaves = new ImageInstanceLayer(
    renderSurfaceHost,
    view,
    screenBounds,
    initializer: (bounds, rng) =>
    {
        var result = new List<ImageInstance>();

        for (int i = 0; i < 20; i++)
        {
            result.Add(new ImageInstance
            {
                Bitmap = leafBitmaps[rng.Next(leafBitmaps.Length)],
                Bounds = new RectangleF(
                    bounds.Left + rng.Next(bounds.Width),
                    bounds.Top + rng.Next(bounds.Height),
                    32,
                    32),
                VelocityX = rng.Next(-25, -8),
                VelocityY = rng.Next(12, 32),
                Rotation = rng.Next(0, 360),
                AngularVelocity = rng.Next(-120, 121)
            });
        }

        return result;
    },
    shouldRecycle: (instance, bounds) =>
        instance.Bounds.Right < bounds.Left ||
        instance.Bounds.Top > bounds.Bottom,
    recycleInstance: (instance, bounds, rng) =>
    {
        instance.Bounds = new RectangleF(
            bounds.Right + rng.Next(0, 100),
            bounds.Top - rng.Next(20, 160),
            instance.Bounds.Width,
            instance.Bounds.Height);

        return instance;
    },
    nickname: "falling-leaves");
```

This remains an image-instance effect rather than a particle effect because every leaf stays directly available through `Instances`.

---

# Recipe: custom per-instance bobbing

`UpdateInstance` is useful when simple linear velocity is not enough.

For example:

```csharp
layer.UpdateInstance = (instance, dt) =>
{
    if (instance.Tag is not float phase)
        phase = 0f;

    phase += dt;
    instance.Tag = phase;

    instance.Bounds = new RectangleF(
        instance.Bounds.X,
        instance.Bounds.Y + MathF.Sin(phase * 2f) * 8f * dt,
        instance.Bounds.Width,
        instance.Bounds.Height);
};
```

`Tag` is useful here as lightweight per-instance state.

For substantial behavior or complex state, prefer a dedicated game object rather than turning `Tag` into a second object model.

---

# Update pipeline

Each `ImageInstanceLayer` update follows roughly this sequence:

```text
1. determine elapsed time
2. select WorldBounds or ScreenBounds
3. for each ImageInstance:
       capture old refresh bounds
       apply linear velocity
       apply angular velocity
       call UpdateInstance
       test ShouldRecycle
       optionally call RecycleInstance
       dirty the old region
       dirty the new region
4. run inherited DirectDrawingMovableBase update behavior
```

The first update has no previous instance timestamp, so the layer does not perform a delta-time-based instance movement on that first call.

---

# Render pipeline

During rendering the layer first chooses its source coordinate rectangle:

```text
SceneLayer mode -> WorldBounds
View mode       -> ScreenBounds
```

It then receives `destRectScreen` from the DirectDrawing rendering pipeline.

Conceptually:

```text
instance coordinate space
        |
        | map relative to layer bounds
        v
screen destination rectangle
        |
        v
SKCanvas.DrawBitmap(...)
```

For each instance Gondwana:

```text
1. maps instance Bounds into screen space
2. applies instance Tint to the shared SKPaint
3. rotates around the destination center, if needed
4. draws the bitmap
```

The canvas is clipped to the `ImageInstanceLayer` destination rectangle.

Instances may continue to exist outside the layer bounds, but pixels outside that clipped region are not rendered.

---

# World-to-screen projection

SceneLayer mode does not draw `ImageInstance.Bounds` directly to the screen.

Instead, the layer maps its world-space coordinate rectangle into the screen-space destination calculated for the DirectDrawing.

Conceptually:

```text
world instance X
    |
    | subtract WorldBounds.Left
    | scale to destination width
    | add destination Left
    v
screen X
```

The same operation is performed for Y.

That is what allows the instances to participate correctly in:

- camera movement
- zoom
- viewport placement
- SceneLayer parallax

View mode uses the same mapping model with `ScreenBounds` as the source rectangle.

---

# Dirty rectangles and refresh behavior

`ImageInstanceLayer` deliberately handles refresh differently from `ParticleSurface`.

A `ParticleSurface` typically refreshes its whole DirectDrawing region after simulation.

`ImageInstanceLayer` instead tracks old and new regions for every instance.

Conceptually:

```text
cloud moves from A to B

old bounds A -> dirty
new bounds B -> dirty
```

This is a good fit for the intended use case:

```text
few objects
large images
persistent instances
```

Refreshing two cloud-sized rectangles is often preferable to refreshing an entire large layer.

---

## SceneLayer dirty regions

In SceneLayer mode the per-instance refresh rectangle is already in world space.

The layer therefore uses:

```csharp
SceneLayer.RefreshQueue.AddWorldRect(...)
```

The normal render pipeline later projects that world dirty region for each relevant View.

---

## View dirty regions

In View mode the instance rectangle is in screen space.

The layer projects that screen-space dirty region back through each SceneLayer's refresh queue using:

```csharp
AddViewScreenRect(...)
```

This is the normal Gondwana mechanism for screen-space DirectDrawing invalidation when the CPU-backed dirty-rectangle path is active.

---

# ForceRefresh vs per-instance refresh

`ImageInstanceLayer` still uses `ForceRefresh()` when the **whole DirectDrawing** needs invalidation.

For example, `InitializeInstances()` performs:

```text
clear everything
rebuild everything
ForceRefresh whole layer
```

Inherited DirectDrawing behavior may also use `ForceRefresh()` for whole-layer state changes such as:

- visibility
- opacity
- fades
- reveal state
- layer bounds

Normal per-instance motion does **not** call `ForceRefresh()` for the entire layer.

Instead:

```text
one ImageInstance changes
    -> dirty old instance rectangle
    -> dirty new instance rectangle
```

That distinction is central to the class's refresh strategy.

---

# Rotated dirty bounds

A rotating rectangle can extend beyond its unrotated `Bounds`.

If Gondwana dirtied only the raw rectangle, rotation could leave stale pixels around the corners.

For non-rotating instances, refresh bounds are simply:

```text
ImageInstance.Bounds
```

For rotating instances, Gondwana uses a conservative square based on the rectangle's half-diagonal:

```text
+-----------------------+
|     conservative      |
|     refresh square    |
|       /-------/       |
|      / image /        |
|     /-------/         |
|                       |
+-----------------------+
```

This square is large enough to contain the image at any rotation angle.

A small additional padding margin is then applied before the rectangle is queued for redraw.

This intentionally trades a little extra redraw area for reliable cleanup around rotated images.

---

# Refresh behavior for unchanged instances

The current update implementation visits every `ImageInstance` and queues its old and new refresh rectangles each active update.

That remains true even if an instance currently has:

```text
VelocityX = 0
VelocityY = 0
AngularVelocity = 0
```

The old and new rectangles may simply be identical.

For the intended use case of relatively few instances, this keeps refresh behavior simple and ensures custom `UpdateInstance` changes are redrawn without requiring separate property-change tracking.

If the desired effect grows into hundreds or thousands of elements, this is another signal that `ParticleSurface` may be the more appropriate abstraction.

---

# Out-of-band instance changes

There is an important dirty-region implication to direct `Instances` access.

The layer captures an instance's **old bounds at the beginning of its own update**.

That means changes made through:

- built-in velocity
- built-in rotation
- `UpdateInstance`
- `RecycleInstance`

can be invalidated correctly because Gondwana knows both the old and new regions.

By contrast, if external game code directly changes:

```csharp
instance.Bounds = someCompletelyDifferentRectangle;
```

between layer updates, the layer no longer knows the previous bounds when its next update begins.

For animated position changes, prefer:

- `VelocityX` / `VelocityY`
- `UpdateInstance`
- `RecycleInstance`

because those paths preserve old/new dirty-region tracking.

Likewise, adding or removing items directly from `Instances` does not itself call `ForceRefresh()`.

For a wholesale runtime rebuild, `Initializer` plus `InitializeInstances()` is the refresh-safe path because `InitializeInstances()` refreshes the entire layer afterward.

---

# Moving the layer vs moving its instances

`ImageInstanceLayer` inherits `DirectDrawingMovableBase`, but there are two separate concepts involved:

```text
layer bounds
    vs
individual ImageInstance.Bounds
```

Instance coordinates are absolute within the selected world or screen coordinate space.

Changing the layer's own bounds does not rewrite every `ImageInstance.Bounds` value.

If the desired behavior is:

```text
move every cloud 100 pixels right
```

update the instances themselves:

```csharp
foreach (var cloud in layer.Instances)
{
    cloud.Bounds = new RectangleF(
        cloud.Bounds.X + 100f,
        cloud.Bounds.Y,
        cloud.Bounds.Width,
        cloud.Bounds.Height);
}
```

For continuous group motion, applying the change inside `UpdateInstance` is preferable because it preserves normal dirty tracking.

The layer bounds are best understood primarily as the coordinate region and clipping/refresh envelope for the collection.

---

# Controlling the whole layer

Because the container is a DirectDrawing, whole-layer rendering state can use normal DirectDrawing APIs:

```csharp
layer.ZOrder = 100;
layer.Opacity = 0.75f;
layer.FadeOut(2f);
layer.Visible = false;
```

These operations affect the rendered collection as a whole.

They do not require you to walk through every `ImageInstance`.

For example:

```text
instance Tint alpha
    = opacity of one instance

layer Opacity
    = opacity of the whole ImageInstanceLayer
```

The two levels can be combined.

---

# Z-order

`ZOrder` belongs to the `ImageInstanceLayer`, not to each `ImageInstance`.

For example:

```csharp
clouds.ZOrder = 50;
```

All instances in that layer are drawn together at that DirectDrawing Z-order.

There is no per-instance Z-order property.

The order inside the collection is therefore also the draw order inside the layer:

```text
Instances[0] draws first
Instances[1] draws next
Instances[2] draws next
...
```

Later instances can cover earlier instances where their bitmaps overlap.

If separate groups need separate engine Z-order values, use separate `ImageInstanceLayer` objects.

---

# Image ownership and disposal

`ImageInstanceLayer.Dispose()` disposes the layer's internal `SKPaint` and then performs the normal DirectDrawing disposal path.

It does **not** dispose each `ImageInstance.Bitmap`.

That is important because several instances may intentionally share the same bitmap:

```csharp
Bitmap = cloudBitmap
```

across many objects.

Bitmap ownership therefore remains with the code or asset system that supplied those images.

When the layer is finished:

```csharp
layer.Dispose();
```

Dispose the bitmap separately only if your code owns it and no other engine object still uses it.

---

# Performance model

`ImageInstanceLayer` is intentionally optimized for a different scale than `ParticleSurface`.

Its model is:

```text
List<ImageInstance>
    + persistent managed objects
    + one bitmap draw per instance
    + old/new dirty rectangles per instance
```

There is no:

- `ArrayPool<ImageInstance>`
- fixed maximum instance count
- automatic compaction
- automatic particle death
- high-volume emission system

This is desirable when there are relatively few long-lived objects because the API remains simple and every instance stays directly addressable.

For example:

```text
8 clouds
20 leaves
12 distant birds
6 fog patches
```

are natural `ImageInstanceLayer` workloads.

Thousands of sparks are not.

---

# Choosing between ImageInstanceLayer and ParticleSurface

Both can draw repeated moving bitmaps, but their lifecycle models are intentionally different.

| | `ImageInstanceLayer` | `ParticleSurface` |
|---|---|---|
| Typical count | Low | High |
| Instance lifetime | Persistent | Usually short-lived |
| Individual identity | Important | Usually unimportant |
| Storage | `List<ImageInstance>` | pooled `Particle[]` |
| Arbitrary rectangular bounds | Yes | particle `Size` model |
| Different bitmap per item | Yes | Yes, but particle-oriented |
| User metadata | `Tag` | No equivalent |
| Automatic emission | No | Yes |
| Automatic lifetime | No | Yes |
| Gravity/acceleration | Custom via update | Built in |
| Burst effects | No | Built in |
| Recycling | Explicit delegates | death/culling + new emission |
| Normal refresh | old/new instance rectangles | whole surface |
| SceneLayer mode | Yes | Yes |
| View mode | Yes | Yes |

Use `ImageInstanceLayer` when you have:

- relatively few objects
- longer-lived instances
- individually meaningful images
- persistent clouds
- fog banks
- decorative background objects
- visuals that should wrap/recycle without dying
- objects whose bitmap, bounds, tint, or tag you want to inspect directly later

Use `ParticleSurface` when you have:

- many objects
- frequent creation and destruction
- short lifetimes
- emission rates
- bursts
- sparks
- smoke
- rain or snow
- fire
- high-volume randomized motion

There is deliberate overlap.

A cloud can be implemented with either system.

The question is whether the cloud is best treated as:

```text
one persistent thing
```

or:

```text
one member of a continuously simulated effect
```

---

# ImageInstanceLayer vs Sprite

An `ImageInstance` is also deliberately lighter than a Sprite.

Use an `ImageInstance` when the visual mostly needs:

```text
bitmap
position
size
velocity
rotation
tint
small custom state
```

Use a Sprite when the object needs richer engine behavior such as:

- sprite animation
- gameplay identity
- collision behavior
- sprite-specific movement/game logic
- richer interaction with the scene

An `ImageInstanceLayer` is not intended to become a second Sprite system.

Its value comes from remaining lightweight.

---

# Practical design advice

For atmospheric layers, keep the model simple.

A good cloud layer often needs only:

```text
several bitmap variants
large Bounds
low horizontal velocity
partial alpha
edge recycling
```

A good leaf layer often needs:

```text
small bitmaps
horizontal + vertical velocity
angular velocity
edge recycling
```

A good fog-bank layer often needs:

```text
very large Bounds
very low velocity
partial alpha
few instances
```

Avoid using `UpdateInstance` as a miniature gameplay engine.

If each instance begins accumulating:

```text
collision
AI
animation state machines
input
complex physics
```

it has probably outgrown `ImageInstanceLayer`.

---

# Mental model

The easiest way to think about `ImageInstanceLayer` is:

```text
ImageInstanceLayer
    = DirectDrawing container + updater + renderer

ImageInstance
    = one persistent lightweight bitmap object

Initializer
    = recipe for the starting collection

UpdateInstance
    = optional per-frame customization

ShouldRecycle
    = decide when an instance should wrap/replace

RecycleInstance
    = perform that wrap/replacement
```

And for coordinate modes:

```text
View mode
    Instances live in screen pixels

SceneLayer mode
    Instances live in world pixels
```

And for refresh behavior:

```text
whole layer changes
    -> ForceRefresh()

one instance moves/changes during Update
    -> dirty old bounds
    -> dirty new bounds
```

That is the core design distinction from `ParticleSurface`.

---

# Where to read next

Core implementation:

- [`Gondwana/Drawing/Direct/ImageLayer/ImageInstanceLayer.cs`](https://isthimius.github.io/Gondwana/api/latest/ImageInstanceLayer_8cs_source.html)
- [`Gondwana/Drawing/Direct/ImageLayer/ImageInstance.cs`](https://isthimius.github.io/Gondwana/api/latest/ImageInstance_8cs_source.html)

Underlying DirectDrawing behavior:

- [`Gondwana/Drawing/Direct/DirectDrawingBase.cs`](https://isthimius.github.io/Gondwana/api/latest/DirectDrawingBase_8cs_source.html)
- [`Gondwana/Drawing/Direct/DirectDrawingMovableBase.cs`](https://isthimius.github.io/Gondwana/api/latest/DirectDrawingMovableBase_8cs_source.html)

For the high-volume alternative:

- [`Gondwana/Drawing/Direct/Particles/ParticleSurface.cs`](https://isthimius.github.io/Gondwana/api/latest/ParticleSurface_8cs_source.html)
- [`Gondwana/Drawing/Direct/Particles/ParticleEmitter.cs`](https://isthimius.github.io/Gondwana/api/latest/ParticleEmitter_8cs_source.html)
- [[Particles]]

Related topics:

- [[DirectDrawing]]
- [[Using Views and Cameras]]
- [[Dirty Rectangles]]
- [[Refresh Queues]]
