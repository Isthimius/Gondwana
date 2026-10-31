A `.gscn` file is a **Gondwana Scene definition**. It stores the persistent three-level scene graph — `Scene`, `SceneLayer`, and `SceneLayerTile` — without serializing runtime ownership back-references or embedding tilesheet object graphs.

## Definition documents and logical members

One physical `.gscn` file contains a document object with a `Definitions` array.
Every member has a unique, non-empty, case-sensitive `ID`. The document location
(loose path, or GAF path plus typed entry name) is independent of that logical identity.

```json
{
  "Definitions": [
    { "ID": "first" },
    { "ID": "second" }
  ]
}
```

This illustrates the envelope and identities; supply the normal format-specific fields
shown below for complete definitions. The same envelope is stored as the bytes of a
single `AssetTypes.SceneDefinition` GAF entry. GAF remains a typed binary container;
loading or saving a document does not create entries for individual members.

```csharp
var selected = SceneDefinitionSerializer.Load("content.gscn", "second");
var definitions = SceneDefinitionSerializer.LoadAll("content.gscn");
var document = SceneDefinitionSerializer.LoadDocument("content.gscn");
SceneDefinitionSerializer.SaveDocument("copy.gscn", document);
SceneDefinitionSerializer.SaveAll("copy.gscn", definitions);

var packed = SceneDefinitionSerializer.Load(assetsFile, "content.gscn", "second");
var packedMembers = SceneDefinitionSerializer.LoadAll(assetsFile, "content.gscn");
```

`LoadDocument`, `LoadAll`, and member-selecting `Load` also accept readable streams and
leave them open. Anonymous streams do not claim physical-source provenance.
`ValidateDocument` reports structural and per-member validation errors, qualified by
logical identity. Parsing rejects null member lists, null members, duplicate identities,
and mixed singleton/envelope objects. Authoring drafts may be structurally valid but
incomplete; runtime materialization runs the format validator.

Existing singleton JSON without an envelope continues to load. `Load(path)` and other
singleton convenience APIs require exactly one member and fail clearly on empty or
multi-member documents. `Save(path, definition)` writes a canonical one-member envelope.
`ToJson(definition)` retains the single-definition JSON projection for existing callers;
use `ToDocumentJson(document)` for canonical document JSON. Singleton JSON examples
below remain supported and can also be placed inside the envelope.

The editor's **Definition** toolbar selects, adds, duplicates, removes, and renames roots.
Saving writes every member, and Save As rebases references for every member without
modifying unrelated content. At least one member remains in an editable session.

Use `SceneDefinitionSerializer.LoadAllScenes` to materialize every member.

Load-all operations roll back newly created runtime content if a member fails.
For physical packaging, export, move, and deletion workflows, see [Assets Files](Assets-Files). These operations preserve logical member identities; [Reference Relocation](Reference-Relocation) describes scope and safety guarantees.

The shared [Import definition workflow](Studio-and-Desktop-Tools#import-into-an-open-document) appends independent members to an open document without changing the source file.

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

`SceneDefinitionSerializer.FromScene()` records GTS and GANI source locations for
referenced runtime dependencies when their definitions match the recorded provenance.
Animation sources include registered cycles reachable through `NextCycle` transitions,
with duplicate and circular references handled once per key. Dependencies without
matching provenance retain their logical references without a physical source path.
`Save(path, scene)` stores source paths relative to the destination GSCN; a direct
`FromScene()` result may contain absolute paths.

These values are for **authoring and tooling only**. `ToScene()` still resolves
frames through the registered tilesheet name and animations through the registered
cycle key; it performs no filesystem loading from these collections.

Loose paths are preferably relative to the containing GSCN. The standalone editor
rebases them on Save As. The shared scene editor previews packed GTS/GANI sources
and their packed images, preserving explicit packed provenance even when loose
duplicates exist. Studio can also open and save the GSCN document itself inside
a GAF; see [packed definition documents](Gondwana-Studio#packed-definition-documents).

In either GTS or GANI source pane, **Add from GAF…** selects an exact typed entry
and a logical member from a GAF/ZIP package. The shared picker validates the
selection, supports encrypted packages and **Reload / retry**, and rejects
conflicting logical names. Studio and standalone editors use the same controls.

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

### Visual map editing

The shared Scene Preview toolbar in Studio and the standalone editor provides:

- **Select**: click an active cell, or drag an inclusive rectangle in layer grid coordinates.
- **Paint / Erase**: hold the left button to brush across cells; each stroke is one Undo step.
- **Rectangle Paint / Rectangle Erase**: drag either direction to preview projected cell outlines; release to commit the whole rectangle as one Undo step.
- **Fill**: click to replace the connected region matching the initial cell's assignment. The operation commits on release; dragging does not change its starting cell.

Select a visible layer in Scene Structure before editing. Paint, Rectangle Paint,
Fill, and Apply to selection use the **last-selected source kind**: either a valid
GTS frame or GANI animation. The source label identifies that choice. Missing or
unresolved sources reject painting; tools never add dependencies automatically.
Frame assignment retains the exact tilesheet, region and frame coordinates.
Animation assignment sets the cycle key, enables and starts the animator, and
assigns its first preview frame. Assigning a frame preserves existing animation
fields, matching the source pane's assignment command.

Fill compares frame identity and authored AnimationKey, EnableAnimator and
StartAnimation, not collision, transform, visibility, fog, ID or nickname. Cells
with different placement metadata can be filled together while retaining their
own metadata. Empty cells and metadata-only cells with no assignment match.
Square-derived layouts use four grid neighbors; flat-top hex columns and
pointed-top hex rows use their six edge-sharing staggered neighbors. Filling is
bounded by the selected layer and does not wrap.

**Apply to selection** and **Erase selection** act on the highlighted set.
**Edit selected properties** opens a small dialog for multi-cell selections:
check each property to apply and enter its value. Mixed properties show a blank
value labeled `(mixed)` until explicitly set. Unchecked properties, tile IDs,
nicknames and empty cells are preserved. Tile Properties continues to edit only
the active cell. A confirmed bulk edit is one Undo operation.

With the preview focused, **Arrow keys** move the active cell and **Shift+Arrow**
extends a grid rectangle, bounded by the layer. **Escape** cancels a pending
gesture or collapses an expanded selection. Property editors and source inputs
retain their own keyboard behavior. Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z use the existing
document history routing. Capture loss, tool/layer/member changes and disposal
cancel pending gestures; Save cancels a pending gesture before saving.

Selection and previews never materialize tiles. Erasing removes the full explicit
tile definition. Undo restores each cell's previous metadata or sparse absence;
unchanged operations add no history. Area operations affecting more than 10,000
cells require confirmation before mutation and may consume substantial history
memory or evict older entries. Large edits run synchronously; cancel at the
confirmation or before releasing a rectangle gesture.

All seven coordinate projections use the existing preview mapping and cell
outlines, including zoom, scroll offsets and layer origins. Rectangles describe
grid membership, not overlapping screen-space image bounds. Copy/paste, Select
All, line tools and custom patterns are not provided. Cross-editor drag-and-drop
(#525) and external content monitoring (#465) remain separate work.

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

## Preserving definition origins in EngineState

`EngineStateSaveOptions` supports `DefinitionPersistence.PreserveSource` for this
format. A definition loaded from a named GAF can retain `AssetsFilePath` and
`AssetEntryName`; a loose definition can retain its path. EngineState compares current
serializable state with its loaded baseline and falls back when it changed. Packed
loads use `AssetsFile.SourcePath`, including an explicitly supplied stream origin;
anonymous archives cannot produce path-based provenance. See
[Definition persistence options](Serialization-and-EngineState#definition-persistence-options)
for collection ownership, generated identities, relative paths, and safe fallback.

## Runtime Scene Viewer

The reusable GSCN editor provides **View Scene** on its preview toolbar, both in standalone Scene tooling and in Gondwana Studio. It launches `Gondwana.Tooling.SceneViewer.WinForms` in a separate process, isolating Engine singleton state from the authoring application.

```powershell
Gondwana.Tooling.SceneViewer.WinForms.exe --scene "Content\Scenes\level1.gscn"
```

The viewer resolves the saved GSCN's GTS and GANI dependencies (including GANI tilesheet sources) from local definitions with explicit authored source fallback, registers runtime content, and calls `SceneDefinitionSerializer.ToScene`. Gondwana owns animation playback, frame transforms and associated geometry, layer projection, wrapping, parallax, and rendering. The viewer uses the current WinForms GPU host lifecycle.

WASD or arrows move the camera continuously; Shift accelerates; the mouse wheel changes runtime viewport zoom; Home resets position/zoom; Esc closes. The resizable window uses normal runtime presentation scaling.

Only saved files are viewed. New/dirty documents require confirmation and a successful save before launch. **Scene Preview** remains lightweight, editable, definition-driven, and Engine-free, with representative/static animation frames. It is not replaced by the viewer.

The Viewer content root is the directory containing the opened GSCN. It scans that directory non-recursively for loose GTS/GANI/GSPR files and typed definitions in adjacent GAFs. Logical GTS names and GANI keys resolve with **loose definition > packed definition > explicit source fallback**; GANI → GTS and GSPR → GTS follow the same policy. Fallback paths are relative to the containing definition/archive. Multiple same-precedence matches fail with candidate source diagnostics rather than choosing arbitrarily.

Adjacent GSPR documents are validated, then only sprites with a `SceneId` matching the viewed scene are materialized on its runtime layers. Matching non-empty sprite IDs or nicknames are deduplicated: loose wins over packed, and remaining same-precedence duplicates fail. Entries with neither identity remain distinct. Other scenes referenced by GSPR are never loaded. This Viewer-specific discovery convention adds no GSCN source fields and changes no format semantics. Packed dependencies that require passwords remain unsupported.
