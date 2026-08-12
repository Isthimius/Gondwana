# Gondwana Space Duel

A small, self-contained spaceship duel built directly on Gondwana's current engine APIs.

## Play

- `A` / `D` or Left / Right: rotate
- `W` or Up: thrust
- `Space`: fire
- `R`: restart
- `Esc`: quit

Destroy all three raiders before they destroy the player's ship. Ships and laser fire wrap across every world edge.

## What the demo exercises

- two code-built star layers with distinct parallax values
- runtime bitmap tilesheets and an embedded ship sprite sheet
- center-anchored sprite rotation on both bitmap and GPU backbuffers
- integrated acceleration, maximum speed, and frame-rate-independent coasting drag
- horizontal and vertical `MovementController` wrapping
- simple steering and firing behavior for three enemy ships
- sprite-based laser projectiles and axis-aligned hit tests
- reusable world-space `HealthBarWidget` instances that follow sprites
- camera follow, view-space HUD drawing, and WinForms keyboard input

Sprite rotation is visual. Collision bounds remain axis-aligned; rotated collision geometry is intentionally outside this demo's scope.

See [Assets/README.md](Assets/README.md) before redistributing the included ship artwork.
