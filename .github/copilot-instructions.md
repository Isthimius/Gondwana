# Copilot Instructions

## Project Guidelines
- Gondwana framework is designed to be declarative and fall-through (event-driven). Blocking patterns using Application.DoEvents() loops or Thread.Yield() are anti-patterns and should be avoided except in very rare cases. Prefer event callbacks and declarative flow over blocking waits.

## Repository Agent Guidance

Read and follow the root [`AGENTS.md`](../AGENTS.md), particularly its C# Formatting and Code Generation section, for all generated and modified C# code. Use the root `.editorconfig` for supported formatting settings. The agent guidance is canonical for layout conventions not fully enforced by the formatter.
