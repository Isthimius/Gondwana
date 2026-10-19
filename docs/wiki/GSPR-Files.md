# GSPR sprite definitions

## Definition documents and logical members

One physical `.gspr` file contains a document object with a `Definitions` array.
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
single `AssetTypes.SpriteDefinition` GAF entry. GAF remains a typed binary container;
loading or saving a document does not create entries for individual members.

```csharp
var selected = SpriteDefinitionSerializer.Load("content.gspr", "second");
var definitions = SpriteDefinitionSerializer.LoadAll("content.gspr");
var document = SpriteDefinitionSerializer.LoadDocument("content.gspr");
SpriteDefinitionSerializer.SaveDocument("copy.gspr", document);
SpriteDefinitionSerializer.SaveAll("copy.gspr", definitions);

var packed = SpriteDefinitionSerializer.Load(assetsFile, "content.gspr", "second");
var packedMembers = SpriteDefinitionSerializer.LoadAll(assetsFile, "content.gspr");
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
Anonymous legacy JSON uses `sprite`; canonical document members must explicitly provide a key.

Use `SpriteDefinitionSerializer.LoadAllSprites` to materialize every member.

Load-all operations roll back newly created runtime content if a member fails.
For physical packaging, export, move, and deletion workflows, see [Assets Files](Assets-Files). These operations preserve logical member identities; [Reference Relocation](Reference-Relocation) describes scope and safety guarantees.

The shared [Import definition workflow](Studio-and-Desktop-Tools#import-into-an-open-document) appends independent members to an open document without changing the source file.

`.gspr` means **Gondwana Sprite**. Each file contains named sprite definitions, each with its own collection of sprite entries. An empty sprite collection supports a new editor session. Developers choose grouping: `player.gspr`, `actors.gspr`, or `projectiles.gspr` have no special engine meaning.

`SpriteDefinitionDocument` is the document root. Each contained `SpriteDefinition` has a stable `Key`, with `Sprites`, `TilesheetSources`, and `SceneSources` collections. Each `SpriteInstanceDefinition` holds stable authored state. It is not a runtime object graph, scene, prefab, ECS archetype, or gameplay save system.

## Persistent state

Each entry retains Id, Nickname, SceneId, SceneLayerId, fractional grid Position, logical Frame, visibility, ZOrder, rotation, alignment, nudges, RenderSize, fog and collision settings. A frame references Tilesheet, RegionName, XTile, and YTile. Null Frame represents the existing unassigned-frame Sprite state.

Scene and layer identities bind to canonical runtime objects. Neither scenes nor tilesheets are embedded below sprites. Animator playback position and timers, movement commands, velocity, jiggle, pulse/resize timers, event handlers, ValueBag, refresh queues, colliders, CompositeSprite graphs, and manager defaults are not serialized.

## Animation assignments

A sprite entry can declare `AnimationKey` and `StartAnimation`, just as a scene tile can.
`AnimationKey` identifies a registered GANI/Cycle by logical key; `StartAnimation`
determines whether that cycle begins playing when the sprite is materialized.
Sprites already own an animator, so GSPR does not need `EnableAnimator`.

GSPR stores no runtime `Animator` object or playback position. Materialization
uses `Cycle.GetAnimationCycle(key)` to obtain a per-sprite clone, and requires the
GANI cycle to be registered first. Missing keys fail explicitly and roll back newly
created sprites. Older GSPR definitions without these fields keep their prior behavior.

`AnimationSources` optionally records loose GANI paths or typed GAF entries for
standalone/Studio authoring. Like GTS and GSCN source metadata, it does not load
cycles at runtime. In the sprite editor, **Add GANI…** makes the keys discoverable,
and the Animation selector or properties assign a key; `StartAnimation` is
editable independently.

## Definition and runtime boundary

```csharp
using Gondwana.Drawing.Sprites.GSPR;

var definition = SpriteDefinitionSerializer.FromSprites(
    SpriteManager.Instance.AllSprites);
SpriteDefinitionSerializer.Save("actors.gspr", definition);

// Parses data only; creates no runtime sprites.
var loaded = SpriteDefinitionSerializer.Load("actors.gspr");
var errors = SpriteDefinitionValidator.Validate(loaded);

// Required scenes, layers, and tilesheets must already exist.
var sprites = SpriteDefinitionSerializer.ToSprites(loaded);
```

Materialization validates structure, resolves canonical dependencies, creates through SpriteManager, and applies authored state. Each sprite registers once. Failure removes and disposes previously created members of the incoming collection. RenderSize is explicit, independent of SizeNewSpritesToSceneLayer.

Validation rejects duplicate supplied GUIDs and nonempty nicknames, missing scene/layer identities, invalid logical frames, nonfinite positions/rotation, negative size, invalid enums, and incomplete or duplicate authoring sources. Empty GUIDs retain newly generated runtime IDs. Empty nicknames are allowed; EngineState assigns usable names during merge.

## Shared authoring sources

`SpriteDefinitionSerializer.FromSprites()` records GTS, GSCN, and GANI source
locations for referenced runtime dependencies when their definitions match the
recorded provenance. Animation sources are collected from registered cycles,
including `NextCycle` transitions, rather than per-sprite playback clones.
Dependencies without matching provenance retain logical references without
physical source paths. `Save(path, sprites)` stores source paths relative to
the destination GSPR.

GTS, GSCN, and GANI source locations live once at document level. Logical runtime IDs remain on entries. Loose sources hold relative GtsPath or GscnPath; packed sources hold AssetsFilePath and AssetEntryName. These declarations tell authoring tools where definitions can be found. Runtime materialization never opens these paths.

Save As rebases source paths relative to the destination. Runtime provenance distinguishes generated, loose and packed definitions, and is not written into portable JSON.

## Loose and packed storage

Load accepts a path, a readable stream, or an AssetsFile plus entry name. Streams remain open. Packed GSPR uses `AssetTypes.SpriteDefinition` (11); all previous enum values are unchanged. Loading a packed entry records its archive and entry provenance. Standalone JSON is indented and contains no EngineState `$id`/`$ref` metadata.

## EngineState

New EngineState saves contain one sprite-category entry with an inline SpriteDefinition, a relative GsprPath, or a packed GAF/entry reference when PreserveSource is selected. Append `separateGsprFile: true` to SaveToFile to produce `<state-name>.sprites.gspr`:

```csharp
state.SaveToFile("world.state",
    parts: EngineStateParts.Tilesheets | EngineStateParts.Scenes | EngineStateParts.Sprites,
    separateGtsFiles: true,
    separateGscnFiles: true,
    separateGsprFile: true);
```

The new option is appended to the existing signature. Compression and partial selection remain available. Loading inline/loose Sprites alone resolves against already loaded dependencies. Packed sprite entries additionally include available scene/tilesheet dependencies. Nickname remains the merge key, and overwriteExisting controls replacement. Legacy raw Sprite arrays and previous SceneLayer-reference data remain readable through the compatibility path.

## Authoring

The standalone `Gondwana.Tooling.Sprites.WinForms` application edits multiple GSPR documents. Its SpriteEditorControl is also hosted directly by Gondwana Studio. Add, duplicate, remove and select entries, load GSCN/GTS sources, double-click a layer or frame to assign it, edit properties, and inspect the preview and validation panes.

Preview uses engine projection math without a game host, live loop, or registered preview sprites/scenes/tilesheets. Structural errors are distinct from missing-source warnings. The shared docking infrastructure restores pane arrangement and visibility per application; standalone and Studio preferences are independent. View restores hidden panes and resets the active editor layout. No document session or zoom state is stored in GSPR or dock preferences.

## Preserving definition origins in EngineState

`EngineStateSaveOptions` supports `DefinitionPersistence.PreserveSource` for this
format. A definition loaded from a named GAF can retain `AssetsFilePath` and
`AssetEntryName`; a loose definition can retain its path. EngineState compares current
serializable state with its loaded baseline and falls back when it changed. Packed
loads use `AssetsFile.SourcePath`, including an explicitly supplied stream origin;
anonymous archives cannot produce path-based provenance. See
[Definition persistence options](Serialization-and-EngineState#definition-persistence-options)
for collection ownership, generated identities, relative paths, and safe fallback.

## Runtime Scene Viewer discovery

The standalone Scene Viewer discovers loose GSPR and typed GSPR entries in every GAF directly beside the opened GSCN. It validates complete documents but materializes only entries whose `SceneId` matches the viewed scene (ordinal comparison), after its layers exist. It never follows `SceneSources` to load other scenes.

For selected frame tilesheets, the shared Viewer resolver prefers a matching local loose GTS, then a packed GTS in an adjacent GAF, then the GSPR's explicit source metadata. Discovery is non-recursive. A matching non-empty sprite ID or nickname makes loose sprites override packed sprites; duplicate identities at the surviving precedence fail with source diagnostics. Entries without either identity remain distinct. Source documents remain unchanged; the assembled filtered collection cannot preserve one document's collection-wide provenance.
