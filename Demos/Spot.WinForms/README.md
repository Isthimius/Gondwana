# Spot.WinForms

**Spot!** is a small, turn-based territory game and a primary playable showcase for the [Gondwana Game Engine](https://github.com/Isthimius/Gondwana/). The same game now runs through Gondwana's WinForms GPU host on Windows and its Blazor/WebGL host in the browser, with the game rules, Widgets UI, gameplay presentation, and effects shared between them.

<p>
  <img width="49%" alt="Spot gameplay showing the game board and score display" src="https://github.com/user-attachments/assets/c29ddd87-fb82-46dc-ad5e-6388c11ba50d" />
  <img width="49%" alt="Spot gameplay showing animated movement and scene effects" src="https://github.com/user-attachments/assets/0aef0b63-1c16-44be-b6a6-d456f4799ce8" />
</p>

## How to play

The goal is simple: finish the game with more spots on the board than any other player.

1. Start a game from the opening screen or choose **Game > New Game**.
2. Choose 2–4 players, a board size from 3×3 through 12×12, and whether each player is human- or computer-controlled.
3. On your turn, click one of your spots to select it.
4. Click an empty destination up to two squares away, horizontally, vertically, or diagonally.
5. Every opposing spot immediately adjacent to the destination becomes yours.

There are two kinds of move:

| Move | Distance | Result |
| --- | --- | --- |
| **Clone** | One square | Your original spot remains and a new spot is created at the destination. |
| **Jump** | Two squares | Your spot moves to the destination, leaving its original square empty. |

Click a selected spot again to deselect it. If a player has no legal move, that turn is skipped automatically.

The game ends when no legal moves remain anywhere on the board or only one player still has spots. The player with the highest score wins; ties are possible.

## Controls and options

| Input | Action |
| --- | --- |
| **Left mouse button** | Select a spot or choose its destination. |
| **Tab** | Show or hide the score display. |
| **Game > New Game** | Configure and start another game. |
| **Options** | Toggle music, sound effects, spot jiggle, or clouds. |
| **Help > How to play** | Open the in-game rules and controls reference. |

## Run the browser version

The Blazor version uses the same Gondwana Widgets for the menu, new-game dialog, help, and About UI. Browser settings are persisted through `localStorage`; **Game > Exit** is intentionally omitted because a web application cannot reliably close a user-created browser tab.

```console
dotnet run --project Demos/Spot.Blazor/Spot.Blazor.csproj
```

During the build, the canonical files from `Demos/Spot.Shared/assets` are packed into a Gondwana `.gaf`. The browser downloads that single package, opens it through `AssetsFile.Load(Stream)`, and loads images, fonts, music, and sound effects from the package.

## Run the Windows version

Spot! targets 64-bit Windows and is developed as part of the Gondwana repository. You will need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```console
git clone https://github.com/Isthimius/Gondwana.git
cd Gondwana
dotnet run --project Demos/Spot.WinForms/Spot.WinForms.csproj
```

You can also open `Gondwana.sln`, set **Spot.WinForms** as the startup project, and run it from Visual Studio with the .NET desktop development workload installed.

To create a self-contained Windows x64 release build:

```console
dotnet publish Demos/Spot.WinForms/Spot.WinForms.csproj -c Release
```

The release configuration produces a self-contained, single-file `win-x64` executable, with `assets/spot.gaf` copied alongside it.

## How it is made

At 10,000 feet, Spot! is a conventional turn-based game sitting on top of Gondwana's real-time engine loop:

- [`Spot.Shared`](../Spot.Shared/) contains the canonical game model, Widget dialogs, menu/About construction, settings helpers, and the common host partials used by both platform demos.
- [`GameWindow`](GameWindow.cs) is the thin native WinForms shell.
- [`SpotGameHost`](Hosts/SpotGameHost.cs) supplies the WinForms GPU host and desktop asset-loading seam.
- [`Spot.Blazor`](../Spot.Blazor/) supplies the Blazor/WebGL shell, local-storage configuration store, browser URI launching, and streamed GAF asset loading.
- [`SpotGame`](../Spot.Shared/Game/SpotGame.cs) owns the turn sequence, selection and move execution, scoring, capture events, and end-game rules.
- [`SpotGameField`](../Spot.Shared/Game/SpotGameField.cs) represents the board as a Gondwana `SceneLayer`. Each logical cell stores its game state while Gondwana sprites provide the visible spots.

The rule layer raises events such as selection, movement, capture, turn changes, and game over. The shared host partials respond with presentation—sprite frames, easing animations, sound effects, score updates, particles, dialogs, and HUD updates—while the two platform projects remain responsible only for their actual platform differences.

Spot! exercises a broad slice of Gondwana in one compact project:

- WinForms and Blazor/WebGL game hosting
- Native Windows and browser mouse/keyboard input
- GPU-backed rendering on both hosts
- Scene layers, views, coordinates, sprites, and z-ordering
- Movement easing, pulsing, resizing, and jiggle effects
- Direct-drawn text and shapes for scores and messages
- Particle surfaces for the opening effect and drifting clouds
- Music, sound effects, custom fonts, and tilesheet-backed artwork
- Engine timers for computer-player pacing
- Persistent configuration for user options through file or browser-localStorage stores
- Stream-loaded GAF content, including byte-backed browser audio

The computer player is intentionally straightforward: it evaluates legal moves by their immediate net territorial gain, then randomly chooses among the best-scoring options. No neural networks, no mysterious black box—just a small opponent that knows enough to be troublesome.

## About Gondwana

[Gondwana](https://github.com/Isthimius/Gondwana) is a code-first, cross-platform 2D and 2.5D game and rendering engine for C# and .NET 8. Spot! serves both as a playable game and as a dogfooding project for Gondwana's Windows and browser stacks.

The Gondwana source is released under the [MIT License](../../LICENSE). Third-party font, music, sound, and art attribution for Spot! is recorded in [`assets/sources.txt`](assets/sources.txt) and [`assets/OFL.txt`](assets/OFL.txt).

## Shared assets

All three Spot hosts package `../Spot.Shared/assets` with `Spot.Assets.targets`.
The generated `assets/spot.gaf` contains runtime images, audio, fonts, and the
`spot_defaults.gts` / `spot_selected.gts` tilesheet definitions. The definitions
reference their PNG entries in the same package and preserve the five vertical
93x96 default frames and five horizontal 64x64 selected frames. Archived assets
are excluded. Desktop output needs only the GAF; the WinForms application icon
is a build input and Blazor's loading icon remains a loose bootstrap resource.
