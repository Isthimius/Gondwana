# Gondwana Game Engine Plugin

This directory packages Gondwana-specific workflows for ChatGPT and Codex.

The repository remains the source of truth:

- `AGENTS.md` is the canonical repository-level guidance.
- `docs/ai/` routes agents to the relevant source, tests, demos, templates, and wiki documentation.
- `Tooling/Gondwana.Mcp/` is the read-only MCP server that exposes the official repository and wiki to remote AI clients.
- this plugin packages user-facing Gondwana workflows that consume those sources.

## Included skills

### `create-gondwana-game`

Use when a developer asks to create a game, demo, sample, mechanic, or project using Gondwana.

The workflow verifies current public APIs, checks templates and the closest demo, and prefers game-facing Gondwana abstractions over internal shortcuts.

### `debug-gondwana-game`

Use when a developer asks to diagnose or fix Gondwana game behavior, a Gondwana integration problem, or a suspected engine regression.

The workflow separates game-code problems from engine problems, checks relevant tests and lifecycle/ownership implications, and favors the smallest coherent fix.

### `explain-gondwana-api`

Use when a developer asks how a Gondwana type, subsystem, architecture decision, or workflow behaves.

The workflow uses the wiki for the mental model and verifies exact current behavior against source and tests.

## MCP binding

The plugin declares its read-only Gondwana MCP server in `.mcp.json`:

`https://gondwana-mcp.onrender.com/mcp`

Each bundled skill also declares the same Streamable HTTP MCP dependency in `agents/openai.yaml`. This gives compatible hosts a concrete, public source for current repository and wiki context without requiring an end-user GitHub token.

The MCP service authenticates to GitHub server-side and remains scoped to the official `Isthimius/Gondwana` repository and Gondwana wiki.

## App binding

The plugin intentionally does not yet contain `.app.json`.

An app-backed ChatGPT connection requires the real registered app/connector identifier. Do not commit an invented ID or placeholder value. Add `.app.json` and the corresponding `apps` declaration in `.codex-plugin/plugin.json` only after the Gondwana MCP endpoint has been registered with the target OpenAI environment.

## Read-only boundary

The Gondwana MCP service is intentionally read-only. The plugin may help an agent write code in the user's own working project when the host product allows it, but the Gondwana repository service itself exposes no branch, commit, pull-request, issue-update, or arbitrary-repository write tools.
