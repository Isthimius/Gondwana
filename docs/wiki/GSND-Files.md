# GSND Files

## Definition documents and logical members

One physical `.gsnd` file contains a document object with a `Definitions` array.
Every member has a unique, non-empty, case-sensitive `Key`. The document location
(loose path, or GAF path plus typed entry name) is independent of that logical identity.

```json
{
  "Definitions": [
    { "Key": "first" },
    { "Key": "second" }
  ]
}
```

This illustrates the envelope and identities; supply the normal format-specific fields
shown below for complete definitions. The same envelope is stored as the bytes of a
single `AssetTypes.AudioDefinition` GAF entry. GAF remains a typed binary container;
loading or saving a document does not create entries for individual members.

```csharp
var selected = AudioDefinitionSerializer.Load("content.gsnd", "second");
var definitions = AudioDefinitionSerializer.LoadAll("content.gsnd");
var document = AudioDefinitionSerializer.LoadDocument("content.gsnd");
AudioDefinitionSerializer.SaveDocument("copy.gsnd", document);
AudioDefinitionSerializer.SaveAll("copy.gsnd", definitions);

var packed = AudioDefinitionSerializer.Load(assetsFile, "content.gsnd", "second");
var packedMembers = AudioDefinitionSerializer.LoadAll(assetsFile, "content.gsnd");
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

Legacy roots without `Key` infer it from the filename or packed entry basename.
Anonymous legacy JSON uses `audio`; canonical document members must explicitly provide a key.

Use `AudioDefinitionSerializer.LoadAllIntoManager` to materialize every member.

Load-all operations roll back newly created runtime content if a member fails.
For physical packaging, export, move, and deletion workflows, see [Assets Files](Assets-Files). These operations preserve logical member identities; [Reference Relocation](Reference-Relocation) describes scope and safety guarantees.

The shared [Import definition workflow](Studio-and-Desktop-Tools#import-into-an-open-document) appends independent members to an open document without changing the source file.

`.gsnd` is Gondwana's standalone audio-definition format.

A GSND file describes **which audio resources exist, where their media comes from,
and their portable playback settings**. It does not contain a backend playback
handle, current play position, device state, or other runtime-only audio state.

GSND is to `AudioResourceManager` roughly what GTS is to tilesheets and GANI is
to animation cycles: a clean authoring definition that can be stored on its own,
embedded in EngineState, or loaded into the runtime registry.

---

## What a GSND definition contains

Each logical member of `AudioDefinitionDocument.Definitions` has type:

```csharp
Gondwana.Audio.GSND.AudioDefinition
```

It contains an ordered collection of:

```csharp
AudioResourceDefinition
```

Each resource defines:

| Property | Purpose |
|---|---|
| `Key` | Unique key used by `AudioResourceManager` |
| `SourceKind` | `LooseFile`, `PackedAsset`, or `Uri` |
| `FilePath` | Loose audio path |
| `AssetsFilePath` | GAF path for packed audio |
| `AssetEntryName` | Audio entry inside the GAF |
| `SourceUri` | URI for URI-backed audio |
| `SourceExtension` | Optional format hint such as `.wav` or `.ogg` |
| `Volume` | Initial volume, from `0.0` through `1.0` |
| `Pan` | Initial stereo pan, from `-1.0` through `1.0` |
| `PlaybackSpeed` | Initial playback rate, currently `0.25x` through `4.0x` |
| `IsLooping` | Whether playback loops |

A simple loose-file definition looks like:

```json
{
  "Resources": [
    {
      "Key": "theme",
      "SourceKind": "LooseFile",
      "FilePath": "audio/theme.ogg",
      "SourceExtension": ".ogg",
      "Volume": 0.8,
      "Pan": 0.0,
      "PlaybackSpeed": 1.0,
      "IsLooping": true
    }
  ]
}
```

The exact JSON is produced by `AudioDefinitionSerializer`; applications should
prefer the serializer rather than hand-assembling format-specific JSON.

---

## Source kinds

### Loose files

A loose resource points at an ordinary audio file:

```json
{
  "Key": "laser",
  "SourceKind": "LooseFile",
  "FilePath": "audio/laser.wav"
}
```

When a GSND file is saved, loose file paths are made relative to the GSND file
whenever the source and destination share a filesystem root. Save As rebases the
reference against the new GSND location.

When the definition is loaded, the path is resolved relative to the GSND file.

### Packed GAF audio

A resource can point at an `Audio` entry inside a GAF:

```json
{
  "Key": "explosion",
  "SourceKind": "PackedAsset",
  "AssetsFilePath": "content/audio.gaf",
  "AssetEntryName": "explosion.wav"
}
```

The referenced GAF must already be loaded when the GSND definition is
materialized directly. EngineState handles this dependency by restoring
`AssetsFiles` before `Audio`.

If an asset entry name has no useful extension, `SourceExtension` supplies the
format hint needed by byte/stream audio backends.

GSND definition files themselves can also be packed into a GAF using:

```csharp
AssetTypes.AudioDefinition
```

### URIs

URI-backed audio uses:

```json
{
  "Key": "browser-theme",
  "SourceKind": "Uri",
  "SourceUri": "assets/theme.mp3"
}
```

Whether a URI is actually playable depends on the configured audio backend.
For example, browser audio is naturally suited to URI sources.

---

## Validation

Use:

```csharp
var errors =
    AudioDefinitionValidator.Validate(definition);
```

Validation checks authoring data without creating playback handles.

It currently verifies:

- non-empty, unique resource keys
- valid source kinds
- required source metadata
- volume and pan ranges
- finite, supported playback-speed values
- a usable extension hint for packed audio whose entry name has no extension

Validation deliberately does not prove that a referenced file, GAF entry, URI,
decoder, output device, or backend will be available at runtime.

---

## Loading and saving

Load a loose GSND file:

```csharp
var definition =
    AudioDefinitionSerializer.Load("content/audio.gsnd");
```

Save one:

```csharp
AudioDefinitionSerializer.Save(
    "content/audio.gsnd",
    definition);
```

Capture the currently registered audio resources:

```csharp
var definition =
    AudioDefinitionSerializer.FromManager(
        AudioResourceManager.Instance);
```

Or save the manager directly:

```csharp
AudioDefinitionSerializer.Save(
    "content/audio.gsnd",
    AudioResourceManager.Instance);
```

---

## Materializing runtime audio

Editing or deserializing GSND does **not** require an audio backend.

Creating live `AudioResource` objects does.

After configuring the appropriate backend:

```csharp
AudioDefinitionSerializer.LoadIntoManager(
    "content/audio.gsnd");
```

The serializer recreates each resource in `AudioResourceManager`, applies its
volume, pan, playback speed, and looping settings, and preserves packed-asset
identity when applicable.

The default behavior replaces an existing resource with the same key. Pass:

```csharp
overwriteExisting: false
```

to preserve an existing resource while applying the incoming portable settings.

GSND does not persist current playback position or whether a sound happened to be
playing or paused at the instant the definition was captured.

---

## EngineState integration

New EngineState saves persist audio through the GSND definition layer instead of
serializing runtime `AudioResource` objects directly.

The default is inline:

```csharp
state.SaveToFile(
    "game.state",
    parts: EngineStateParts.Audio);
```

To externalize audio:

```csharp
state.SaveToFile(
    "game.state",
    parts: EngineStateParts.Audio,
    separateGsndFile: true);
```

For `game.state`, the external layout is:

```text
game.state
game.audio/
    audio.gsnd
```

EngineState restores asset files before GSND audio, so packed GAF references can
resolve correctly.

Older EngineState files that contain the pre-GSND `SoundResources` representation
remain readable. New saves use GSND.

See [[Serialization and EngineState]] for the full snapshot and dependency model.

---

## WinForms tooling

The standalone editor is:

```text
Tooling/Gondwana.Tooling.Audio.WinForms
```

Run it from the repository root:

```console
dotnet run --project Tooling/Gondwana.Tooling.Audio.WinForms -c Release
```

The editor is definition-driven and does not start a `GameHost` or require an
audio backend just to edit a file.

Its hostable `AudioEditorControl` contains editor-local dockable panes for:

- Audio resources
- Properties
- Validation

The standalone application's Working directory browser and each complete GSND
editor remain outer DockPanelSuite windows/documents. This is the same composition
pattern used by the current GTS, GAF, and GANI tooling and is intended for later
reuse by Gondwana Studio.

Layout persistence is not currently implemented.

---

## Runtime versus definition data

A useful distinction is:

```text
GSND definition
    |
    +-- source identity
    +-- resource key
    +-- volume / pan
    +-- playback speed
    +-- looping

runtime AudioResource
    |
    +-- all of the above
    +-- backend playback handle
    +-- current position
    +-- duration
    +-- playing / paused / stopped state
    +-- playback-completion events
```

GSND deliberately describes reproducible content rather than a live audio-device
snapshot.

---

## Where to read next

- [[Audio]]
- [[Assets Files]]
- [[Serialization and EngineState]]
- [[NAudio|Audio---NAudio]]
- [[Browser Audio|Audio---Browser-Audio]]

## Preserving definition origins in EngineState

`EngineStateSaveOptions` supports `DefinitionPersistence.PreserveSource` for this
format. A definition loaded from a named GAF can retain `AssetsFilePath` and
`AssetEntryName`; a loose definition can retain its path. EngineState compares current
serializable state with its loaded baseline and falls back when it changed. Packed
loads use `AssetsFile.SourcePath`, including an explicitly supplied stream origin;
anonymous archives cannot produce path-based provenance. See
[Definition persistence options](Serialization-and-EngineState#definition-persistence-options)
for collection ownership, generated identities, relative paths, and safe fallback.
