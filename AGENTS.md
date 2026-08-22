# Gondwana Agent Guidance

This file is the repository-level entry point for coding agents and AI assistants working with Gondwana.

Gondwana is a code-first, cross-platform 2D and 2.5D game and rendering engine for C# and .NET 8. It is intentionally object-oriented rather than ECS-based, and its tooling is supplemental: Gondwana games remain ordinary .NET projects whose behavior is expressed in code.

For the AI-oriented repository map, documentation routes, and implementation workflow, start with [`docs/ai/README.md`](docs/ai/README.md).

## Before Making a Nontrivial Change

1. Read `docs/ai/README.md`.
2. Inspect the current implementation related to the request.
3. Inspect existing tests for the same subsystem or behavior.
4. Inspect the closest current demo or template when the task concerns public usage.
5. Consult the Gondwana wiki for the intended mental model and architectural explanation.
6. Prefer an existing Gondwana abstraction over introducing a parallel one.
7. Validate the result with the repository's normal build and test path.

Do not answer implementation questions from generic game-engine assumptions when the repository can establish the actual Gondwana behavior.

## Source-of-Truth Precedence

When sources disagree, use this order:

1. Current source code on the working branch
2. Current automated tests
3. Current project/build/workflow configuration
4. Repository-level documentation such as `README.md`
5. Gondwana wiki and generated API reference
6. `CHANGELOG.md`
7. `ROADMAP.md`, issues, discussions, and other forward-looking material

The wiki explains the intended model, but source and tests determine what the current branch actually does.

Do not treat a roadmap item, issue acceptance criterion, old changelog entry, or historical wiki wording as evidence that a feature currently exists.

If code and documentation materially disagree, preserve the current implementation unless the task explicitly calls for changing it, and identify the documentation drift.

## Architectural Guardrails

Preserve these project-level principles unless the requested work intentionally changes them:

- **Code first.** Tooling assists development but does not own game behavior or project structure.
- **Object-oriented core.** Gondwana is not a mandatory Entity Component System.
- **World space first.** Gameplay state, movement, collisions, and scene logic operate in world coordinates; views and cameras transform that state for presentation.
- **Layered scenes.** `Scene`, `SceneLayer`, `View`, `Camera`, and `Viewport` have distinct responsibilities.
- **Adapters at the edges.** Platform-neutral behavior belongs in core/runtime packages; WinForms, Avalonia, and Blazor concerns belong in their adapter or hosting packages.
- **Explicit lifecycle.** Initialization, update, rendering, dispatch, and shutdown behavior should remain inspectable rather than hidden behind implicit magic.
- **Rendering strategies may differ by backbuffer.** CPU bitmap rendering uses dirty-region behavior where appropriate; GPU-backed rendering uses its own full-viewport path. Do not force architectural symmetry when the hardware paths intentionally differ.
- **Modular packages.** Avoid unnecessary dependencies between core, hosting, widgets, adapters, audio/input/video packages, tooling, and demos.
- **Predictable composition.** Ownership, draw order, timing, and invalidation should remain explicit and debuggable.

See the wiki's **Engine Architecture Overview** and the repository `README.md` for the public architectural description.

## Change Discipline

When modifying engine code:

- Follow naming, nullability, accessibility, and formatting patterns in neighboring code.
- Preserve public API behavior unless the task explicitly requires a breaking change.
- Keep platform-specific code out of `Gondwana/` unless the existing architecture already places that concern there.
- Prefer focused changes over broad refactors unrelated to the request.
- Add or update tests for behavior changes and regressions when practical.
- Search for serialization, cloning, caching, lifecycle, disposal, and event-order implications when changing stateful engine types.
- Check both CPU and GPU implications when touching rendering or invalidation.
- Check world-space, layer-space, and screen-space assumptions when touching coordinates, cameras, views, collisions, or input.
- Do not manually edit generated documentation output merely to make it agree with code.

## Demos and Game Boilerplate

When creating or changing sample game code:

- Treat `Demos/` and `Tooling/Gondwana.Templates/` as the primary examples of current public usage.
- Use public Gondwana APIs instead of reaching into engine internals just to make a sample work.
- Choose the closest demo for the requested mechanic rather than combining patterns indiscriminately.
- For Windows-focused examples, inspect the current WinForms hosting path and Windows demos.
- For cross-platform desktop examples, inspect Avalonia hosting.
- For browser examples, inspect the current Blazor/WebAssembly path.
- If a demo and engine API disagree, verify the current engine API and fix or avoid stale demo usage rather than copying it.

## Validation

The `master` CI workflow uses Release builds with Windows targeting enabled. Mirror it when practical:

```console
dotnet workload restore Gondwana.sln /p:EnableWindowsTargeting=true
dotnet restore --nologo /p:Configuration=Release /p:EnableWindowsTargeting=true
dotnet build --configuration Release --no-restore --nologo /p:EnableWindowsTargeting=true
dotnet test --configuration Release --no-build --no-restore --nologo /p:EnableWindowsTargeting=true Testing/Gondwana.Tests/Gondwana.Tests.csproj
```

For a small targeted change, running the relevant test subset first is fine, but the full `Gondwana.Tests` project is the baseline regression suite.

## Documentation

Human-facing documentation lives primarily in the public Gondwana wiki:

https://github.com/Isthimius/Gondwana/wiki

Generated API reference:

https://isthimius.github.io/Gondwana/

Use [`docs/ai/documentation-map.md`](docs/ai/documentation-map.md) to route a topic to the appropriate wiki section before searching blindly.
