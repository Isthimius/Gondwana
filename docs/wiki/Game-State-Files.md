Gondwana can save selected portions of the running engine to a file and later restore or merge that state.

A game state file is built from `EngineState`, Gondwana's serialization facade over engine-managed runtime content.

Typical uses include:

- saving a collection of scenes and sprites
- restoring a previously saved world
- packaging reusable engine content
- loading additional content into an existing game
- creating test or development fixtures
- mounting state files automatically during engine initialization

A game state file should not, however, be confused with a complete application-specific save-game system.

---

## Contents

- [Game state versus game save data](#game-state-versus-game-save-data)
- [What a state file can contain](#what-a-state-file-can-contain)
- [Saving a state file](#saving-a-state-file)
- [Saving selected state](#saving-selected-state)
- [Loading a state file](#loading-a-state-file)
- [Loading versus merging](#loading-versus-merging)
- [Compressed state files](#compressed-state-files)
- [Tilesheets and GTS files](#tilesheets-and-gts-files)
- [Animations and GANI files](#animations-and-gani-files)
- [Scenes and GSCN files](#scenes-and-gscn-files)
- [Loading state files during startup](#loading-state-files-during-startup)
- [What is not automatically saved](#what-is-not-automatically-saved)
- [Choosing what belongs in EngineState](#choosing-what-belongs-in-enginestate)
- [File names and locations](#file-names-and-locations)
- [Compatibility considerations](#compatibility-considerations)
- [Related documentation](#related-documentation)

---

## Game state versus game save data

The most important distinction is between **engine state** and **game-specific state**.

`EngineState` captures objects and resources managed by Gondwana itself.

For example:

```text
EngineState

├── Assets files
├── Tilesheets
├── Animation cycles
├── Scenes
├── Sprites
└── Audio resources
```

That makes an engine state file useful for saving a substantial portion of a running game world.

It does **not** mean that every variable in the application is automatically persisted.

A game may also need to save information such as:

```text
current quest
player inventory
difficulty
score
completed objectives
dialog choices
unlocked areas
game-specific flags
```

Unless those values are represented by something that `EngineState` already serializes, they remain the responsibility of the game.

A useful mental model is:

```text
              Complete game save
                     |
          +----------+----------+
          |                     |
          v                     v
     Engine state        Game-specific data
          |                     |
          v                     v
 scenes, sprites,        quests, inventory,
 tilesheets, etc.        score, progression,
                         custom state, etc.
```

Some games may need only one side of that diagram. Others will combine both.

---

## What a state file can contain

State selection is controlled by the `[Flags]` enum `EngineStateParts`.

The currently supported categories are:

| Part | Represents |
| --- | --- |
| `AssetsFiles` | Loaded Gondwana asset files |
| `Tilesheets` | Registered tilesheet definitions |
| `Cycles` | Animation cycles |
| `Scenes` | Registered scenes |
| `Sprites` | Sprites managed by `SpriteManager` |
| `Audio` | Registered audio resources |
| `All` | All currently supported state categories |
| `None` | No state categories |

The active engine exposes its state through:

```csharp
Engine.Instance.State
```

`EngineState` is backed by Gondwana's live registries. It is not simply a disconnected object containing a private copy of everything in the engine.

When a state file is saved, Gondwana constructs a serializable snapshot from those registries.

For a deeper look at that architecture, see [[Serialization and EngineState]].

---

## Saving a state file

The simplest save is:

```csharp
Engine.Instance.State.SaveToFile("savegame.json");
```

By default this:

- saves all supported `EngineStateParts`
- writes human-readable JSON
- does not compress the file
- stores tilesheet definitions inside the state file
- stores animation definitions inline as GANI data
- stores scene definitions inline as GSCN data

If the destination file already exists, it is overwritten.

The destination directory must already exist.

For example:

```csharp
Directory.CreateDirectory("Saves");

Engine.Instance.State.SaveToFile(
    Path.Combine("Saves", "slot1.json"));
```

---

## Saving selected state

A state file does not have to contain the entire engine state.

For example, a world-oriented file might contain only scenes and sprites:

```csharp
Engine.Instance.State.SaveToFile(
    "world.json",
    parts:
        EngineStateParts.Scenes |
        EngineStateParts.Sprites);
```

A resource-oriented file might contain:

```csharp
Engine.Instance.State.SaveToFile(
    "resources.json",
    parts:
        EngineStateParts.AssetsFiles |
        EngineStateParts.Tilesheets |
        EngineStateParts.Cycles |
        EngineStateParts.Audio);
```

Selective state files are useful when the file represents something other than a traditional saved game.

Examples include:

- a level
- a resource bundle
- optional content
- editor-generated content
- a test fixture
- a runtime snapshot

### Dependencies

Some engine state depends on other engine state.

For example, a tilesheet or audio resource may depend on an `AssetsFile`, and a GANI animation depends on the tilesheets that provide its frames.

When state is restored, Gondwana handles known dependencies and restores engine registries in an appropriate order.

When creating a standalone partial state file, however, include the resources that the saved state will need.

For example:

```csharp
var parts =
    EngineStateParts.AssetsFiles |
    EngineStateParts.Tilesheets;

Engine.Instance.State.SaveToFile(
    "tilesheets.json",
    parts: parts);
```

This is safer than assuming that the destination game will already have every referenced resource loaded.

---

## Loading a state file

A previously saved state can replace the corresponding current engine state with:

```csharp
EngineState.LoadFromFile("savegame.json");
```

`LoadFromFile` operates directly on Gondwana's live registries.

It does not return a detached `EngineState` object.

You can also restore only selected categories:

```csharp
EngineState.LoadFromFile(
    "world.json",
    parts:
        EngineStateParts.Scenes |
        EngineStateParts.Sprites);
```

Only the selected categories, plus any required dependencies, participate in the load.

This means a partial load does not automatically destroy unrelated engine state.

---

## Loading versus merging

Gondwana provides two different ways to apply a state file.

### `LoadFromFile`

```csharp
EngineState.LoadFromFile("world.json");
```

Use `LoadFromFile` when the state file should become authoritative for the selected categories.

Conceptually:

```text
existing selected state
        |
        v
      clear
        |
        v
state from file
```

### `MergeFromFile`

```csharp
EngineState.MergeFromFile("extra-content.json");
```

Use `MergeFromFile` when the file should be added to the engine without first clearing the selected registries.

Conceptually:

```text
existing state
      +
incoming state
      |
      v
combined engine state
```

By default, existing objects win when the incoming file contains something with the same engine identity.

Incoming values can instead be allowed to replace existing entries:

```csharp
EngineState.MergeFromFile(
    "patch.json",
    overwriteExisting: true);
```

### Which should I use?

| Goal | Use |
| --- | --- |
| Restore a saved world | `LoadFromFile` |
| Replace selected engine state | `LoadFromFile` |
| Add optional content | `MergeFromFile` |
| Layer several state files | `MergeFromFile` |
| Apply incoming state as a patch | `MergeFromFile(..., overwriteExisting: true)` |

---

## Compressed state files

State files can optionally be compressed using GZip.

To save one:

```csharp
Engine.Instance.State.SaveToFile(
    "savegame.dat",
    compress: true);
```

To load it:

```csharp
EngineState.LoadFromFile(
    "savegame.dat",
    compressed: true);
```

Or when merging:

```csharp
EngineState.MergeFromFile(
    "content.dat",
    compressed: true);
```

Compression is not detected from the filename. The caller must specify that the file is compressed.

Compression also provides **no encryption or security**. It only reduces the size of the serialized JSON.

---

## Tilesheets and GTS files

Tilesheets receive special handling during engine-state serialization.

By default, the tilesheet definitions needed by the state are stored inside the state file.

```csharp
Engine.Instance.State.SaveToFile(
    "session.json");
```

They can instead be written as separate `.gts` files:

```csharp
Engine.Instance.State.SaveToFile(
    "session.json",
    separateGtsFiles: true);
```

This can be useful when tilesheet definitions should remain individually inspectable or editable rather than being embedded into one larger state file.

The state file retains the information needed to locate those external definitions when it is loaded again.

For the tilesheet definition format itself, see [[GTS Files]].

---

## Animations and GANI files

Animation cycles receive the same definition-file treatment as tilesheets and scenes.

By default, each registered cycle is converted to an inline GANI `AnimationDefinition`:

```csharp
Engine.Instance.State.SaveToFile(
    "session.json");
```

Animations can instead be written as separate `.gani` files:

```csharp
Engine.Instance.State.SaveToFile(
    "session.json",
    separateGaniFiles: true);
```

For `session.json`, external definitions are written beneath:

```text
session.json
session.animations/
    actor.walk.gani
    world.water.gani
    ...
```

GANI frame entries reference tilesheets by logical name, region, and coordinates. Animation restoration therefore depends on the referenced tilesheets. When `EngineStateParts.Cycles` is applied, Gondwana includes the tilesheet dependency chain and restores tilesheets before materializing animations.

For a partial state file that must restore independently, save its required `Tilesheets` along with its `Cycles`.

The `separateGaniFiles`, `separateGtsFiles`, and `separateGscnFiles` choices are independent.

For the animation-definition format itself, see [[GANI Files]].

---

## Scenes and GSCN files

Scenes use the GSCN scene-definition format when EngineState is saved.

By default, each scene definition is embedded inline in the state file:

```csharp
Engine.Instance.State.SaveToFile(
    "session.json");
```

Scenes can instead be written as separate `.gscn` files:

```csharp
Engine.Instance.State.SaveToFile(
    "session.json",
    separateGscnFiles: true);
```

For `session.json`, external scene definitions are written under a sibling directory:

```text
session.json
session.scenes/
    <scene-id>.gscn
    ...
```

The main state file contains relative references to those files when possible.

This works independently of `separateGtsFiles`. A project can therefore choose any combination of inline/external tilesheet and scene definitions.

Sprites remain a separate EngineState category. When restored, their saved `SceneId` and `SceneLayerId` values are used to reconnect them to the canonical layers created from GSCN.

For the scene-definition format itself, see [[GSCN Files]]. For implementation details, see [[Serialization and EngineState]].

---

## Loading state files during startup

State files can also be declared as part of `EngineConfiguration`.

Each startup file is represented by a `StateFileMount`.

For example:

```csharp
new StateFileMount
{
    File = "Content/base-world.json",
    IsCompressed = false,
    OverwriteExisting = false,
    EngineStateParts = EngineStateParts.All
};
```

A mount controls:

- which file to load
- whether the file is GZip-compressed
- whether incoming entries replace matching existing entries
- which `EngineStateParts` should be applied

Configured state files are merged during engine initialization.

Multiple state files can therefore be layered:

```text
base content
     |
     v
extra content
     |
     v
game-specific content
     |
     v
running engine
```

The files are processed in configuration order.

This can be useful for:

- separating engine content from game content
- modular level or resource packages
- optional content
- editor workflows
- layered configuration of a game world

See [[Engine Configuration]] for the complete startup configuration model.

---

## What is not automatically saved

`EngineState` should not be treated as a memory dump of the entire running process.

Only the explicitly supported engine-state categories are included.

Of particular importance, `EngineState.ValueBag` is currently marked with `JsonIgnore` and is **not persisted** by `SaveToFile`.

Likewise, application variables outside Gondwana's serialized registries are not automatically captured.

For example:

```csharp
int playerGold = 500;
bool rescuedPrincess = true;
string currentQuest = "FindTheThing";
```

Those variables do not become part of an `EngineState` file simply because the game is running when `SaveToFile` is called.

They require application-level persistence.

---

## Choosing what belongs in EngineState

Use `EngineState` for state that naturally belongs to Gondwana-managed engine objects.

Examples include:

- scene definitions
- sprite instances
- sprite positions and other serialized sprite state
- tilesheets
- animation definitions
- engine-managed audio resources

Use a game-specific save model for state whose meaning belongs to the game rather than the engine.

For example:

```csharp
public sealed class PlayerSaveData
{
    public int Gold { get; set; }
    public int CurrentLevel { get; set; }
    public List<string> Inventory { get; set; } = [];
    public List<string> CompletedQuests { get; set; } = [];
}
```

A larger game may save both:

```text
slot1/
├── engine-state.json
└── game-data.json
```

There is no requirement that those be separate files. A game may choose whatever persistence structure best matches its design.

The important distinction is ownership:

> Gondwana serializes Gondwana state. The game remains responsible for game-specific state.

---

## File names and locations

`EngineState` does not require a particular file extension.

These are all valid choices:

```text
savegame.json
world.state
level1.json
slot1.dat
content.gondwana-state
```

For uncompressed files, `.json` is useful because it makes the format immediately obvious and keeps the file easy to inspect.

For compressed files, an application may prefer a different extension.

Games should also consider where save files belong.

Development files may live next to the executable or project.

User save data will usually belong in an operating-system-appropriate per-user application-data directory rather than beside installed program files.

The game, not `EngineState`, determines that storage policy.

---

## Compatibility considerations

Engine state files serialize actual Gondwana object graphs and type information.

That makes them closely related to the engine version and the types used by the application.

A state file should therefore not automatically be treated as a permanent, version-independent interchange format.

When a game expects save files to survive significant future changes, consider:

- maintaining an explicit save-data version
- keeping application-specific progression in a small game-owned DTO
- providing migration logic when game data changes
- testing old saves against new releases
- avoiding unnecessary implementation details in long-lived save formats

For development snapshots, editor files, fixtures, and content layering, direct `EngineState` serialization may be exactly what is needed.

For a commercial game's permanent player save format, a deliberately versioned game-owned layer may be preferable.

---

## Related documentation

For the implementation details behind state serialization, including snapshot construction, registry restoration, dependency handling, merge identity, serializer settings, and reference preservation, see:

- [[Serialization and EngineState]]
- [[Engine Configuration]]
- [[Assets Files]]
- [[GTS Files]]
- [[GANI Files]]
- [[GSCN Files]]
- [[Sprites]]

The distinction is intentional:

**Game State Files** explains how a game uses persisted state. **Serialization and EngineState** explains how Gondwana implements it.