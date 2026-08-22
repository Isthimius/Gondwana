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

## MCP/app binding

The plugin intentionally does not yet contain `.app.json` or `.mcp.json`.

Those files require a real deployed or tunneled Gondwana MCP endpoint and, for an app-backed ChatGPT connection, the actual registered app/connector identifier. Do not commit invented IDs or placeholder production URLs.

Once `Tooling/Gondwana.Mcp` has a reachable HTTPS endpoint and is registered with the target OpenAI environment:

1. add the real app/MCP binding,
2. declare the corresponding `apps` and/or `mcpServers` path in `.codex-plugin/plugin.json`,
3. add the MCP dependency to each skill's `agents/openai.yaml`,
4. validate the complete plugin,
5. test the same user prompts with and without explicit `@Gondwana` invocation.

## Read-only boundary

The Gondwana MCP service is intentionally read-only. The plugin may help an agent write code in the user's own working project when the host product allows it, but the Gondwana repository service itself exposes no branch, commit, pull-request, issue-update, or arbitrary-repository write tools.
