Gondwana uses several coordinate spaces deliberately.

They represent different questions:

- **Grid space** — which logical cell?
- **World space** — where is something in the scene?
- **Screen space** — where does it appear on the render surface?
- **Layer-local projection space** — how does a layer's grid map into the world?

Keeping those concepts separate allows the engine to support orthogonal, isometric, hexagonal, oblique, scrolling, zooming, parallax, and multi-view rendering without forcing every subsystem to understand every projection.

The most important relationship is:

`Grid -> World -> Screen`

---

## Grid space

Grid space identifies logical positions within a `SceneLayer`.

A coordinate such as:

```text
(4, 7)
```

means something like:

> column 4, row 7 on this layer

It does **not** inherently mean a particular world-pixel position.

That distinction matters because the same grid coordinate can project differently depending on the layer's coordinate system.

For example, `(4, 7)` on an orthogonal layer and `(4, 7)` on an isometric layer refer to equivalent logical grid locations, but their world positions are calculated differently.

Grid space is useful for:

- tile addressing
- map and board logic
- spawn locations
- neighbor calculations
- coordinate-system-aware movement
- converting logical map positions into renderable positions

Grid space belongs to a particular layer. It is not a global scene coordinate system.

---

## World pixel space

World pixel space is Gondwana's primary spatial and simulation coordinate system.

Once something has been projected from grid space, its position is normally expressed in world pixels.

This is where most engine systems operate.

Examples include:

- sprite positions
- collision rectangles
- camera positions
- layer origins
- movement
- dirty world regions
- direct drawings attached to scene layers
- world bounds

A world position such as:

```text
(1200, 640)
```

means 1200 pixels from the world's X origin and 640 pixels from its Y origin.

It says nothing about where that point currently appears on the user's monitor.

The camera may be elsewhere. The view may occupy only part of the render surface. The layer may use parallax. The viewport may be zoomed.

Those are rendering concerns.

This separation is important because gameplay objects should not need to move simply because the camera moved.

A sprite can remain at:

```text
World: (1200, 640)
```

while its screen position changes continuously as the camera pans across the scene.

For most simulation code, **world space is the source of truth**.

---

## Screen space

Screen space represents actual pixel positions on the render surface.

By the time a world position reaches screen space, Gondwana has applied the relevant rendering transformations, including:

- camera position
- view placement
- viewport offsets
- parallax
- zoom

A world object therefore does not have one permanently meaningful screen coordinate.

Its screen coordinate depends on **which view is rendering it and how that view is configured**.

Conceptually:

```text
World position
    |
    | camera / parallax
    v
View-relative position
    |
    | viewport placement / zoom
    v
Screen position
```

Screen space is used for things such as:

- final backbuffer rendering
- presentation rectangles
- pointer hit testing
- HUD elements
- view-bound direct drawings
- widgets and overlays

A UI element attached directly to a `View`, for example, normally lives in screen-oriented space rather than moving through the scene with the camera.

---

## Layer-local projection space

A `SceneLayer` owns the relationship between its logical grid and world pixel space.

This is where Gondwana's coordinate-system implementations matter.

A layer may use:

- orthogonal coordinates
- isometric rhombic coordinates
- isometric axial coordinates
- flat-top hex axial coordinates
- pointed-top hex axial coordinates
- oblique coordinates

Each system answers the same fundamental question:

> Given this logical grid coordinate, where does it exist in world space?

For an orthogonal layer, the calculation may be nearly as simple as:

```text
worldX = column * tileWidth
worldY = row    * tileHeight
```

For isometric, hexagonal, or oblique layouts, that projection becomes more specialized.

The important architectural point is that **the rest of the engine does not need to know the formula**.

The `SceneLayer` and its coordinate implementation handle the projection.

Once the result becomes a world-pixel position, cameras, collisions, rendering, and other systems can work with it normally.

---

## The pipeline

The common path through Gondwana is:

```text
Grid
  |
  | SceneLayer coordinate projection
  v
World
  |
  | Camera + parallax + view/viewport transform
  v
Screen
```

Not every object goes through every stage.

A sprite may be positioned directly in world space:

```text
World -> Screen
```

A HUD element may already be view-relative:

```text
Screen
```

A tile begins naturally in grid space:

```text
Grid -> World -> Screen
```

The distinction is about **what the coordinate means**, not merely the numeric type used to store it.

---

## Camera movement does not move the world

One of the easiest mistakes when working with rendering code is to mentally combine world and screen coordinates.

Suppose a sprite is located at:

```text
World: (1000, 500)
```

and the camera moves 200 pixels to the right.

The sprite is still at:

```text
World: (1000, 500)
```

Its screen position moves left because the camera changed what portion of the world is visible.

Conceptually:

```text
screenX = worldX - cameraX
screenY = worldY - cameraY
```

The actual rendering path may include additional transformations, but the principle remains the same.

**The camera changes the view of the world. It does not change the world.**

---

## Layer origins

Layers can also have their own origins within world space.

That means a grid coordinate is first interpreted relative to its layer and then placed into the larger scene.

Conceptually:

```text
Grid coordinate
      |
      v
Layer-local projected pixel
      |
      + Layer world origin
      v
World pixel
```

This allows separate layers to use the same logical grid coordinates while occupying different positions in the world.

It also reinforces why grid coordinates should not be treated as global scene coordinates.

---

## Multiple views

Screen space becomes especially important when a scene is rendered through more than one `View`.

The same world object can appear at different screen coordinates in different views.

For example:

```text
                  +-> View A -> Screen position A
World position ---|
                  +-> View B -> Screen position B
```

Both views are rendering the same world position.

They may simply have:

- different cameras
- different viewport rectangles
- different zoom levels
- different dimensions

This is one reason Gondwana keeps simulation coordinates independent of the final render surface.

---

## Parallax

Parallax is another transformation between world and screen.

A layer can move visually at a different rate from the camera while its objects remain in world space.

The object itself does not acquire a special "parallax position."

Instead, the layer's world position is transformed differently while being rendered through a view.

This keeps parallax where it belongs: in the rendering transformation rather than in gameplay state.

---

## World-bound vs view-bound drawings

The same distinction appears in Gondwana's direct-drawing system.

A direct drawing associated with a `SceneLayer` behaves like world content.

Its position participates in the world-to-screen transformation:

```text
World -> Screen
```

A direct drawing associated directly with a `View` behaves like an overlay.

It is already tied to the rendered view and does not scroll through the world with the camera.

That makes view-bound drawings appropriate for things such as:

- HUD elements
- debug overlays
- screen-space decoration
- interface components

---

## Converting in the correct direction

When converting coordinates, first ask what the value currently represents.

For example:

### Tile placement

You know:

```text
Grid: (4, 7)
```

You need:

```text
World position
```

Use the layer's coordinate system.

---

### Rendering a sprite

You know:

```text
World position
```

You need:

```text
Screen position
```

Use the appropriate view and viewport transformation.

---

### Mouse interaction with the world

You know:

```text
Screen position
```

You may need:

```text
World position
```

and possibly afterward:

```text
Grid position
```

So the conceptual path is reversed:

```text
Screen -> World -> Grid
```

That distinction becomes important for:

- tile selection
- editor tools
- mouse interaction
- placement previews
- world-space hit testing

---

## A useful rule of thumb

When deciding which coordinate space a piece of code should use:

**Map logic**

Use **grid space** when the logical cell matters.

**Simulation**

Use **world space** when physical position, movement, or bounds matter.

**Rendering and UI**

Use **screen space** when actual pixels on a particular view or render surface matter.

**Projection**

Let the `SceneLayer` coordinate implementation handle the relationship between grid and world space.

Avoid carrying screen coordinates back into simulation state unless there is a specific reason to do so.

---

## Mental model

If you remember only one thing from this page, remember:

```text
Grid -> World -> Screen
```

Grid describes the logical map.

World describes the scene.

Screen describes one particular view of that scene.

Gondwana keeps those concepts separate so that changing how something is **viewed** does not change where it **exists**.

---

## Where to read next

- [`Gondwana/Scenes/SceneLayer.cs`](https://isthimius.github.io/Gondwana/api/latest/SceneLayer_8cs_source.html)
- [`Gondwana/Rendering/Views/View.cs`](https://isthimius.github.io/Gondwana/api/latest/View_8cs_source.html)
- [`Gondwana/Rendering/Views/Viewport.cs`](https://isthimius.github.io/Gondwana/api/latest/Viewport_8cs_source.html)
- coordinate implementations under [`Gondwana/Drawing/Coordinates/`](https://github.com/Isthimius/Gondwana/tree/master/Gondwana/Drawing/Coordinates)
- [[Coordinate Systems]]

---

## Virtual wrapped coordinates

Periodic layers retain virtual world/grid coordinates for position and presentation. Explicit lookup resolves them to canonical content; that object's ordinary bounds remain canonical. Use translated instances for rendering and collisions. See [[SceneLayer Wrapping]].
