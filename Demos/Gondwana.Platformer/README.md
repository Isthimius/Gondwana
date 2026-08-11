# Gondwana Platformer

A small, self-contained platform game built directly on Gondwana's current engine APIs.

## Play

- `A` / `D` or Left / Right: move
- `W`, Up, or Space: jump
- `R`: restart
- `Esc`: quit

Collect all five sun relics, then reach the red flag. Falling into a pit or touching spikes returns the player to the start; collected relics remain collected until the level is restarted.

## What the demo exercises

- a code-built `Scene` with parallax and world layers
- bitmap-backed runtime tilesheets
- fixed layer-tile colliders and a dynamic sprite collider
- integrated sprite velocity and acceleration for running, gravity, and jumping
- horizontal camera follow with a dead zone and world-bound clamping
- view-bound `DirectRectangle` and `TextBlock` HUD elements
- WinForms keyboard input through Gondwana's keyboard poller

The pixel art is generated in code by `PlatformerArt`, so the demo has no external asset setup or licensing requirements.
