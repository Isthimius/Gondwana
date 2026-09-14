Gondwana asset files provide a simple way to bundle game resources into a single container instead of distributing every image, sound, definition, and other resource as a separate loose file.

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
| `Video` | Video resources handled through platform-specific media support |
| `Cursor` | Cursor resources; currently not supported |
| `Font` | Font resources |
| `Misc` | General miscellaneous data; currently not directly interpreted by the engine |
| `Svg` | Scalable vector graphics |
| `TilesheetDefinition` | Gondwana `.gts` tilesheet definitions |

The type is primarily classification metadata.

An `AssetsFile` itself does not decode an image or play a sound. It stores the bytes and identifies what kind of asset they represent.

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

When loading selected engine-state components, Gondwana automatically includes asset files when required by tilesheets or audio.

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

- `Gondwana/Assets/AssetsFile.cs`
- `Gondwana/Assets/AssetsFileEntry.cs`
- `Gondwana/Assets/AssetsFileIdentifier.cs`
- `Gondwana/Assets/AssetTypes.cs`
- `Gondwana/Audio/AudioResourceManager.cs`
- `Gondwana/Drawing/SvgResourceManager.cs`
- `Tooling/Gondwana.Cli/Commands/Assets/`
- `Tooling/Gondwana.Assets.WinForms/`