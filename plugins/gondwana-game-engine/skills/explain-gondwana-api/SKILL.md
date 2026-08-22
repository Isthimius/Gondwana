---
name: explain-gondwana-api
description: Explains Gondwana APIs, architecture, concepts, and current engine behavior using the official wiki plus live source and tests. Use when the user asks how Gondwana works, what a type/property/subsystem does, why the engine is designed a certain way, how two Gondwana concepts differ, or whether a documented capability exists today.
---

# Explain a Gondwana API or Concept

Use this skill for accurate explanations of Gondwana rather than implementation-first work.

## Use the right source for the question

Gondwana deliberately has different sources of truth for different questions.

Use the **wiki** first for:

- mental models,
- terminology,
- architecture,
- normal game-developer workflows,
- coordinate-space explanations,
- subsystem overviews.

Use **current source and tests** to verify:

- exact APIs and signatures,
- current implementation order,
- ownership,
- inheritance/copy/cache behavior,
- current regression guarantees,
- whether a feature actually exists on the current branch.

Use demos/templates to show how current public APIs are composed in real game code.

Do not treat `ROADMAP.md`, an open issue, old changelog text, or historical documentation as proof that a feature is implemented.

## Workflow

1. If the Gondwana MCP tools are available, call `get_repository_info` for source-sensitive questions.
2. Read/search the relevant wiki topic.
3. Search/read the defining source files for exact implementation claims.
4. Search relevant tests when behavior or ordering matters.
5. Inspect a current demo/template when a usage example would improve the answer.
6. Reconcile the sources using the precedence in `AGENTS.md`.

If documentation and current implementation disagree, say so plainly and identify which describes current behavior.

## Answer style

Start with the conceptual answer, then add implementation detail only as needed.

For code-facing explanations:

- name the relevant Gondwana types,
- distinguish public API from internals,
- identify coordinate space when relevant,
- identify lifecycle/update/render stage when relevant,
- cite or name the relevant source paths and wiki pages when the host supports citations.

Avoid explaining Gondwana by analogy to Unity, Godot, ECS engines, or generic game-engine conventions when Gondwana's own architecture answers the question directly.

## Roadmap versus current behavior

When a question concerns planned work, label it clearly:

- **Implemented now**
- **Partially implemented**
- **Planned**
- **Exploratory/design discussion**

Never silently upgrade planned behavior into a current guarantee.
