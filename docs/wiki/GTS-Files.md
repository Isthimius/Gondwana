A `.gts` file is a **Gondwana Tilesheet definition**. It stores the information Gondwana needs to reconstruct a [`Tilesheet`](Tilesheets): the image source, regions, frame geometry, rendering metadata, and collision metadata.

The image pixels are normally stored somewhere else. A `.gts` file points to a loose image file or to an image entry inside an [`AssetsFile`](Assets-Files).

> A `.gts` file is a recipe, not the cake. It describes how to rebuild a tilesheet; it is not ordinarily an image container.

## On this page

- [The serialization model](#the-serialization-model)
- [What a .gts definition stores](#what-a-gts-definition-stores)
- [A small example](#a-small-example)
- [Loading a loose .gts file](#loading-a-loose-gts-file)
- [Image sources](#image-sources)
- [Loose and packed combinations](#loose-and-packed-combinations)
- [Provenance](#provenance)
- [Saving runtime tilesheets](#saving-runtime-tilesheets)
- [Bitmap and stream-backed tilesheets](#bitmap-and-stream-backed-tilesheets)
- [Collision inheritance](#collision-inheritance)
- [EngineState and .gts files](#enginestate-and-gts-files)
- [Common mistakes](#common-mistakes)

## The serialization model

Gondwana keeps the runtime object and the serialized definition separate:

`GTS JSON` → `TilesheetDefinition` → runtime `Tilesheet`

| Layer | Responsibility |
| --- | --- |
| `.gts` JSON | Portable, human-readable serialized data. |
| `TilesheetDefinition` and related definition types | The in-memory data-transfer model for the file. |
| `TilesheetDefinitionSerializer` | Converts definitions to and from JSON, files, streams, and runtime tilesheets. |
| Runtime `Tilesheet` | Owns the decoded bitmap, regions, frame slices, and live rendering resources. |
| `TilesheetRegistry` | Registers live tilesheets by name. |

This separation is deliberate. Runtime classes do not need to become JSON-shaped, and tools can inspect or edit a definition without first creating every SkiaSharp resource.

## What a .gts definition stores

The root `TilesheetDefinition` contains:

| Property | Purpose |
| --- | --- |
| `Name` | The logical tilesheet name used when the runtime object is created. |
| `Image` | A loose image path or an `AssetsFile` image reference. |
| `Regions` | The source areas, grid geometry, overhang, and collision metadata. |
| `Mask` | Optional RGBA color-key mask and tolerance. |
| `PremultiplyAlpha` | Whether the source image should be premultiplied when loaded. |
| `Source` | Provenance describing where the definition itself came from. |

Each `TilesheetRegionDefinition` stores:

- `Name`
- `Area`
- `TileSize`
- `TilePadding`
- `RegionMargin`
- `Overhang`
- the region-default `CollisionAdjust`
- zero-based frame coordinates and optional frame collision overrides

For an explanation of the geometry properties, see **[[Tilesheets]]**.

### What is not stored

A `.gts` file does not normally contain:

- encoded image bytes
- cached `SKBitmap` or `SKImage` frame slices
- registry or object references
- scene tiles, sprites, or animation state
- runtime disposal state
- the tilesheet `ValueBag`

Those either belong to the referenced image, are rebuilt at load time, or belong to another Gondwana subsystem.

## A small example

The following is an abridged loose definition for a two-frame tilesheet. `System.Drawing` values are represented using their normal JSON string conversion.

```json
{
  "Name": "Terrain",
  "Image": {
    "FilePath": "terrain.png"
  },
  "Regions": [
    {
      "Name": "default",
      "Area": "0, 0, 32, 16",
      "TileSize": "16, 16",
      "TilePadding": {
        "left": 0,
        "top": 0,
        "right": 0,
        "bottom": 0
      },
      "RegionMargin": {
        "left": 0,
        "top": 0,
        "right": 0,
        "bottom": 0
      },
      "Overhang": {
        "left": 0,
        "top": 0,
        "right": 0,
        "bottom": 0
      },
      "CollisionAdjust": {
        "Top": 1,
        "Bottom": 1,
        "Left": 2,
        "Right": 2
      },
      "Frames": [
        {
          "XTile": 0,
          "YTile": 0
        },
        {
          "XTile": 1,
          "YTile": 0,
          "CollisionAdjust": {
            "Top": 3,
            "Bottom": 1,
            "Left": 1,
            "Right": 1
          }
        }
      ]
    }
  ],
  "PremultiplyAlpha": false,
  "Source": {
    "Kind": 0
  }
}
```

The first frame inherits the region collision adjustment because its own `CollisionAdjust` is absent. The second frame has an explicit override.

The serializer writes indented JSON, omits `null` values, includes default values, and ignores unknown properties while reading. Missing frame collections are normalized to empty collections. Together, those rules keep older and additive tool-authored files reasonably tolerant.

## Loading a loose .gts file

For normal game code, load through `TilesheetRegistry`:

```csharp
using Gondwana.Drawing;
using Gondwana.Drawing.Tilesheets;

Tilesheet terrain = TilesheetRegistry.Instance.LoadFromDefinitionFile(
    "Content/Tilesheets/terrain.gts");

Frame grass = terrain[0, 0];
```

This reads the JSON, resolves the image source, rebuilds the regions and frame slices, applies mask or alpha processing, and registers the resulting tilesheet by its definition `Name`.

If a tool only needs the definition model, it can stop before creating the runtime tilesheet:

```csharp
using Gondwana.Drawing.Tilesheets.GTS;

TilesheetDefinition definition = TilesheetDefinitionSerializer.Load(
    "Content/Tilesheets/terrain.gts");

foreach (TilesheetRegionDefinition region in definition.Regions)
{
    Console.WriteLine($"{region.Name}: {region.TileSize}");
}
```

## Image sources

`TilesheetImageDefinition` supports two mutually-exclusive forms.

### Loose image file

```json
"Image": {
  "FilePath": "images/terrain.png"
}
```

A relative `FilePath` is resolved from the definition's base directory. For a loose `.gts` file, that is the directory containing the `.gts` file.

### Image inside an AssetsFile

```json
"Image": {
  "AssetsFilePath": "../assets/game.gaf",
  "AssetEntryName": "images/terrain.png"
}
```

`AssetsFilePath` locates the `.gaf`; `AssetEntryName` locates the image within it. A relative assets-file path is resolved from the same base directory as a relative loose image path.

When the `.gts` definition is itself packed in the same `AssetsFile` as its image, the image can use only the entry name:

```json
"Image": {
  "AssetEntryName": "images/terrain.png"
}
```

The containing `AssetsFile` becomes the default assets file during loading.

These source forms are intentionally strict:

- `FilePath` cannot be combined with either asset property.
- `AssetsFilePath` requires `AssetEntryName`.
- `AssetEntryName` without `AssetsFilePath` requires a default `AssetsFile`, which is supplied when loading a packed definition.

Ambiguous or incomplete definitions throw instead of quietly choosing one source. Quiet precedence rules are where configuration bugs go to breed.

## Authoring packed image references

The WinForms GTS editor can author the loose-definition/packed-image combination
directly. Its project-source browser shows `.gaf` and `.zip` files alongside loose
GTS/image files. Expanding a package lists entries whose `AssetTypes` value is
`Image`.

Selecting a packed image for a GTS sets:

```json
"Image": {
  "AssetsFilePath": "../assets/game.gaf",
  "AssetEntryName": "images/terrain.png"
}
```

The editor previews the image directly from the `AssetsFile` stream and does not
extract a temporary loose image. When the GTS is saved, the package path is rebased
relative to the GTS destination where possible, using the same preservation rules as
loose image paths.

The standalone editor may request a password while browsing an encrypted package.
That password belongs to the tooling session; it is not serialized into the GTS
definition.

## Loose and packed combinations

The definition's storage and the image's storage are independent. Gondwana supports all four basic combinations:

| Definition location | Image location | Image fields | Relative paths are based on |
| --- | --- | --- | --- |
| Loose `.gts` | Loose image | `FilePath` | The `.gts` directory |
| Loose `.gts` | Packed image | `AssetsFilePath` + `AssetEntryName` | The `.gts` directory |
| Packed `.gts` | Loose image | `FilePath` | The containing `.gaf` directory |
| Packed `.gts` | Packed image | `AssetEntryName`, or `AssetsFilePath` + `AssetEntryName` | The containing `.gaf` directory |

For the packed/packed case:

- use only `AssetEntryName` when the image is in the same `AssetsFile` as the definition;
- include `AssetsFilePath` when the image is in a different `AssetsFile`.

### Loading a packed definition

```csharp
using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets;

AssetsFile gameAssets = AssetsFile.LoadOrCreate("Content/game.gaf");

Tilesheet terrain = TilesheetRegistry.Instance.LoadFromDefinitionAsset(
    gameAssets,
    "tilesheets/terrain.gts");
```

The GTS entry must be stored with `AssetTypes.TilesheetDefinition`. The logical runtime name still comes from `TilesheetDefinition.Name`; the packed entry name is only its address inside the archive.

## Provenance

`TilesheetDefinition.Source` records **where the definition came from**. It does not select the image source; that remains the job of `TilesheetDefinition.Image`.

| `TilesheetDefinitionSourceKind` | Meaning | Relevant fields |
| --- | --- | --- |
| `None` | No origin is known yet. | None |
| `LooseDefinitionFile` | The definition came from a loose `.gts` file. | `GtsFilePath` |
| `PackedDefinitionFile` | The definition came from a `.gts` entry in an `AssetsFile`. | `AssetsFilePath`, `AssetEntryName` |
| `Generated` | The definition was generated in memory, commonly from a runtime tilesheet or tooling. | None |

The serializer follows these rules:

- `Load(filePath)` stamps `LooseDefinitionFile` when the JSON currently has `Source.None`.
- Loading a definition entry through `LoadFromDefinitionAsset` stamps `PackedDefinitionFile` with the `.gaf` path and entry name.
- `FromTilesheet(...)` creates a definition marked `Generated`.
- `Load(Stream)` cannot infer a physical origin, so it preserves the serialized source and otherwise leaves it as `None`.
- Saving a `TilesheetDefinition` whose source is `None` writes a loose-file source into the saved clone without mutating the caller's in-memory object.
- An already-explicit source is preserved when saving a definition.

This makes provenance useful to editors and tooling: they can distinguish “loaded from here” from “created in memory,” even when both definitions reference the same image.

> Provenance is descriptive metadata. Path resolution is still determined by the load operation's base directory and the fields under `Image`.

## Saving runtime tilesheets

The simplest runtime-to-GTS save is:

```csharp
using Gondwana.Drawing.Tilesheets.GTS;

TilesheetDefinitionSerializer.Save(
    "Content/Tilesheets/terrain.gts",
    terrain);
```

For a file-backed tilesheet, this writes the definition and normally records the image path relative to the destination `.gts` file. It does not copy or duplicate the existing source image.

For an `AssetsFile`-backed tilesheet, the definition records the assets-file path and image entry name.

Path behavior is controlled by the optional argument:

```csharp
TilesheetDefinitionSerializer.Save(
    "Content/Tilesheets/terrain.gts",
    terrain,
    makePathsRelative: false);
```

`makePathsRelative` defaults to `true` for `Save(filePath, tilesheet)`. Stored path separators are normalized to `/`, which keeps generated JSON stable across Windows and Unix-like systems.

### Definition and JSON conversion

You can also convert without immediately writing a file:

```csharp
TilesheetDefinition definition =
    TilesheetDefinitionSerializer.FromTilesheet(terrain);

string json = TilesheetDefinitionSerializer.ToJson(definition);
```

Or serialize a runtime tilesheet relative to a chosen directory:

```csharp
string json = TilesheetDefinitionSerializer.ToJson(
    terrain,
    baseDirectory: "Content/Tilesheets",
    makePathsRelative: true);
```

Unlike `Save(filePath, tilesheet)`, these conversion methods have no destination filename from which to choose a companion image path. A runtime-only bitmap or stream must therefore be persisted first.

## Bitmap and stream-backed tilesheets

A tilesheet loaded from an `SKBitmap` or image stream initially has decoded pixels but no persistent image address:

```csharp
Tilesheet generated = TilesheetRegistry.Instance.LoadFromBitmap(
    "generated-terrain",
    bitmap);
```

There are two ways to make it serializable.

### Let GTS saving persist it automatically

```csharp
TilesheetDefinitionSerializer.Save(
    "Content/Tilesheets/generated-terrain.gts",
    generated);
```

Because `generated` has neither `ImageFilePath` nor `AssetIdentifier`, `Save` automatically creates:

```text
Content/Tilesheets/generated-terrain.gts
Content/Tilesheets/generated-terrain.png
```

The PNG uses the same base filename, the GTS records `generated-terrain.png`, and the runtime tilesheet is promoted to file-backed by setting its `ImageFilePath`.

### Choose the image destination explicitly

```csharp
generated.PersistImageToFile(
    "Content/Images/generated-terrain.png");

TilesheetDefinition definition =
    TilesheetDefinitionSerializer.FromTilesheet(generated);
```

`PersistImageToFile` defaults to PNG at quality 100, creates missing directories, records the full image path on the tilesheet, and clears any previous `AssetIdentifier`.

If masking or premultiplication has transformed the runtime bitmap, Gondwana persists the retained **original source bitmap**. The GTS mask or premultiplication metadata is then reapplied once during loading. Saving the already-transformed bitmap would apply the operation twice and slowly turn correctness into modern art.

## Collision inheritance

GTS preserves the difference between these two states:

1. a frame inherits its region's `CollisionAdjust`;
2. a frame has an explicit override whose current value happens to equal the region default.

The distinction is represented by nullable frame metadata:

```csharp
public CollisionAdjust? CollisionAdjust { get; set; }
```

| Frame JSON | Meaning after load |
| --- | --- |
| `CollisionAdjust` absent | Inherit `TilesheetRegionDefinition.CollisionAdjust`. |
| `CollisionAdjust` present | Create an explicit frame override. |

The runtime serializer writes every valid frame coordinate. Inherited frames omit the nullable adjustment; overridden frames include it. Therefore, changing the region default after loading updates only inheriting frames.

Older `.gts` files without `Frames` or collision metadata remain valid. Missing collections are treated as empty, and missing collision values use their zero-value defaults.

## EngineState and .gts files

`EngineState` can persist tilesheet definitions in either of two ways:

- inline inside the engine-state JSON, which is the default;
- as separate `.gts` files when `separateGtsFiles: true`.

```csharp
using Gondwana;

Engine.Instance.State.SaveToFile(
    "Saves/session.json",
    separateGtsFiles: true);
```

Separate definitions are written beneath a sibling directory named after the state file:

```text
Saves/session.json
Saves/session.tilesheets/terrain.gts
Saves/session.tilesheets/actors.gts
```

The engine-state file stores relative references to those definitions when possible. Each standalone file is written with `TilesheetDefinitionSerializer`, not the general EngineState JSON settings, so the `.gts` remains clean and independently usable by the engine, tools, source control, and editors.

## Common mistakes

| Symptom | Likely cause |
| --- | --- |
| The `.gts` exists but the tilesheet will not load | The referenced loose image, `.gaf`, or asset entry is missing. |
| A relative image path resolves from the process directory | A definition was loaded from a stream or object without supplying the intended base directory. |
| The image source is reported as ambiguous | `FilePath` was combined with asset source properties. Choose one form. |
| An entry-only image reference fails | The definition was not loaded from a containing `AssetsFile`, so no default assets file exists. |
| `FromTilesheet` throws for a bitmap-backed sheet | Use `Save(filePath, tilesheet)` for automatic sibling persistence, or call `PersistImageToFile` first. |
| A frame stops following region collision changes | Its GTS frame entry contains an explicit `CollisionAdjust`; clear the runtime override before saving if inheritance is intended. |
| Moving a loose `.gts` breaks its image path | Move its relative loose image with it, or update the image reference. |
| The runtime name differs from the packed entry name | Expected: `TilesheetDefinition.Name` is the logical name; the asset entry is only storage addressing. |
| Paths look different on Windows after serialization | Generated GTS paths deliberately use `/` for cross-platform stability. |

## In short

1. A `.gts` file stores tilesheet metadata, not image bytes.
2. The definition and its image can each be loose or packed independently.
3. Relative paths are resolved from the loose `.gts` directory or the containing `.gaf` directory.
4. `Source` records definition provenance; `Image` identifies the bitmap source.
5. `Save(filePath, tilesheet)` uses relative paths by default and automatically persists runtime-only images beside the `.gts`.
6. Region collision defaults and explicit per-frame overrides round-trip as distinct states.
7. Load through `TilesheetRegistry` when you want a registered runtime tilesheet; load through `TilesheetDefinitionSerializer` when a tool only needs the definition.

That is the complete practical model: the `.gts` says what the tilesheet is, provenance says where that definition came from, and the image reference says where its pixels live.
## Inter-tile spacing and trailing margins

Use `TilePadding.Right` and `TilePadding.Bottom` for spacing between atlas cells,
with leading padding zero. Grid sizing counts padding in every stride. To avoid
requiring a final gap after the last tile, set trailing `RegionMargin` to the
source's trailing image margin minus that axis's spacing. For symmetric source
margin M and spacing S, leading margins are M and trailing margins are M - S.

Leading margins must be non-negative. Right and Bottom may be negative only down
to `-TilePadding.Right` and `-TilePadding.Bottom`, respectively. This compensates
for a final padding interval without permitting frame pixels beyond the region.
For example, three 16-pixel tiles separated by 2-pixel gaps fit a 52-pixel region
using right padding 2 and right margin -2. Runtime and Studio validation enforce
these bounds; there is no separate tile-separation property.

