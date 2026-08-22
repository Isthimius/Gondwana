# Gondwana Implementation Workflow for AI Assistants

Use this workflow for patches, refactors, bug fixes, demo work, and implementation advice.

The goal is not ceremony. The goal is to avoid solving the right problem in the wrong layer.

## 1. Establish the Actual Target

Identify:

- the requested behavior,
- the target platform or platforms,
- whether the change belongs to engine code, hosting, an adapter, widgets, tooling, or a demo,
- whether the request is a bug fix, new behavior, public API change, or sample-only change.

Do not broaden the scope merely because neighboring code could also be cleaned up.

## 2. Trace the Current Behavior

Before designing a replacement:

1. Find the public or internal type named in the request.
2. Find its owner and lifecycle.
3. Follow direct collaborators that materially affect the behavior.
4. Search for event ordering, caching, serialization, disposal, and threading where applicable.
5. Search tests for the same types and expected behavior.
6. Check the closest demo if the behavior is exposed to game code.

For rendering, also trace the backbuffer and platform presentation path.

For collisions and movement, also trace update order and coordinate-space assumptions.

For widgets and input, also trace registration, focus/capture, hit testing, and host wiring.

## 3. Consult Architectural Intent

Use the wiki after the current behavior is understood.

This is deliberate: source prevents an agent from proposing against an outdated mental model, while the wiki prevents an agent from "fixing" intentional architecture merely because a different design would also work.

If the wiki and source disagree materially, report the disagreement and use the source/test behavior as the current contract unless the task explicitly asks to change it.

## 4. Design the Smallest Coherent Change

Prefer:

- extending an existing abstraction,
- preserving ownership boundaries,
- explicit behavior over hidden side effects,
- narrowly scoped public API additions,
- tests that describe the intended contract.

Avoid:

- introducing a second mechanism for something Gondwana already models,
- moving platform concerns into core for convenience,
- forcing CPU and GPU rendering paths into artificial symmetry,
- adding editor/tooling requirements to runtime projects,
- creating generic infrastructure solely to remove a few lines from one demo.

## 5. Account for Common Cross-Cutting Effects

Before finalizing an engine change, ask whether it affects:

- initialization order,
- update/render order,
- world/layer/screen coordinate conversion,
- dirty-region invalidation,
- GPU full-viewport rendering,
- serialization/deserialization,
- object cloning/copying,
- animation frame changes,
- collider attachment or collision metadata,
- scene/layer ownership,
- view registration,
- input polling or widget routing,
- disposal/resource ownership,
- thread or dispatcher affinity,
- platform adapter behavior.

Only investigate the items relevant to the changed subsystem; this is a checklist against accidental blind spots, not a requirement to touch every area.

## 6. Tests

For a behavior change:

1. Preserve existing relevant tests unless the contract intentionally changes.
2. Add a focused regression test for the new or fixed behavior.
3. Prefer tests at the lowest layer that can prove the contract.
4. Add integration coverage when the behavior depends on ownership or lifecycle across multiple types.
5. Keep demo behavior out of the unit-test contract unless the demo exposes a reusable engine regression.

Baseline suite:

```console
dotnet test --configuration Release --no-build --no-restore --nologo /p:EnableWindowsTargeting=true Testing/Gondwana.Tests/Gondwana.Tests.csproj
```

The full CI-aligned build sequence is documented in the root `AGENTS.md`.

## 7. Demos and Boilerplate

When generating a game or demo:

1. Inspect `Tooling/Gondwana.Templates/` for current project startup conventions.
2. Pick the closest existing demo for mechanics and platform usage.
3. Use Gondwana's public APIs.
4. Keep game-specific logic in the game project unless a genuinely reusable engine capability is missing.
5. If the task reveals a missing engine primitive, distinguish that core change clearly from sample code.
6. Favor understandable sample code over abstracting every repeated line.

A demo should teach how Gondwana is intended to be used, not merely prove that an internal implementation can be coerced into producing the desired pixels.

## 8. Documentation Impact

Update or flag documentation when a change modifies:

- a public API,
- architecture or ownership,
- lifecycle/update ordering,
- rendering behavior,
- coordinate semantics,
- serialization formats,
- tooling commands,
- recommended game-development workflow.

Do not duplicate full wiki articles inside `docs/ai/`. Update the owning human-facing documentation and keep this AI routing layer concise.
