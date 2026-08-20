# Spot!

**Spot!** is a small, turn-based territory game for Windows and the primary playable showcase for the [Gondwana Game Engine](../..). It is built entirely in C# and .NET 8 using Gondwana's WinForms hosting, rendering, input, audio, sprite, particle, movement, and scene systems.

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
| **Backtick / tilde key** | Show or hide the score display. |
| **Game > New Game** | Configure and start another game. |
| **Options** | Toggle music, sound effects, spot jiggle, clouds, or GPU acceleration. |

Changing **GPU Acceleration** requires restarting Spot! The bitmap renderer is used by default; the GPU-backed renderer can be enabled from the Options menu.

## Run the Windows version

Spot! targets 64-bit Windows and is developed as part of the Gondwana repository. You will need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```console
git clone https://github.com/Isthimius/Gondwana.git
cd Gondwana
dotnet run --project Demos/Spot/Spot.csproj
```

You can also open `Gondwana.sln`, set **Spot** as the startup project, and run it from Visual Studio with the .NET desktop development workload installed.

To create a self-contained Windows x64 release build:

```console
dotnet publish Demos/Spot/Spot.csproj -c Release
```

The release configuration produces a self-contained, single-file `win-x64` executable, with the game's content assets copied alongside it.

## How it is made

At 10,000 feet, Spot! is a conventional turn-based game sitting on top of Gondwana's real-time engine loop:

- [`GameWindow`](GameWindow.cs) is the native WinForms shell. It owns the menu, saved options, startup splash, and the choice between bitmap and GPU rendering.
- [`SpotGameHost`](Hosts/SpotGameHost.cs) and [`SpotGpuGameHost`](Hosts/SpotGpuGameHost.cs) connect that window to Gondwana's Windows hosting and render-surface implementations.
- [`SpotHostCore`](Hosts/SpotHostCore.cs) contains the presentation layer shared by both renderers: asset loading, mouse input, AI turns, animation, particles, audio, scores, and game-over presentation.
- [`SpotGame`](Game/SpotGame.cs) owns the turn sequence, selection and move execution, scoring, capture events, and end-game rules.
- [`SpotGameField`](Game/SpotGameField.cs) represents the board as a Gondwana `SceneLayer`. Each logical cell stores its game state while Gondwana sprites provide the visible spots.

The rule layer raises events such as selection, movement, capture, turn changes, and game over. The host responds with presentation—sprite frames, easing animations, sound effects, score updates, and particle effects. Keeping those responsibilities separate lets the same game rules drive either Windows rendering backend without duplication.

Spot! exercises a broad slice of Gondwana in one compact project:

- WinForms game hosting and native mouse/keyboard input
- CPU bitmap and GPU-backed render surfaces
- Scene layers, views, coordinates, sprites, and z-ordering
- Movement easing, pulsing, resizing, and jiggle effects
- Direct-drawn text and shapes for scores and messages
- Particle surfaces for the opening effect and drifting clouds
- Music, sound effects, custom fonts, and tilesheet-backed artwork
- Engine timers for computer-player pacing
- Persistent configuration for user options

The computer player is intentionally straightforward: it evaluates legal moves by their immediate net territorial gain, then randomly chooses among the best-scoring options. No neural networks, no mysterious black box—just a small opponent that knows enough to be troublesome.

## About Gondwana

[Gondwana](https://github.com/Isthimius/Gondwana) is a code-first, cross-platform 2D and 2.5D game and rendering engine for C# and .NET 8. Spot! serves both as a playable game and as a dogfooding project for the engine's Windows stack.

The Gondwana source is released under the [MIT License](../../LICENSE). Third-party font, music, sound, and art attribution for Spot! is recorded in [`assets/sources.txt`](assets/sources.txt) and [`assets/OFL.txt`](assets/OFL.txt).
