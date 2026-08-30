# The Greenward Key

The Greenward Key is a playable, code-first top-down action-RPG prototype built with Gondwana and .NET 8 for Windows. Its gameplay uses imported *The Legend of Zelda: A Link to the Past* sprite sheets through Gondwana `.gts` definitions.

## Art source and redistribution

The PNG sheets in `assets/` came from [The Spriters Resource: The Legend of Zelda: A Link to the Past](https://www.spriters-resource.com/snes/legendofzeldaalinktothepast/). The game and artwork are owned by their respective rights holders; their presence here does not grant redistribution rights. Treat this demo and its imported assets as local, non-commercial experimentation unless you have confirmed permission for your intended use.

Each used sheet has a companion `.gts` file that defines named source regions and the appropriate white, teal, or magenta transparency mask. `overworld.png` is currently byte-for-byte identical to `forest.png` and is not loaded separately.

## Switching art sources

The demo supports both its generated tilesheet and the imported `.gts` definitions. Change the single `GameArt.Mode` field near the top of `GameArt.cs`:

```csharp
internal static readonly GameArtMode Mode = GameArtMode.Generated;
```

Use `GameArtMode.Generated` for the code-drawn artwork or `GameArtMode.Gts` for the imported sprite sheets. Both modes populate the same logical frame IDs, so no gameplay code changes are required.

## Playable features

- title screen with new-game and load-game paths
- a scrolling overworld with paths, water, trees, rocks, an NPC, enemies, and pickups
- Gondwana grid-space sprite movement and world-pixel AABB collision resolution
- four-direction sword combat with one hit per enemy per swing
- health, damage cooldown, knockback, enemy health bars, potions, game over, and restart flow
- NPC dialogue with multiple pages
- inventory containing the sword, rusted key, potions, and sun relic
- a separate dungeon region with a locked gate, dungeon enemies, and the Hollow King boss
- JSON save/load for player position, health, area, facing, inventory, collected pickups, and enemy/boss health
- keyboard and Windows XInput controller support, including analog-stick and D-pad movement

## Requirements

- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- optional Xbox-compatible/XInput controller

## Run

From the project root:

```console
dotnet restore
dotnet run --project Demos/Gondwana.ZeldaPrototype/Gondwana.ZeldaPrototype.csproj
```

The normal project path follows Gondwana's current WinForms template and references the latest `2.*` packages.

To compile directly against a Gondwana source checkout instead:

```powershell
./run-against-source.ps1 -GondwanaSourceRoot C:\src\Gondwana
```

## Controls

| Action | Keyboard | XInput controller |
| --- | --- | --- |
| Move | WASD or arrows | Left stick or D-pad |
| Sword | Space | X |
| Talk / use entrance | E or Enter | A |
| Inventory | I | Y |
| Use potion | H | B |
| Pause / resume | P or Enter | Start |
| Save | F5 | Left shoulder |
| Load | F9 | Right shoulder |
| Quit | Escape | — |

At the title screen, press Enter/N/A to start a new game or L/F9/Y to load.

## Objective

Speak with Elder Rowan, cross the eastern bridge, recover the rusted key, and enter the barrow. The key opens the inner gate. Defeat the Hollow King beyond it.

Save data is written to:

```text
%LOCALAPPDATA%\HiddenWorldsGames\GondwanaZeldaPrototype\savegame.json
```

## Project structure

```text
Gondwana.ZeldaPrototype/
├── assets/
│   ├── forest.png
│   ├── forest.gts
│   ├── link.png
│   ├── link.gts
│   └── ...
├── README.md
├── docs/
│   └── API-VERIFICATION.md
├── run-against-source.ps1
├── GameArt.cs
├── GameHealthBar.cs
├── GameModels.cs
├── GameWindow.cs
├── Gondwana.ZeldaPrototype.csproj
├── Program.cs
├── SaveGameService.cs
├── ZeldaGameHost.cs
├── ZeldaGameHost.Gameplay.cs
└── ZeldaGameHost.World.cs
```

## Architecture notes

- The ground is a separate visual `SceneLayer`.
- Blocking tiles and movable actors deliberately share one gameplay `SceneLayer`, because the current collision registry/resolver is layer-scoped.
- A narrow fixed-tile compatibility shim covers the public 2.5.2 package's hidden collider field; current source uses its normal public collider.
- Sprites move in fractional grid units. Gondwana derives world-pixel drawing and collision rectangles.
- HUD, title, dialogue, inventory, and pause interfaces are view-bound `DirectRectangle` and `TextBlock` objects, so camera movement does not move them.
- Health bars use world-bound `DirectRectangle` primitives and follow their target sprites automatically. This keeps the default build compatible with the public `2.*` packages while using the same current-source drawing APIs.
- Save data uses a small game-specific DTO. `EngineState.ValueBag` is intentionally transient, while full `EngineState` persistence captures engine registries rather than serving as an arbitrary gameplay-save bag.

## Prototype limits

This is deliberately a compact prototype, not a commercial Zelda clone in a trench coat. Combat and collision use AABBs, enemies use direct chase behavior rather than pathfinding, the selected imported frames are static, and there is no audio or quest scripting layer. All requested gameplay pillars are represented and playable without requiring engine changes.
