`SceneLayer.WrapHorizontally` and `WrapVertically` make a layer periodic. The engine draws and collides with translated instances of its existing content; it does not clone tiles, sprites, drawings, or widgets.

## Configure a repeating world

```csharp
var world = scene.AddLayer(columnCount: 100, rowCount: 80, width: 32, height: 32);
world.WrapHorizontally = true;
world.WrapVertically = true;

SceneLayerTile? tile = world.ResolveWrappedTile(-1, 80); // canonical [99, 0]
PointF canonical = world.WrapGrid(new PointF(101, -1)); // (1, 79)
```

Both flags default to `false`. They retain their serialized names, so previously saved scenes containing `true` now enable periodic behavior when loaded.

## Canonical content and virtual coordinates

The grid owns one canonical tile per cell. Virtual coordinates identify where an instance appears: column `100` in a 100-column layer is an image of column `0`, column `101` is an image of `1`, and column `-1` is an image of `99`. Coordinates can be several periods outside the grid. `WrapGrid` preserves fractions and changes only enabled axes.

The ordinary indexer remains bounds-checked: `world[-1, 0]` returns `null`, even when wrapping is enabled. Use `ResolveWrappedTile` for explicit integer resolution, or `WrapGrid` before indexing. An out-of-range disabled axis still returns `null`. Adjacency follows the coordinate system's existing direction rules and crosses enabled seams.

World/grid conversion methods retain virtual coordinates; they do not normalize positions implicitly. Resolving a tile returns the canonical object, whose ordinary bounds remain at its canonical position. To work with repeated world-space rectangles, `GetWrappedOffsets(contentBounds, queryBounds)` returns the translations whose copies intersect the query.

## Projected grids

“Horizontal” means the **column axis**, and “vertical” means the **row axis**. These axes need not be horizontal or vertical on screen.

| Coordinate system | Repetition |
| --- | --- |
| Orthogonal | Columns repeat along world X; rows repeat along world Y. |
| Isometric Rhombic | Both period vectors are diagonal. |
| Isometric Axial | Columns repeat horizontally; rows repeat diagonally. |
| Oblique Right / Left | Columns repeat horizontally; rows repeat along the projection's shear. |
| Hex Axial Flat Top | Column staggering requires an even column count when columns wrap. |
| Hex Axial Pointed Top | Row staggering requires an even row count when rows wrap. |

Period vectors come from the coordinate strategy's anchors. A whole period must produce the same world-space displacement at representative positions, including negative positions and both stagger parities. Pixel-rounded projections can impose additional dimensional constraints: for example, odd tile dimensions combined with an odd isometric period may alternate between two pixel displacements. Such configurations raise `InvalidOperationException`; use dimensions that produce an integral, consistent period.

Validation happens when periodic geometry is used, rather than in an early property setter. This permits ordinary construction and JSON property ordering. Subsequent lookups, rendering, collision queries, and camera operations validate the current configuration again.

## Sprites and movement

Layer wrapping defines the topology used for presentation and interaction. A sprite's `Movement.WrapX` / `WrapY` options independently determine whether its movement controller normalizes its position at a boundary. Enabling the layer flags does **not** enable those movement options on every sprite. Existing movement-only wrapping remains available on non-periodic layers.

Sprites on a periodic layer render at equivalent positions around seams, including when their artwork straddles an edge. Each visible image shares the same canonical sprite and gameplay state.

## Cameras

A camera can move continuously along wrapped axes. It is not periodically teleported. Its configured `WorldBoundsPx` continues to constrain non-wrapped axes. For projected grids, clamping operates in the period-vector basis; the configured world rectangle and viewport are projected into that basis to determine the remaining constrained interval. With both flags off, the existing rectangular clamping behavior is unchanged.

`FollowCentered` uses the target's layer. Otherwise, the first visible layer in the scene's insertion order supplies camera topology, independent of layer Z-order. Other layers repeat according to their own dimensions and flags; they do not combine their periods or override the camera's topology. This makes mixed background/gameplay layers deterministic.

Following chooses the equivalent target position nearest the current camera center. When a movement controller normalizes a sprite from one edge to the opposite canonical edge, following continues near the seam instead of scrolling backward through the map. `RectangleF.Empty` world bounds still means no clamping.

## Collisions at seams

Static tiles and dynamic colliders are queried as a canonical collider plus translated bounds. This handles horizontal, vertical, and corner seams, negative positions, and objects straddling a seam. Tile/frame collision regions and collision adjustments are applied before the translation. Solid resolution uses the translated bounds; trigger and solid events identify the original owners.

For custom collision queries, use `ColliderRegistry.QueryInstances` and each `ColliderInstance.BoundsWorldPx`. The older `QueryAabb` returns unique canonical collider identities; inspecting their ordinary bounds does not describe a seam instance. Ignoring a collider excludes all its images, so an object does not collide with its own repeated copies.

Collision remains the existing discrete AABB system. Wrapping does not add swept collision detection or change collision masks, profiles, or trigger rules.

## DirectDrawing and widgets

SceneLayer-bound DirectDrawing content repeats using the same world-space period vectors as tiles. World-space widget children repeat with their layer too. A following health bar and its target therefore use matching translations. View-bound drawings, HUDs, menus, and other view-space UI remain fixed and never repeat.

Widget hit testing recognizes repeated copies and routes events to the canonical widget. Pointer capture retains the selected instance's world translation in `WidgetPointerEventArgs.WrappedOffsetWorldPx`; releasing over another copy does not click the originally selected copy. Dragging uses the original pointer delta, retaining the selected image without shifting the canonical widget by a whole period. Custom controls that compare pointer coordinates with canonical screen bounds should translate those bounds by `WrappedOffsetWorldPx * view.Viewport.Zoom`.

## Rendering and cost

The engine includes all intersecting repetitions, including corners and views spanning several complete periods. It selects copies using actual artwork bounds, handles overhang, and sorts tile instances using translated positions. Fog, grid lines, and collision outlines use each instance's translation.

Wrapped rendering currently scans canonical content to select intersecting instances. Bitmap hosts use full-view composition when a visible wrapped layer is present and the scene changes, ensuring every visible copy refreshes. GPU hosts retain their full-frame path. Large layers and heavily zoomed-out views can therefore cost more than non-wrapped layers.

Queries reject non-finite or excessive instance ranges rather than entering unbounded loops. A single content query or rendered layer is limited to one million candidate/visible instances. Keep views and object sizes reasonable relative to the layer periods. Spatial indexing and more selective repeated dirty-region invalidation are possible future optimizations.

---

See also [[Tiles and Tile-Based SceneLayers]], [[Coordinate Systems]], [[Views, Cameras, and Viewports]], [[Collision Detection]], [[DirectDrawing]], and [[Widgets Overview]].
