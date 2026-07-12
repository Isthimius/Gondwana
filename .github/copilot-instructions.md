# Copilot Instructions

## Project Guidelines
- Gondwana framework is designed to be declarative and fall-through (event-driven). Blocking patterns using Application.DoEvents() loops or Thread.Yield() are anti-patterns and should be avoided except in very rare cases. Prefer event callbacks and declarative flow over blocking waits.