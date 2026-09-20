A `.gscn` file is a **Gondwana Scene definition**. It stores the persistent three-level scene graph — `Scene`, `SceneLayer`, and `SceneLayerTile` — without serializing runtime ownership back-references or embedding tilesheet object graphs.

The format is designed to be usable by both the runtime and scene-editing tooling.

## On this page

- [The serialization model](#the-serialization-model)
- [What a GSCN definition stores](#what-a-gscn-definition-stores)
- [Frames and tilesheets](#frames-and-tilesheets)
- [Animations and GANI](#animations-and-gani)
- [Authoring dependency sources](#authoring-dependency-sources)
- [Collision settings](#collision-settings)
- [Loading and saving](#loading-and-saving)
- [Sparse tile definitions](#sparse-tile-definitions)
- [Provenance](#provenance)
- [EngineState integration](#enginestate-integration)
- [Validation](#validation)
- [Standalone GSCN editor](#standalone-gscn-editor)

## The serialization model

Gondwana keeps the portable definition separate from the runtime object graph:

```text
.gscn JSON
    |
    v
SceneDefinition
    |
    v
runtime Scene
    |
    +-- SceneLayer
           |
           +-- SceneLayerTile
```

The main types live under:

```csharp
Gondwana.Scenes.GSCN
```

and include:

| Type | Responsibility |
| --- | --- |
| `SceneDefinition` | Root scene definition |
| `SceneLayerDefinition` | One tile-grid layer |
| `SceneLayerTileDefinition` | Persistent state for one grid cell |
| `SceneFrameDefinition` | Lightweight tilesheet/frame reference |
| `SceneCollisionProfileDefinition` | Named scene collision profile |
| `SceneDefinitionSerializer` | JSON, file, stream, runtime conversion, and materialization |
| `SceneDefinitionValidator` | Structural authoring diagnostics |
| `SceneDefinitionSource` | Definition provenance |

This separation avoids persisting runtime relationships such as:

```text
SceneLayer -> Scene
SceneLayerTile -> SceneLayer
```

Those references are reconstructed when a runtime scene is materialized.

## What a GSCN definition stores

At the root, `SceneDefinition` stores:

- the scene `ID`
- collision group names
- collision profiles
- authoring-time GTS source locations
- authoring-time GANI source locations
- layer definitions
- source/provenance metadata

Each `SceneLayerDefinition` stores persistent layer configuration including:

- `ID`
- coordinate system
- column and row counts
- tile width and height
- Z-order
- parallax
- visibility
- horizontal and vertical wrapping
- grid/collision debug-display flags
- world-space origin
- default tile collision profile
- tile entries

Each `SceneLayerTileDefinition` can store:

- grid coordinates `X` and `Y`
- persistent tile `Id`
- nickname
- visibility
- current frame reference
- animator enablement
- optional GANI animation key
- whether that assigned animation starts when the scene is materialized
- fog enablement
- collision adjustment
- collision type
- frame-following collision flags
- collision profile name

Runtime-only objects such as refresh queues, collider registries, coordinate-system instances, and ownership back-references are rebuilt rather than serialized.

## Frames and tilesheets

A GSCN does not embed a runtime `Tilesheet`.

A tile frame is represented by a lightweight reference:

```json
"Frame": {
  "Tilesheet": "Terrain",
  "RegionName": "default",
  "XTile": 2,
  "YTile": 1
}
```

The referenced tilesheet must be registered before the scene is materialized.

This is why EngineState restores tilesheets before scenes. When GSCN tiles also
reference GANI animations, the animation registry must likewise be restored before
scene materialization. EngineState's restore order is:

```text
AssetsFiles -> Audio -> Tilesheets -> Cycles/GANI -> Scenes/GSCN -> Sprites
```

## Animations and GANI

GSCN refers to animations by logical key. It does **not** embed a runtime
`Cycle`, `Animator`, current frame index, playback direction, timer state, or
the GANI definition itself.

A scene tile can store:

```json
"EnableAnimator": true,
"AnimationKey": "actor.walk",
"StartAnimation": true
```

The fields have separate jobs:

| Field | Meaning |
| --- | --- |
| `EnableAnimator` | Keep an animator on the tile even if no animation is assigned. |
| `AnimationKey` | Assign the registered GANI/cycle with this logical key. |
| `StartAnimation` | Start the assigned animation when the runtime scene is materialized. |

An `AnimationKey` implicitly creates the runtime animator, so a hand-authored
definition does not need to set `EnableAnimator` merely to assign an animation.
`EnableAnimator` remains useful for the existing case where a tile should have an
animator but no initial cycle.

`StartAnimation: true` requires an `AnimationKey`. The structural validator
checks that relationship, but it does not consult the runtime cycle registry.
Actual key resolution happens during scene materialization.

The referenced animation must therefore already be registered:

```text
.gts
  |
  v
.gani
  |
  v
.gscn
```

If the key is not registered, materialization fails with an
`InvalidDataException` instead of embedding or reconstructing an animation from
the scene file.

## Authoring dependency sources

Logical runtime references deliberately do not contain filesystem paths. Tooling
nevertheless needs to reopen the GTS and GANI definitions used to author a scene.

`SceneDefinition` therefore carries two authoring-only collections:

```csharp
List<SceneTilesheetSourceDefinition> TilesheetSources
List<SceneAnimationSourceDefinition> AnimationSources
```

A tilesheet source maps a logical tilesheet name to either a loose `.gts` path
or a packed GAF definition entry. An animation source does the same for a logical
`AnimationKey` and GANI definition.

For example:

```json
"TilesheetSources": [
  {
    "Tilesheet": "Terrain",
    "Kind": "LooseDefinitionFile",
    "GtsPath": "../tiles/terrain.gts"
  }
],
"AnimationSources": [
  {
    "AnimationKey": "world.water",
    "Kind": "LooseDefinitionFile",
    "GaniPath": "../animations/water.gani"
  }
]
```

These values are for **authoring and tooling only**. `ToScene()` still resolves
frames through the registered tilesheet name and animations through the registered
cycle key; it performs no filesystem loading from these collections.

Loose paths are preferably relative to the containing GSCN. The standalone editor
rebases them on Save As. Packed source metadata is preserved for future/tooling
use even when that editor cannot preview the packed definition directly.

The metadata is optional for runtime compatibility. Older GSCN documents with only
logical references remain valid.

## Collision settings

A scene tile's explicit collision behavior is stored with `CollisionType`:

| Value | Meaning |
| --- | --- |
| `None` | No tile collision |
| `Blocking` | Solid collision |
| `Trigger` | Non-blocking trigger/overlap collision |

`CollisionsEnabled` is **not** duplicated in GSCN. Runtime `Tile.CollisionsEnabled` is derived from the effective `CollisionType`.

A tile may instead follow collision metadata from its current GTS frame:

```json
"AdjustCollisionAreaByFrame": true,
"CollisionTypeByFrame": true
```

When these flags are enabled, frame-derived values have final authority. During GSCN materialization Gondwana applies explicit collision values first, then enables the `*ByFrame` flags so the current frame can supply the effective values.

A `CollisionProfileName` controls the scene collision group/mask role. It does not by itself enable collision.

## Loading and saving

Save a runtime scene:

```csharp
using Gondwana.Scenes.GSCN;

SceneDefinitionSerializer.Save(
    "Content/Scenes/level1.gscn",
    scene);
```

Load and materialize a runtime scene:

```csharp
Scene scene = SceneDefinitionSerializer.LoadScene(
    "Content/Scenes/level1.gscn");
```

If tooling only needs the definition model:

```csharp
SceneDefinition definition =
    SceneDefinitionSerializer.Load(
        "Content/Scenes/level1.gscn");
```

Convert without writing a file:

```csharp
SceneDefinition definition =
    SceneDefinitionSerializer.FromScene(scene);

string json =
    SceneDefinitionSerializer.ToJson(definition);
```

A GSCN definition can also be stored in an `AssetsFile` using:

```csharp
AssetTypes.SceneDefinition
```

and loaded through the serializer's assets-file overload.

## Sparse tile definitions

A hand-authored or tool-generated GSCN does not have to write an entry for every grid cell.

For example, a 100 × 100 layer may contain definitions only for cells that differ from their defaults.

When materialized, omitted cells remain ordinary default `SceneLayerTile` instances.

Runtime-to-GSCN conversion currently writes every cell so a runtime snapshot preserves the complete persistent tile state.

## Provenance

`SceneDefinition.Source` records where the definition came from.

`SceneDefinitionSourceKind` supports:

| Kind | Meaning |
| --- | --- |
| `None` | No source is known |
| `LooseDefinitionFile` | Loaded from a loose `.gscn` file |
| `PackedDefinitionFile` | Loaded from a `.gscn` entry in an `AssetsFile` |
| `Generated` | Generated from runtime state or tooling |

Provenance describes the definition's origin. It is not a substitute for scene identity; `SceneDefinition.ID` remains the runtime scene identity.

## EngineState integration

EngineState can store scenes in either form.

### Inline

Inline GSCN definitions are the default:

```csharp
Engine.Instance.State.SaveToFile(
    "session.json",
    separateGscnFiles: false);
```

The scene entry contains its `SceneDefinition` inside the EngineState JSON.

### Separate files

Externalize the definitions with:

```csharp
Engine.Instance.State.SaveToFile(
    "session.json",
    separateGscnFiles: true);
```

For `session.json`, Gondwana creates:

```text
session.json
session.scenes/
    <scene-id>.gscn
    ...
```

The EngineState file stores a relative `GscnPath` when possible.

External files are written through `SceneDefinitionSerializer`, so they remain clean standalone GSCN documents rather than acquiring EngineState `$id`/`$ref` metadata.

`separateGscnFiles` is independent of `separateGtsFiles`; tilesheet and scene definitions can each be inline or external.

When a saved GSCN contains `AnimationKey` values, save the corresponding
`EngineStateParts.Cycles` data as well (inline GANI or separate `.gani` files).
On load, select both `Cycles` and `Scenes` unless the required animations are
already registered. Scenes intentionally do not auto-select every external
dependency; this matches the existing GSCN/GTS behavior for independently managed
content.

Sprites remain a separate EngineState category. A serialized sprite stores `SceneId` and `SceneLayerId`; after scenes are restored, EngineState reconnects the sprite to the canonical materialized layer.

Older EngineState files that stored raw Scene/SceneLayer graphs remain readable.

## Validation

Tooling can validate a definition without constructing a runtime scene:

```csharp
IReadOnlyList<string> errors =
    SceneDefinitionValidator.Validate(definition);
```

Validation checks include:

- duplicate collision groups and profiles
- unknown collision-profile group references
- invalid grid or tile dimensions
- duplicate layer IDs
- duplicate or out-of-range tile coordinates
- invalid collision values
- missing frame tilesheet/region information
- invalid frame coordinates
- empty animation keys
- `StartAnimation` without an `AnimationKey`

This makes the definition model suitable for scene tooling: editors can inspect and validate GSCN data before loading runtime rendering or collision infrastructure.

## Standalone GSCN editor

`Tooling/Gondwana.Tooling.Scenes.WinForms` provides the standalone Windows GSCN
editor. Its public `SceneEditorControl` is also the reusable surface intended for
Gondwana Studio.

Each scene editor owns an inner DockPanelSuite workspace with:

- Scene structure
- Scene preview
- GTS frame sources
- GANI animations
- Properties
- Tile properties
- Validation

The outer application treats the entire editor as one document. Inner panes can be
split or tabbed only inside that editor. Layout persistence is intentionally not
implemented.

The preview is definition-driven: it does not boot a `GameHost` or register a
runtime `Scene`. It reuses Gondwana's `SceneLayer` coordinate-conversion math
to position the authoring data and composes loaded GTS images by layer Z-order.

The Scene preview uses the same zoom interaction as the GTS editor: **Ctrl+mouse
wheel** and the **− / +** toolbar controls zoom in 1.25× steps, the selector
provides common fixed percentages plus **Fit**, and scrollbars appear when the
zoomed scene exceeds the viewport. A checked **Grid** toolbar button controls
preview grid-line visibility without hiding the selected-tile outline.

GSCN sparse-tile semantics are preserved. Selecting a grid cell does not create a
tile entry; editing a property or assigning GTS/GANI content does. **Clear tile**
removes that explicit entry again.

For legacy GSCN documents without source metadata, the editor may recover an
unambiguous matching GTS/GANI from the GSCN's own directory. Recovery is
non-recursive and never chooses among multiple matches.

## In short

1. GSCN is the portable definition format for Gondwana scenes.
2. It represents the persistent Scene → SceneLayer → SceneLayerTile hierarchy without ownership back-references.
3. Frames reference registered tilesheets by logical name, region, and coordinates.
4. Animations reference registered GANI/cycle definitions by logical `AnimationKey`.
5. Optional `TilesheetSources` / `AnimationSources` locate authoring definitions without changing runtime resolution.
6. Assignment and automatic start are separate; GSCN does not embed animator runtime state.
7. `CollisionType` is authoritative; `CollisionsEnabled` is derived.
8. `*ByFrame` flags allow a scene tile to follow GTS frame collision metadata.
9. EngineState can embed GSCN definitions or reference separate `.gscn` files.
10. The same definition model is intended to support runtime loading and scene UI tooling.
