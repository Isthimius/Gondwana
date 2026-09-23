GANI is Gondwana's portable definition format for reusable tile animations.

Each frame may specify `DurationSeconds`, a finite positive display duration.
Omitting it (or using null) retains the cycle's `ThrottleTime`. Timing belongs
to a sequence occurrence, so the same image can appear twice with different
durations. Runtime playback and Studio preview use the currently displayed
frame's duration, including the return leg of ping-pong playback. Select a frame
in Studio to edit its duration in the property grid; clear it to use the default.

For cycles with explicit frame durations, `TotalCycleTime` sums display durations
(counting interior ping-pong frames twice). Simple cycles include the final
frame's display duration. Cycles without overrides retain their historical total
time calculation, including Simple's transition-time convention. A zero legacy
`ThrottleTime` still stops playback safely; explicit overrides must be positive.

A `.gani` file describes one registered animation cycle without serializing the runtime `Cycle`, `FrameSequence`, `Frame`, or `Tilesheet` object graphs.

The format lives under:

```text
Gondwana.Drawing.Animation.GANI
```

and follows the same definition-file approach used by GTS and GSCN.

---

## Contents

- [What GANI represents](#what-gani-represents)
- [Relationship to GTS](#relationship-to-gts)
- [Relationship to GSCN](#relationship-to-gscn)
- [Definition model](#definition-model)
- [Example](#example)
- [Loading and saving](#loading-and-saving)
- [Runtime materialization](#runtime-materialization)
- [Next-cycle transitions](#next-cycle-transitions)
- [Validation](#validation)
- [Packed GANI definitions](#packed-gani-definitions)
- [Provenance](#provenance)
- [EngineState integration](#enginestate-integration)
- [Tooling model](#tooling-model)
- [Where to read next](#where-to-read-next)

---

## What GANI represents

At runtime Gondwana animation is built from:

```text
Tilesheet
   |
   v
Frame
   |
   v
FrameSequence
   |
   v
Cycle
   |
   v
Animator
```

GANI represents the reusable definition portion of that pipeline:

```text
.gts / registered tilesheet frames
            |
            v
     AnimationDefinition
            |
            v
          Cycle
            |
            v
         Animator
```

A GANI file does **not** store per-object playback state such as:

- which tile or sprite is currently using the animation
- the current frame index of an animator
- whether one specific animator is paused
- animator events
- the owning `Tile`

Those remain runtime concerns.

A GANI definition corresponds to one reusable registered `Cycle`.

---

## Relationship to GTS

GANI depends on tilesheet frames.

It does not embed a `Tilesheet` or copy GTS image/region metadata into the animation file. Each animation frame stores a lightweight runtime reference:

```text
Tilesheet logical name
Region name
X tile
Y tile
```

For example:

```json
{
  "Tilesheet": "world",
  "RegionName": "water",
  "XTile": 1,
  "YTile": 0
}
```

The corresponding tilesheet must be registered before the GANI definition is materialized into a runtime `Cycle`.

For authoring tools, GANI can also record where that logical tilesheet definition came from through `TilesheetSources`. A loose source can point to a relative `.gts` path, while the model also has a packed form for a GTS entry inside a Gondwana assets file:

```json
"TilesheetSources": [
  {
    "Tilesheet": "world",
    "Kind": "LooseDefinitionFile",
    "GtsPath": "../tiles/world.gts"
  }
]
```

These source entries are **authoring/dependency metadata**. They help editors reopen the correct GTS definitions, but runtime materialization does not load those files. `AnimationDefinitionSerializer.ToCycle(...)` still resolves frame references through `TilesheetRegistry`.

Relative source paths are interpreted from the containing GANI file.

That gives Gondwana a clear dependency direction:

```text
.gts
 |
 v
.gani
```

Changing the source GTS can therefore update the frames available to animations without duplicating tilesheet definitions inside every animation file.

---

## Relationship to GSCN

GSCN scene tiles can assign a reusable GANI animation by logical key:

```json
"AnimationKey": "actor.walk",
"StartAnimation": true
```

The scene file stores only the key and whether the assignment should begin playing
when the scene is materialized. It does not copy the GANI frames or serialize the
runtime `Cycle`/`Animator` graph.

The complete definition dependency direction is therefore:

```text
.gts
 |
 v
.gani
 |
 v
.gscn
```

When materializing a GSCN that contains an `AnimationKey`, the corresponding GANI
cycle must already be registered. EngineState satisfies this when both
`EngineStateParts.Cycles` and `EngineStateParts.Scenes` are selected because
cycles are restored before scenes.

## Definition model

The root type is:

```csharp
AnimationDefinition
```

Its persistent fields are:

| Property | Meaning |
| --- | --- |
| `Key` | Global animation/cycle key |
| `ThrottleTime` | Seconds between frame transitions |
| `CycleType` | `Simple`, `Repeating`, or `PingPong` |
| `HideTileOnCycleEnd` | Runtime cycle behavior flag |
| `NextCycleKey` | Optional key for the follow-on cycle |
| `TilesheetSources` | Optional authoring-time locations for logical GTS dependencies |
| `Frames` | Ordered lightweight GTS frame references |
| `Source` | Definition provenance |

Each `AnimationTilesheetSourceDefinition` contains:

| Property | Meaning |
| --- | --- |
| `Tilesheet` | Logical tilesheet name matched by animation frames |
| `Kind` | `LooseDefinitionFile` or `PackedDefinitionFile` |
| `GtsPath` | Loose GTS path, preferably relative to the GANI file |
| `AssetsFilePath` | Assets-file path for a packed GTS source |
| `AssetEntryName` | Packed GTS entry name |

Each `AnimationFrameDefinition` contains:

| Property | Meaning |
| --- | --- |
| `Tilesheet` | Registered tilesheet name |
| `RegionName` | Region within that tilesheet |
| `XTile` | Zero-based frame column |
| `YTile` | Zero-based frame row |

The definition deliberately does not persist `FrameSequence`'s current playback index or internal direction state.

---

## Example

A repeating water animation could be written as:

```json
{
  "Key": "world.water",
  "ThrottleTime": 0.18,
  "CycleType": "Repeating",
  "HideTileOnCycleEnd": false,
  "NextCycleKey": "world.water",
  "TilesheetSources": [
    {
      "Tilesheet": "world",
      "Kind": "LooseDefinitionFile",
      "GtsPath": "../tiles/world.gts"
    }
  ],
  "Frames": [
    {
      "Tilesheet": "world",
      "RegionName": "water",
      "XTile": 0,
      "YTile": 0
    },
    {
      "Tilesheet": "world",
      "RegionName": "water",
      "XTile": 1,
      "YTile": 0
    },
    {
      "Tilesheet": "world",
      "RegionName": "water",
      "XTile": 2,
      "YTile": 0
    }
  ]
}
```

The file contains no `$id`, `$ref`, embedded `Tilesheet`, or runtime `Animator` object graph.

---

## Loading and saving

Load a definition without creating a runtime cycle:

```csharp
using Gondwana.Drawing.Animation.GANI;

AnimationDefinition definition =
    AnimationDefinitionSerializer.Load("animations/water.gani");
```

Save a definition:

```csharp
AnimationDefinitionSerializer.Save(
    "animations/water.gani",
    definition);
```

A runtime `Cycle` can also be converted directly:

```csharp
AnimationDefinition definition =
    AnimationDefinitionSerializer.FromCycle(cycle);

AnimationDefinitionSerializer.Save(
    "animations/water.gani",
    cycle);
```

For in-memory workflows:

```csharp
string json =
    AnimationDefinitionSerializer.ToJson(definition);

AnimationDefinition restored =
    AnimationDefinitionSerializer.FromJson(json);
```

These methods use the clean GANI serializer settings rather than `EngineState.JsonSerializerSettings`.

---

## Runtime materialization

Referenced tilesheets must already be registered.

Then:

```csharp
Cycle cycle =
    AnimationDefinitionSerializer.LoadCycle(
        "animations/water.gani");
```

or:

```csharp
Cycle cycle =
    AnimationDefinitionSerializer.ToCycle(definition);
```

Materialization:

1. validates the definition
2. resolves each logical frame reference through `TilesheetRegistry`
3. builds a runtime `FrameSequence`
4. creates/registers the `Cycle`
5. resolves its follow-on cycle

A missing tilesheet, missing region, or out-of-range frame coordinate fails with an `InvalidDataException` rather than silently substituting another frame.

`TilesheetSources` is not consulted by runtime materialization. A stale or unavailable authoring path does not replace the normal registry-based runtime dependency model.

---

## Next-cycle transitions

`Cycle.NextCycle` is represented by logical identity:

```json
"NextCycleKey": "actor.idle"
```

rather than by serializing another `Cycle` object.

That allows definitions such as:

```text
actor.attack.gani
        |
        +---- NextCycleKey: actor.idle
                            |
                            v
                     actor.idle.gani
```

For standalone `ToCycle` / `LoadCycle` calls, a different `NextCycleKey` must already be registered.

EngineState handles this more broadly: it first materializes all selected animation definitions and then resolves next-cycle links in a second pass. That permits circular definition relationships such as:

```text
idle -> blink -> idle
```

If `NextCycleKey` is omitted or empty, GANI uses the normal runtime default and points the cycle back to itself.

---

## Validation

Definitions can be checked without creating runtime objects:

```csharp
IReadOnlyList<string> errors =
    AnimationDefinitionValidator.Validate(definition);
```

Structural validation includes:

- non-empty animation key
- finite, non-negative throttle time
- valid `CycleType`
- at least one frame
- non-empty tilesheet and region names
- non-negative frame coordinates
- null frame entries
- duplicate or malformed tilesheet-source entries

Tilesheet existence and actual region bounds are checked during materialization, because those checks require the referenced runtime tilesheets.

---

## Packed GANI definitions

A GANI document can also be stored in a Gondwana assets file using:

```csharp
AssetTypes.AnimationDefinition
```

Load a packed definition with:

```csharp
AnimationDefinition definition =
    AnimationDefinitionSerializer.Load(
        assetsFile,
        "animations/world.water.gani");
```

Or materialize it directly:

```csharp
Cycle cycle =
    AnimationDefinitionSerializer.LoadCycle(
        assetsFile,
        "animations/world.water.gani");
```

Referenced tilesheets still need to be registered before runtime materialization.

---

## Provenance

`AnimationDefinition.Source` records where the definition came from.

`AnimationDefinitionSourceKind` supports:

| Kind | Meaning |
| --- | --- |
| `None` | No source is known |
| `LooseDefinitionFile` | Loaded from a loose `.gani` file |
| `PackedDefinitionFile` | Loaded from a GANI entry in an `AssetsFile` |
| `Generated` | Generated from a runtime cycle or tooling |

Provenance describes the definition's origin. The animation's runtime identity is still `AnimationDefinition.Key`.

---

## EngineState integration

`EngineStateParts.Cycles` is persisted through GANI.

Inline definitions are the default:

```csharp
Engine.Instance.State.SaveToFile(
    "session.json",
    separateGaniFiles: false);
```

To externalize animation definitions:

```csharp
Engine.Instance.State.SaveToFile(
    "session.json",
    separateGaniFiles: true);
```

For `session.json`, Gondwana writes:

```text
session.json
session.animations/
    actor.walk.gani
    world.water.gani
    ...
```

The main state file stores relative `GaniPath` references when possible.

GANI is independent of the GTS and GSCN storage choices, so each definition family can be embedded or externalized separately.

Because animation definitions depend on tilesheet frames, applying `EngineStateParts.Cycles` also selects the tilesheet dependency chain. When creating a partial state file intended to restore independently, save its required `Tilesheets` (and any required `AssetsFiles`) along with its cycles.

---

## Tooling model

GANI is designed to be an authoring format, not merely an EngineState implementation detail.

A future animation editor can work entirely against `AnimationDefinition`:

```text
Animation
├── Key / timing / cycle type
├── Frames
│   ├── GTS frame reference
│   ├── GTS frame reference
│   └── ...
├── Next cycle
└── Preview
```

The editor can validate and save the definition without needing to serialize a live `Animator`.

The WinForms GANI editor persists loose GTS dependencies in `TilesheetSources` and reloads them automatically when a GANI document is reopened. For older GANI files that do not yet contain source metadata, the editor performs a deliberately narrow migration check: it looks only beside the GANI file for a GTS definition whose logical `Name` matches the frame's `Tilesheet`. If exactly one match exists, it is loaded and the document is marked dirty so the recovered dependency is written on the next save. The editor does not recursively search the filesystem.

Saving or **Save As** rebases loose dependency paths relative to the new GANI location when both files are on the same filesystem root.

That same separation makes GANI suitable for both a standalone WinForms animation tool and, later, an embedded Gondwana Studio editor.

---

## Where to read next

- [[Tile Animation]] — runtime `FrameSequence`, `Cycle`, and `Animator` behavior
- [[.gts Files|GTS-Files]] — tilesheet/frame definitions referenced by GANI
- [[Tilesheets]] — runtime tilesheet model
- [[Game State Files]] — saving animation definitions with EngineState
- [[Serialization and EngineState]] — inline/external definition handling
