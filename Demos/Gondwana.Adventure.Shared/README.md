# Gondwana Adventure

An independent tribute to Warren Robinett's Atari 2600 Adventure. The full kingdom's room adjacency is informed by the [annotated original disassembly](https://6502disassembly.com/2600-adventure/Adventure.bin.html) (room links only). The room geometry, artwork, game code, object placement, AI and synthesized sound effects are original interpretations, not a pixel-perfect ROM recreation. No ROM or original game code/audio/assets are distributed.

All gameplay and rendering live in the shared runtime. GPU WinForms, Avalonia and Blazor hosts reference it directly.

```sh
dotnet run --project Demos/Gondwana.Adventure.WinForms
dotnet run --project Demos/Gondwana.Adventure.Avalonia
dotnet run --project Demos/Gondwana.Adventure.Blazor
```

- Arrow keys or WASD: move; cross the edges to navigate.
- Space: begin the quest, or drop your carried object. Touch objects to pick them up.
- Carry the matching key to a castle's gate, then walk into the gate. Gates stay open.
- Find the chalice in the black dungeon, return it to the gold keep.
- Sword: defeat dragons at close range. Bridge: drop near a wall to cross. Magnet: draws objects toward you.
- V on title: toggle the shuffled-tools variant; P: pause; R: restart; M: mute.
- The tiny dot unlocks a secret passage.
- Click the browser canvas first to focus input and permit audio.

The audio layer generates PCM WAV tones for pickup, drop, gates, dragon attacks, slaying, bat theft, victory, and startup. WinForms uses its built-in NAudio configuration, Blazor uses Browser Audio, and Avalonia uses NAudio on Windows. Non-Windows Avalonia remains playable without sound pending a portable Gondwana desktop backend.

The embedded Architects Daughter font is distributed under the [SIL Open Font License](Fonts/OFL.txt).
