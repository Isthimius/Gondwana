# GSPR sprite definitions

`.gspr` means **Gondwana Sprite**. Each file is a collection-oriented authoring document containing zero or more sprite entries. Empty documents support a new editor session. Developers choose grouping: `player.gspr`, `actors.gspr`, or `projectiles.gspr` have no special engine meaning.

`SpriteDefinition` is the root, with `Sprites`, `TilesheetSources`, and `SceneSources` collections. Each `SpriteInstanceDefinition` holds stable authored state. It is not a runtime object graph, scene, prefab, ECS archetype, or gameplay save system.

## Persistent state

Each entry retains Id, Nickname, SceneId, SceneLayerId, fractional grid Position, logical Frame, visibility, ZOrder, rotation, alignment, nudges, RenderSize, fog and collision settings. A frame references Tilesheet, RegionName, XTile, and YTile. Null Frame represents the existing unassigned-frame Sprite state.

Scene and layer identities bind to canonical runtime objects. Neither scenes nor tilesheets are embedded below sprites. Animator playback, movement commands, velocity, jiggle, pulse/resize timers, event handlers, ValueBag, refresh queues, colliders, CompositeSprite graphs, and manager defaults are not serialized.

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

GTS and GSCN source locations live once at document level. Logical runtime IDs remain on entries. Loose sources hold relative GtsPath or GscnPath; packed sources hold AssetsFilePath and AssetEntryName. These declarations tell authoring tools where definitions can be found. Runtime materialization never opens these paths.

Save As rebases source paths relative to the destination. Runtime provenance distinguishes generated, loose and packed definitions, and is not written into portable JSON.

## Loose and packed storage

Load accepts a path, a readable stream, or an AssetsFile plus entry name. Streams remain open. Packed GSPR uses `AssetTypes.SpriteDefinition` (11); all previous enum values are unchanged. Loading a packed entry records its archive and entry provenance. Standalone JSON is indented and contains no EngineState `$id`/`$ref` metadata.

## EngineState

New EngineState saves contain one sprite-category entry with one inline SpriteDefinition, or a relative GsprPath pointing to one collection file. Append `separateGsprFile: true` to SaveToFile to produce `<state-name>.sprites.gspr`:

```csharp
state.SaveToFile("world.state",
    parts: EngineStateParts.Tilesheets | EngineStateParts.Scenes | EngineStateParts.Sprites,
    separateGtsFiles: true,
    separateGscnFiles: true,
    separateGsprFile: true);
```

The new option is appended to the existing signature. Compression and partial selection remain available. Loading only Sprites resolves against already loaded dependencies; it does not force replacement of scenes or tilesheets. Nickname remains the merge key, and overwriteExisting controls replacement. Legacy raw Sprite arrays and previous SceneLayer-reference data remain readable through the compatibility path.

## Authoring

The standalone `Gondwana.Tooling.Sprites.WinForms` application edits multiple GSPR documents. Its SpriteEditorControl is also hosted directly by Gondwana Studio. Add, duplicate, remove and select entries, load GSCN/GTS sources, double-click a layer or frame to assign it, edit properties, and inspect the preview and validation panes.

Preview uses engine projection math without a game host, live loop, or registered preview sprites/scenes/tilesheets. Structural errors are distinct from missing-source warnings. The shared docking infrastructure restores pane arrangement and visibility per application; standalone and Studio preferences are independent. View restores hidden panes and resets the active editor layout. No document session or zoom state is stored in GSPR or dock preferences.
