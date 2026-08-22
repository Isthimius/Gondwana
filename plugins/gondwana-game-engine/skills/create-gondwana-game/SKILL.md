---
name: create-gondwana-game
description: Builds or extends games, demos, samples, and gameplay features with the Gondwana C#/.NET game engine. Use when the user asks to make, create, scaffold, implement, port, or add a game or game mechanic with Gondwana, including requests such as "make Pong with Gondwana", "create a platformer", or "add particles/collisions/input to my Gondwana game".
---

# Create a Gondwana Game

Use this skill for game-facing implementation with Gondwana.

The goal is not merely to produce plausible C# game code. Produce code that matches the **current Gondwana public API and intended architecture**.

## Establish authoritative context

Before substantial implementation:

1. If the Gondwana MCP tools are available, call `get_repository_info`.
2. Read `AGENTS.md` from the current public/default Gondwana source unless the user explicitly names another ref.
3. Read `docs/ai/README.md`.
4. Inspect `Tooling/Gondwana.Templates/` for current project/startup conventions.
5. Identify and inspect the closest current demo in `Demos/`.
6. Search/read the source for the public types the implementation will use.
7. Search/read the relevant wiki pages for the intended mental model.

Do not invent an API because a similarly named method would be conventional in another game engine.

If live Gondwana repository access is unavailable, say that exact API verification is unavailable before relying on uncertain signatures.

## Choose the closest existing path

Prefer the nearest working Gondwana example:

- Windows/WinForms game startup: inspect the current WinForms template and Windows demos.
- Avalonia desktop: inspect current Avalonia hosting/examples.
- Blazor/WebAssembly: inspect the current Blazor hosting/examples.
- Platforming/collision gameplay: inspect `Demos/Gondwana.Platformer/`.
- Ship movement/rotation/combat/HUD patterns: inspect `Demos/Gondwana.SpaceDuel/`.
- Particles: inspect `Demos/Gondwana.ParticleTest/`.
- Coordinate systems/projections: inspect `Demos/Gondwana.CoordinateTest/`.
- Small game structure: inspect current compact demos such as `Demos/Gondwana.Flappy/`.
- Widgets: inspect `Gondwana.Widgets/` and `Demos/WidgetsTest/`.

Do not copy a stale demo pattern without checking the engine API it calls.

## Implementation rules

- Use public Gondwana APIs in game code.
- Keep game-specific behavior in the game project.
- Add core-engine behavior only when the requested capability genuinely belongs in Gondwana.
- Preserve the code-first model; do not introduce an editor-owned workflow.
- Respect package boundaries between core, hosting, widgets, adapters, optional packages, tooling, and demos.
- Treat world, layer/grid, and screen coordinates as distinct spaces.
- Account for the intentionally different bitmap and GPU rendering paths when rendering/invalidation is relevant.
- Prefer straightforward sample code over abstraction created only to reduce a few repeated lines.

When the request reveals a missing engine capability, separate the proposed engine change from the game/demo code so the user can evaluate the architectural addition explicitly.

## Deliverables

For implementation requests, provide the smallest useful artifact that matches the user's workflow:

- focused code snippets for small changes,
- complete files for small self-contained examples,
- an apply-ready patch when modifying an existing repository,
- project boilerplate when creating a new game.

Include tests when changing reusable engine behavior. Demo-only gameplay does not need to become a core test unless it exposes a reusable regression.

## Verification

Before claiming an implementation is correct:

1. verify the exact public types/members used,
2. check relevant tests,
3. check the closest demo/template,
4. account for lifecycle/ownership implications,
5. build/test when the environment permits it.

If verification cannot be performed, state what remains unverified rather than presenting guessed API usage as current Gondwana code.
