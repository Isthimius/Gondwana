---
name: debug-gondwana-game
description: Diagnoses and fixes Gondwana game, integration, and engine behavior using current source, tests, demos, and documentation. Use when the user reports a bug, failing test, rendering/input/collision/movement issue, unexpected lifecycle behavior, compilation problem, regression, or asks why Gondwana code is not behaving as expected.
---

# Debug a Gondwana Game

Use this skill to diagnose Gondwana behavior before proposing a fix.

The central question is: **which layer actually owns the problem?**

## Establish current behavior

For a nontrivial problem:

1. If the Gondwana MCP tools are available, call `get_repository_info`.
2. Read `AGENTS.md` and `docs/ai/README.md`.
3. Inspect the source named by the user or implicated by the symptom.
4. Follow direct collaborators and lifecycle/ownership relationships.
5. Search `Testing/Gondwana.Tests/` for the affected type or behavior.
6. Inspect the closest current demo when the symptom is game-facing.
7. Consult the relevant wiki page after understanding current code.

Treat current source and tests as authoritative for current behavior. Use the wiki to understand intended design.

## Classify the problem

Explicitly determine whether the defect is primarily in:

- the user's game code,
- demo/sample code,
- a public Gondwana API,
- engine internals,
- a platform adapter,
- hosting/lifecycle integration,
- widgets/input routing,
- tooling/assets,
- documentation that has drifted from current code.

Do not change engine internals merely to make incorrect game usage appear to work.

## Trace the relevant implications

Investigate only the cross-cutting areas that matter to the symptom, including:

- initialization and attachment order,
- engine-cycle versus frame-render timing,
- event ordering,
- world/layer/grid/screen coordinate conversion,
- camera/view/viewport ownership,
- dirty-region invalidation,
- GPU full-viewport rendering,
- collider registration and collision profile/mask resolution,
- animation/frame-derived state,
- serialization/deserialization,
- caching/copying,
- input polling, widget focus/capture, and hit testing,
- dispatcher/thread affinity,
- native/platform presentation,
- disposal and resource ownership.

Do not shotgun-edit all of these. Use the checklist to avoid missing a relevant coupling.

## Fix discipline

Prefer the smallest fix that restores the intended contract.

- Preserve public behavior unless the task intentionally changes it.
- Keep platform-specific fixes in the appropriate adapter/hosting package.
- Do not force bitmap and GPU paths into artificial symmetry.
- Avoid unrelated cleanup while fixing a regression.
- Add a focused regression test when engine behavior changes.
- Preserve an existing test unless the requested contract intentionally supersedes it.
- If the documentation is stale but code/tests are correct, fix or flag the documentation rather than regressing the implementation.

## Explain the diagnosis

When reporting a fix, distinguish:

1. **Observed behavior**
2. **Root cause**
3. **Why the proposed change belongs in this layer**
4. **Other implications checked**
5. **Regression coverage**
6. **Anything still unverified**

For a patch request, generate an apply-ready focused patch instead of a prose-only list of edits.
