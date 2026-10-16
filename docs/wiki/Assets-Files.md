Gondwana asset files provide a simple way to bundle game resources into a single container instead of distributing every image, sound, definition, and other resource as a separate loose file.

## Multi-definition documents

A typed GTS, GANI, GSCN, GSND, or GSPR entry stores one definition **document**. Its `Definitions` array may contain multiple logical members. The archive entry name identifies the physical document, and Name (GTS), Key (GANI/GSND/GSPR), or ID (GSCN) identifies the member. Existing singleton entries remain readable. `AssetsFile` storage, adding, and extraction semantics are unchanged; those low-level operations do not relocate references. The CLI semantic packing workflow described below performs reference transformation before adding entries.


An `AssetsFile` is intentionally not a complicated virtual filesystem or content database.

At its core, it is a collection of **typed, named binary assets**:

```text
AssetsFile
    |
    +-- Image / player.png
    +-- Image / terrain/grass.png
    +-- Audio / music/title.mp3
    +-- Svg / ui/logo.svg
    +-- TilesheetDefinition / terrain.gts
```

The engine can then retrieve those resources as streams and hand them to the subsystem that understands their actual format.

---

## Why asset files exist

A game can work perfectly well with loose files:

```text
assets/
    images/
    audio/
    maps/
    ...
```

During development, that is often convenient.

For distribution, however, it can be useful to package those resources together:

```text
game.gaf
```

An asset file gives Gondwana:

- a single container for many resources
- consistent type-based lookup
- optional password protection and encryption
- stream-based access to packaged data
- a portable resource source that engine subsystems can consume

Asset files are therefore primarily a **packaging and resource-loading mechanism**.

They do not change what an image, sound, SVG, or tilesheet definition actually is.

---

## Asset identity

Every entry in an `AssetsFile` has two important pieces of identity:

```text
AssetType + AssetName
```

For example:

```text
Image / player.png
Audio / explosion.wav
Svg / ui/logo.svg
```

The asset type tells Gondwana what general category the resource belongs to.

The name identifies the resource within that category.

This means that these are distinct assets:

```text
Image / logo.png
Audio / logo.png
```

even though they share the same name.

Asset-name comparisons are case-insensitive.

---

## Asset types

The current `AssetTypes` categories are:

| Type | Purpose |
|---|---|
| `Image` | Raster image resources |
| `Audio` | Audio resources |
| `Video` | Native desktop playback through optional `Gondwana.Video` |
| `Cursor` | Cursor resources; currently not supported |
| `Font` | Font resources |
| `Misc` | General miscellaneous data; currently not directly interpreted by the engine |
| `Svg` | Scalable vector graphics |
| `TilesheetDefinition` | Gondwana `.gts` tilesheet definitions |
| `SceneDefinition` | Gondwana `.gscn` scene definitions |
| `AnimationDefinition` | Gondwana `.gani` animation definitions |
| `AudioDefinition` | Gondwana `.gsnd` sound collections |
| `SpriteDefinition` | Gondwana `.gspr` sprite collections (numeric value 11) |

The type is primarily classification metadata.

An `AssetsFile` itself does not decode an image or play a sound. It stores the bytes and identifies what kind of asset they represent.

### Playing a GAF video on desktop

The optional `Gondwana.Video` package connects video assets to `DirectVideo` without
extracting a permanent file:

```csharp
var video = new Gondwana.Drawing.Direct.DirectVideo(
    () => new Gondwana.Video.VlcVideoPlayer(),
    Gondwana.Video.VideoSource.FromAsset(assets, "intro.mp4"),
    renderSurfaceHost, view, new System.Drawing.Rectangle(0, 0, 960, 540))
{
    Stretch = Gondwana.Drawing.Direct.StretchMode.Uniform
};
```

The source obtains a fresh `AssetTypes.Video` stream when opened, and the player
owns it until replacement or disposal. Current GAF streams are backed by asset
bytes in memory; no temporary extraction is involved. Keep the archive alive until
Open/construction completes. Dispose the drawing when finished. Bounds are required;
natural video dimensions and metadata arrive asynchronously. Check the metadata
snapshot's readiness before interpreting audio presence or duration.

This backend requires native desktop LibVLC and does not support Blazor. See the
[Video deployment and smoke guide](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Video/README.md)
for Windows/macOS/Linux dependencies, ownership and engine-thread requirements.

The appropriate Gondwana subsystem handles those bytes afterward.

---

## Asset files are stream-oriented

Consumers do not need to know how an asset is physically stored inside the bundle.

They ask the `AssetsFile` for the resource:

```csharp
using var stream = assets.Get(AssetTypes.Image, "player.png");
```

and receive a readable `Stream`.

That stream can then be passed to whatever understands the resource.

Conceptually:

```text
AssetsFile
    |
    | type + name
    v
Asset bytes
    |
    v
Stream
    |
    +-> image decoder
    +-> audio system
    +-> SVG loader
    +-> GTS serializer
    +-> application code
```

This keeps the asset container independent from the individual resource implementations.

---

## Loading an asset file

Use `AssetsFile.LoadOrCreate` to open an existing bundle or create a new one:

```csharp
var assets = AssetsFile.LoadOrCreate("game.gaf");
```

If the file exists, Gondwana loads its entries.

If it does not exist, the returned `AssetsFile` begins as an empty bundle that can be populated and saved.

For example:

```csharp
var assets = AssetsFile.LoadOrCreate("game.gaf");

assets.Add(
    AssetTypes.Image,
    "Content/player.png",
    "player.png");

assets.Add(
    AssetTypes.Audio,
    "Content/explosion.wav",
    "explosion.wav");

assets.Save();
```

The `.gaf` extension is a Gondwana convention rather than a requirement of the underlying `AssetsFile` implementation. Tooling also accepts names such as `.assets`.

---

## What is actually inside the file?

Gondwana asset files are ZIP-backed containers.

Internally, entries are stored using their asset type and asset name.

Conceptually:

```text
Image_player.png
Audio_explosion.wav
Svg_ui/logo.svg
```

This is an implementation detail in normal engine use. Applications should access assets through `AssetsFile` rather than depending on the physical ZIP entry naming scheme.

Using a familiar container format keeps the asset system straightforward while still allowing Gondwana to layer its own type and lookup semantics over it.

---

## In-memory behavior

When an existing asset file is loaded, Gondwana reads its entries into memory.

The loaded bytes become the `AssetsFile` instance's working source of truth.

The ZIP file itself does not remain open.

Conceptually:

```text
game.gaf
    |
    | load
    v
ZIP entries
    |
    | buffer
    v
AssetsFile in memory
```

From that point, operations such as:

- retrieving assets
- adding assets
- replacing assets
- removing assets

operate against the in-memory collection.

Calling:

```csharp
assets.Save();
```

writes the current collection back to the asset file.

This design avoids keeping persistent file handles open and makes asset retrieval independent of an active ZIP stream.

It also means that the contents of a loaded bundle occupy memory while that `AssetsFile` remains alive.

---

## Adding assets

Assets can be added from files:

```csharp
assets.Add(
    AssetTypes.Image,
    "Content/player.png",
    "player.png");
```

or directly from streams:

```csharp
using var stream = File.OpenRead("player.png");

assets.Add(
    AssetTypes.Image,
    "player.png",
    stream);
```

The supplied data is read immediately into the asset file's in-memory collection.

If another entry already exists with the same asset type and name, the new data replaces it.

---

## Retrieving assets

Assets are normally retrieved using their type and name:

```csharp
using var stream = assets.Get(
    AssetTypes.Audio,
    "explosion.wav");
```

The indexer provides the same basic lookup:

```csharp
using var stream = assets[
    AssetTypes.Audio,
    "explosion.wav"];
```

Lookup first attempts an exact asset-name match.

Gondwana can also fall back to matching by the filename without its extension.

For example:

```csharp
assets.Get(AssetTypes.Image, "player");
```

can resolve:

```text
player.png
```

when an appropriate matching image entry exists.

For predictable asset sets, using the complete stored asset name is still preferable—especially when multiple files could share the same base name.

---

## Paths can be asset names

Asset names are not limited to simple filenames.

The Gondwana CLI, for example, preserves paths relative to the directory being packed:

```text
images/player.png
images/enemies/slime.png
audio/music/title.mp3
audio/sfx/explosion.wav
```

This allows a bundle to retain useful logical organization without requiring those resources to remain loose files on disk.

The path is part of the asset's name.

It is not a physical directory that Gondwana needs to recreate in order to read the resource.

---

## AssetsFileIdentifier

Some Gondwana objects need to remember where their underlying resource came from.

`AssetsFileIdentifier` represents that relationship.

It records:

```text
AssetsFile
    +
AssetType
    +
AssetName
```

Conceptually:

```text
Tilesheet
    |
    v
AssetsFileIdentifier
    |
    +-- game.gaf
    +-- Image
    +-- terrain.png
```

The identifier can then retrieve the asset data from the associated bundle when needed.

For example, a `Tilesheet` loaded from an asset file retains an `AssetsFileIdentifier` pointing back to its image asset.

This lets serializable engine objects describe their resource source without embedding their own duplicate copy of the asset data.

---

## Asset files and resource managers

An `AssetsFile` stores resources.

Resource managers turn those stored resources into usable engine objects.

For example, the SVG resource manager can enumerate an asset file and load its `Svg` entries:

```text
AssetsFile
    |
    | Svg entries
    v
SvgResourceManager
    |
    v
SvgResource
```

Audio follows the same basic pattern:

Configure a byte-capable audio backend before loading packed audio. The Windows
`Gondwana.Audio.NAudio` backend supports these entries; `Gondwana.Audio.Browser`
currently loads URI-addressable browser assets with `LoadFromUri` instead.

```text
AssetsFile
    |
    | Audio entries
    v
AudioResourceManager
    |
    v
AudioResource
```

Tilesheets can likewise obtain their image data directly from an asset file.

This distinction is useful:

```text
AssetsFile
    = storage

Resource manager
    = loading and lifetime management

Resource object
    = usable engine representation
```

The asset system does not attempt to replace those higher-level systems.

---

## Definition origins and EngineState

A GAF can contain definition entries as well as their media. EngineState can retain
an existing definition as `AssetsFilePath` + `AssetEntryName` using
`DefinitionPersistence.PreserveSource`. The entry name is exact and its category must
match the definition format. The definition origin is distinct from its image/audio
origin. EngineState never packages generated definitions into a GAF.

`AssetsFile.SourcePath` returns `FilePath` for file-backed archives. For streams, use
`AssetsFile.Load(stream, password: null, register: true, sourcePath: "assets/game.gaf")`
to supply a reconstructible origin. The stream is still buffered immediately; its
identity neither opens that path nor enables saving the stream archive there.
`Load(stream)` without an identity remains valid and cannot yield a durable packed
reference. All five definition loaders follow that rule.

EngineState stores inert archive records (`FilePath`, `Password`, `UseEncryption`),
not live archives or entry bytes. It makes GAF paths relative to the state directory
where possible and resolves them from there during application. Registered stream
archives with matching identities can be reused during merge; replacement loads
reopen filesystem paths. See [Serialization and EngineState](Serialization-and-EngineState#stream-archives)
for stream fallback and host responsibilities.

---

## Multiple asset files

Gondwana does not require an application to use one enormous asset bundle.

Multiple `AssetsFile` instances can exist at the same time.

For example:

```text
core.gaf
ui.gaf
level1.gaf
music.gaf
```

Each is an independent asset collection.

Loaded `AssetsFile` instances are registered globally through:

```csharp
AssetsFile.AllAssetsFiles
```

and disposing an asset file removes it from that collection.

This makes it possible to organize assets according to the needs of the application rather than imposing one universal package layout.

---

## EngineState integration

Asset files are also part of Gondwana's serializable `EngineState`.

This is important because other engine state can depend on them.

For example:

```text
EngineState
    |
    +-- AssetsFiles
    |
    +-- Tilesheets ----+
    |                  |
    +-- Audio ---------+--> may depend on AssetsFiles
```

When loading selected engine-state components, Gondwana automatically includes asset files for definition categories, including packed GTS, GANI, GSCN, GSND, and GSPR references.

The serialized engine state describes the registered asset files and allows Gondwana to reopen those bundles during restoration.

The contents of the `.gaf` files themselves are not copied into the engine-state JSON.

The asset files therefore remain external resources and must still be available when the engine state is restored.

---

## Encryption and password protection

Asset files can optionally be password-protected:

```csharp
var assets = AssetsFile.LoadOrCreate(
    "game.gaf",
    password: "example-password");
```

AES-256 encryption can also be requested:

```csharp
var assets = AssetsFile.LoadOrCreate(
    "game.gaf",
    password: "example-password",
    encrypt: true);
```

When encryption is enabled, saved ZIP entries are written using AES-256 encryption.

This can be useful for packaged game resources, but it should be treated as **asset-package protection**, not as a general-purpose secrets-management system.

---

## Tooling

Gondwana includes tooling for working with asset bundles.

The CLI can package a directory into an asset file and infer asset types from file extensions.

Conceptually:

```text
Content/
    images/
    audio/
    definitions/
        |
        | gondwana assets pack
        v
     game.gaf
```

The asset tooling also supports operations such as inspecting and extracting bundle contents.

`Gondwana.Assets.WinForms` provides a graphical development tool for creating and managing asset files.

These tools operate on the same `AssetsFile` format used by the engine at runtime.

---

## Loose files or asset files?

Asset files are optional.

Gondwana systems generally support ordinary files and streams as well.

That means a project can choose whichever workflow makes sense:

```text
Development
    |
    +-> loose files

Distribution
    |
    +-> packaged AssetsFile
```

or even mix the two.

For example, a game might keep configuration files loose while packaging graphics and audio into bundles.

The important point is that an `AssetsFile` is another **source of resource streams**, not a completely separate resource model.

---

## Mental model

The simplest way to think about Gondwana asset files is:

```text
Files
    |
    | package
    v
AssetsFile
    |
    | type + name
    v
Stream
    |
    | decode / interpret
    v
Engine resource
```

Or, more simply:

> An `AssetsFile` knows **where the bytes are** and **what category they belong to**.

The rest of Gondwana knows **what those bytes mean**.

That separation keeps asset packaging independent from rendering, audio, tilesheets, and other resource systems.

---

## Where to read next

- [`Gondwana/Assets/AssetsFile.cs`](https://isthimius.github.io/Gondwana/api/latest/AssetsFile_8cs_source.html)
- [`Gondwana/Assets/AssetsFileEntry.cs`](https://isthimius.github.io/Gondwana/api/latest/AssetsFileEntry_8cs_source.html)
- [`Gondwana/Assets/AssetsFileIdentifier.cs`](https://isthimius.github.io/Gondwana/api/latest/AssetsFileIdentifier_8cs_source.html)
- [`Gondwana/Assets/AssetTypes.cs`](https://isthimius.github.io/Gondwana/api/latest/AssetTypes_8cs_source.html)
- [`Gondwana/Audio/AudioResourceManager.cs`](https://isthimius.github.io/Gondwana/api/latest/AudioResourceManager_8cs_source.html)
- [`Gondwana/Drawing/SvgResourceManager.cs`](https://isthimius.github.io/Gondwana/api/latest/SvgResourceManager_8cs_source.html)
- [`Tooling/Gondwana.Cli/Commands/Assets/`](https://github.com/Isthimius/Gondwana/tree/master/Tooling/Gondwana.Cli/Commands/Assets)
- [`Tooling/Gondwana.Assets.WinForms/`](https://github.com/Isthimius/Gondwana/tree/master/Tooling/Gondwana.Assets.WinForms)

### Semantic packing

`gondwana assets pack` (also `gondwana pack`) automatically recognizes `.gts`, `.gani`, `.gscn`, `.gspr`, and `.gsnd` as their corresponding definition asset types. It reads every member through the Core serializers and writes one canonical document per input file. It does not split members or change their logical identities.

Before writing, packing resolves image/audio resources and GTS/GANI/GSCN dependencies across the complete inventory. Explicit authoring sources select a physical document and logical member. When source metadata is absent, a unique matching logical member in the final inventory supplies it; ambiguous matches fail. Physical references are transformed through `DefinitionReferenceRelocator`. `NextCycleKey` remains a logical reference; cycle relationships are allowed. URI audio resources remain unchanged.

By default dependencies must be in the planned package. Use `--allow-external` only when deliberately retaining existing loose files or external GAF dependencies. Explicit external targets must exist, and referenced typed entries and logical members are checked. This option does not search for unspecified external dependencies or recursively package external documents. External GAFs use the supplied password. External paths are rebased relative to the final output where possible. Binary media are not decoded during preflight.

`--append` analyzes the final inventory, including existing entries. An incoming file replaces its entire matching typed entry, never appends members to an existing document. Omitted loose dependencies under the source root can resolve to matching existing typed entries. Retained documents are checked against replacements, so removing a referenced member fails. Unaffected entry contents are preserved; existing AES encryption is retained.

Packing validates before writing, builds a temporary sibling archive, and replaces the destination only after successful writing and validation. Failure or cancellation leaves an existing output intact. The output itself is excluded when inside the source directory. Without `--append`, a nonempty invocation replaces the package completely; an empty input leaves it unchanged. Input files are not modified. `assets unpack` performs the corresponding semantic export described below.

### Semantic unpack and selective export

`gondwana assets unpack game.gaf Extracted` exports native definition documents with references transformed for their new locations. `assets extract` is an alias for the same operation. Ordinary image, audio, and other leaf entries retain their original bytes. Every member of a native document stays in the file named by its physical entry, in the original order; logical names and keys do not become filenames.

```sh
# Export everything, converting included dependencies to relative loose paths
gondwana assets unpack game.gaf Extracted
# Export tilesheets; images remain referenced in game.gaf
gondwana assets unpack game.gaf ExportedSheets --type TilesheetDefinition
# Export one exact typed document, including all its logical members
gondwana assets extract game.gaf ExportedScene --type SceneDefinition --entry Scenes/level.gscn
# Permit replacement of existing output files
gondwana assets unpack game.gaf Extracted --overwrite --password secret
```

`--entry` matches an exact GAF entry name, case-insensitively; it does not match basenames. If the name occurs in several types, supply `--type`. No match is an error. A type filter without matches exports nothing.

When both a definition and its dependency are selected, the dependency becomes a loose reference relative to the exported document. For example, `Tilesheets/terrain.gts` can reference `../Images/terrain.png`. Dependencies not selected remain packed in the original archive, with its path rebased relative to the new document. This includes GTS images whose source archive was previously implicit. References to external loose files or other GAFs retain their original targets and are rebased. URI audio is unchanged; audio format hints are retained, including for extensionless exported files.

The archive is validated and the selected documents' immediate dependency targets and logical members are checked before writing. External archives use the supplied password. Export does not download URIs or recursively export/audit external dependency graphs. Core applications must still load required archives and register dependent tilesheets, animation cycles, and scenes in their usual order; exporting does not change runtime initialization requirements.

All destinations are checked before staging. Unsafe paths, symlink/junction ancestry, case-insensitive destination collisions, file/directory conflicts, and attempts to overwrite the source GAF are rejected. Existing output requires `--overwrite`. Prepared files are staged, and replaced files are backed up during the multi-file commit. On an ordinary failure or cancellation, completed writes are rolled back and transaction files are cleaned up. If rollback fails, the error identifies retained recovery files.

Multi-file export is **not globally atomic**: a crash, power loss, disk failure, or concurrent filesystem modification can interrupt commit or recovery. Do not modify the output tree concurrently. Archive contents are buffered during preflight, and binary media are not decoded. A complete self-contained export works without the original GAF; a partial export must retain any archives/resources still referenced. Export is a copy operation and never rewrites the source GAF or project-wide inbound references.

## Semantic GAF Add / Import

The standalone Assets editor and Studio use the same `AssetEditorControl` workflow:

1. Select one or more loose files in **Add / Import**.
2. Review the proposed type and destination entry name for each file. Common media types are inferred individually. GTS, GANI, GSCN, GSPR, and GSND types are fixed to their native definition types; unknown files default to Misc and can be corrected.
3. Choose **Import dependencies** to copy the known recursive dependency closure, or **Keep external references** to preserve dependencies outside the destination GAF. Explicitly selected files are still imported in either mode.
4. Review additions, reused entries, physical reference transformations, and remaining external dependencies. **Import** applies the prepared plan; **Cancel** leaves the archive and dirty state unchanged.
5. **Save** persists the changes. Import does not save automatically, move/delete source files, or change the borrowed archive instance or its password/encryption settings.

Each native file remains one document containing all its logical members, in order. Member identities, animation assignments, scene relationships, and URI audio are preserved. References use the current destination GAF path; externally retained paths are rebased relative to its directory. Known dependencies in subdirectories retain paths relative to the first selected file's directory. Dependencies outside that directory use `dependencies/<AssetType>/<filename>`; resources copied from another GAF use `dependencies/<archive-name>/<entry-name>`. All names are shown in the preview.

Existing entries are resolved against the current in-memory archive, including unsaved edits. Exact typed packed references can reuse existing members; logical-only references require a unique member among selected/current documents. A leaf resource at the planned exact typed entry is reused only when its bytes are identical. Reused native documents are checked recursively against their existing references; any external dependencies they already have remain external and appear in the preview, even with Import dependencies selected. Basename matches alone never establish equivalence. Missing/ambiguous sources and conflicting native documents fail clearly. Existing entries are never replaced by Add / Import; change the destination name or supply an explicit source. This workflow does not certify the safety of the separate existing **Replace** command.

Only explicit physical dependencies are followed recursively; the importer does not scan neighboring folders or arbitrary project code to guess missing source metadata. Include the relevant files in the selection or author explicit sources. Referenced external archives use the destination password when opening is required; archives requiring a different password are not supported by this workflow. URI resources are not downloaded.

The UI-independent `Gondwana.Tooling.Assets` library reuses the CLI dependency analysis, document serializers, and Phase 1 relocation. It buffers a snapshot and all prepared additions before applying them. A stale preview is rejected if archive contents changed. Cancellation or an ordinary multi-entry write failure removes attempted additions, preserving unrelated edits. Apply on the editor thread; this does not provide concurrent-writer synchronization or recovery from process termination/out-of-memory failure.

Export, Rename, Delete, and Save As retain their existing behavior. Whole-GAF Save As relocation and inbound-reference updates are not implemented here: use the intended final archive path for semantic import. Copying a GAF to another path with the existing Save As operation can still leave references to its former location; that separate limitation is not addressed by Add / Import.
