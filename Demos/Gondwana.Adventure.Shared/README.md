# Gondwana Adventure

An independent tribute to Warren Robinett's Atari 2600 Adventure. The level-two kingdom's room adjacency and geometric playfield layouts are derived from the [annotated disassembly](https://6502disassembly.com/2600-adventure/Adventure.bin.html), with [NTSC palette reference](https://lospec.com/palette-list/atari-2600-palette-ntsc-version). A 960 × 720 (4:3) playfield sits above a separate HUD. Walls and collision share the same room geometry; castle portals use an explicit keep-entry/exit rule. Actor art, starting placements, simplified AI and synthesized sound are independent interpretations rather than a ROM-exact simulation. No ROM or original executable code/audio is distributed.

All gameplay and rendering live in the shared runtime. GPU WinForms, Avalonia and Blazor hosts reference it directly.

```sh
dotnet run --project Demos/Gondwana.Adventure.WinForms
dotnet run --project Demos/Gondwana.Adventure.Avalonia
dotnet run --project Demos/Gondwana.Adventure.Blazor
```

- Arrow keys or WASD: move; cross the edges to navigate.
- Space: begin the quest, or drop your carried object. Touch objects to pick them up.
- Carry the matching key to a castle's gate, then walk into the gate. Gates stay open; leave keeps via their bottom opening.
- Find the chalice in the black dungeon, return it to the gold keep.
- Sword: defeat dragons at close range. Bridge: drop near a wall to cross. Magnet: draws objects toward you.
- V on title: toggle Game 2 fixed placements / Game 3 randomized placements; P: pause; R: restart; M: mute.
- The invisible dot and two other objects (not the bridge) together open the secret barrier. After death, Space revives the dragons and returns the player to the Golden Castle while preserving item locations.
- Click the browser canvas first to focus input and permit audio.

The audio layer generates PCM WAV tones for pickup, drop, gates, dragon attacks, slaying, bat theft, victory, and startup. WinForms uses its built-in NAudio configuration, Blazor uses Browser Audio, and Avalonia uses NAudio on Windows. Non-Windows Avalonia remains playable without sound pending a portable Gondwana desktop backend.

The embedded Architects Daughter font is distributed under the [SIL Open Font License](Fonts/OFL.txt).
