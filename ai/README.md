# AI Guide to the Gondwana Repository

This directory is a stable routing layer for coding agents and AI assistants.

It is intentionally **not** a second copy of the Gondwana wiki. The wiki remains the human-facing explanation of the engine, while the repository remains the authority for current implementation details. These files tell an agent where to look, how to resolve conflicts, and how to make changes without fighting Gondwana's architecture.

Start with the root [`AGENTS.md`](../../AGENTS.md).

## The One-Source-of-Truth Model

Use the resource that owns the kind of truth you need:

| Need | Primary source |
| --- | --- |
| What the current branch actually does | Current source code |
| Required behavior and regression guarantees | `Testing/Gondwana.Tests/` |
| Build, test, pack, and CI behavior | `.github/workflows/` and project files |
| Public engine identity and supported packages | `README.md` |
| Architectural explanation and usage documentation | [Gondwana wiki](https://github.com/Isthimius/Gondwana/wiki) |
| Current API signatures | Source code, then the [generated API reference](https://isthimius.github.io/Gondwana/) |
| Working examples of public APIs | `Demos/` and `Tooling/Gondwana.Templates/` |
| Tooling implementation | `Tooling/` |
| Released-history context | `CHANGELOG.md` |
| Planned or exploratory work | `ROADMAP.md`, issues, and discussions |

If two sources conflict about current behavior, follow the precedence in `AGENTS.md`.

## What to Read Next

- [`repository-map.md`](repository-map.md) — where the major runtime, adapter, demo, test, and tooling areas live.
- [`documentation-map.md`](documentation-map.md) — which wiki topics explain a subsystem or concept.
- [`implementation-workflow.md`](implementation-workflow.md) — the preferred investigation, implementation, testing, and demo workflow.

## Guidance for Repository-Aware Assistants

For nontrivial implementation work, do not load the entire repository indiscriminately.

A better sequence is:

1. Identify the relevant public type, subsystem, or demo.
2. Inspect the defining source files.
3. Follow direct collaborators and ownership relationships.
4. Search tests for those types and behaviors.
5. Inspect the nearest demo or template if the task concerns game-facing usage.
6. Consult the wiki to confirm architectural intent.
7. Make the smallest coherent change that satisfies the request.
8. Run targeted tests, then the baseline regression suite when practical.

This keeps the working context focused and reduces the chance of importing unrelated assumptions from another subsystem.

## Repository-Aware Tooling

[`Tooling/Gondwana.Mcp/`](../../Tooling/Gondwana.Mcp/) contains Gondwana's read-only Model Context Protocol server.

The MCP service exposes bounded repository list/read/search tools plus wiki list/read/search tools. It is hard-wired to `Isthimius/Gondwana`, defaults source reads to `master`, and exposes no write-capable GitHub operations.

The service does not redefine documentation or implementation truth. It makes the existing sources reachable to external AI clients: the repository continues to own implementation truth, and the wiki continues to own explanatory documentation.
