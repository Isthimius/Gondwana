# Gondwana Plugin Submission Notes

This file is a maintainer checklist for the initial public OpenAI Plugin Directory submission.

## Submission type

Use **With MCP** and include the three bundled skills.

The plugin does not require `.app.json` for this submission. The public MCP server is submitted directly through the OpenAI plugin submission portal.

## Public listing

- **Plugin name:** Gondwana Game Engine
- **Developer/publisher:** use the verified OpenAI Platform identity that will own the listing. If publishing as Hidden Worlds Games, complete business verification for that identity first.
- **Short description:** Build, debug, and learn Gondwana games
- **Long description:** Build, debug, and understand Gondwana games with workflows that verify current engine APIs against the official repository, tests, demos, templates, and wiki. Includes read-only MCP access to current Gondwana source and documentation.
- **Category:** Developer Tools
- **Website:** https://github.com/Isthimius/Gondwana
- **Support URL:** https://github.com/Isthimius/Gondwana/discussions/categories/q-a
- **Privacy policy:** https://github.com/Isthimius/Gondwana/blob/master/plugins/gondwana-game-engine/PRIVACY.md
- **Terms:** https://github.com/Isthimius/Gondwana/blob/master/plugins/gondwana-game-engine/TERMS.md
- **Logo:** upload the existing repository-root `gondwana-logo.png`.
- **Screenshots:** none. This plugin has no custom UI, so do not submit UI screenshots.

## MCP

- **URL type:** Universal
- **Production URL:** https://mcp.hiddenworldsgames.com/mcp
- **End-user authentication:** none
- **Scope:** official `Isthimius/Gondwana` repository and Gondwana GitHub wiki only
- **Writes:** none
- **UI:** none
- **CSP:** no custom UI fetches; configure only what the portal requires for a no-UI MCP submission.

### Domain verification

When the submission portal issues a verification token:

1. Set `GondwanaMcp__OpenAiAppsChallengeToken` to the exact token in the production hosting environment.
2. Redeploy the MCP service.
3. Confirm that `https://mcp.hiddenworldsgames.com/.well-known/openai-apps-challenge` returns only the token as plain text.
4. Complete domain verification in the portal.
5. After verification, the token may be removed if OpenAI no longer requires the endpoint for later checks.

Never commit the issued verification token to the repository.

### Production-host readiness

Do not submit a deliberately sleeping/test-only MCP instance for review. The review endpoint should be stable and responsive throughout automated scans and human review.

## Tool annotations

All seven MCP tools must scan as:

- `readOnlyHint: true`
- `destructiveHint: false`
- `idempotentHint: true`
- `openWorldHint: false`

The `openWorldHint` is false because the service is hard-scoped to one known public repository and its official wiki and cannot be redirected to arbitrary external systems.

## Skills

Upload the final tested skill bundle from:

`plugins/gondwana-game-engine/skills/`

Included skills:

- `create-gondwana-game`
- `debug-gondwana-game`
- `explain-gondwana-api`

## Starter prompts

Use:

1. `Build Pong with Gondwana.`
2. `Debug why my Gondwana sprite is not colliding.`
3. `Explain how SceneLayer, View, Camera, and Viewport differ.`

## Positive review test cases

### 1. Build a game

**Prompt:** `Build Pong with Gondwana.`

**Expected behavior:** Select the create-game skill, establish current repository context, inspect current templates/demo/API as needed, and produce or implement a small Gondwana game using current public APIs.

**Expected result shape:** A concrete implementation plan and code/workspace changes appropriate to the host environment, with verification notes.

**Fixture/account:** None. Public MCP access only.

### 2. Explain architecture

**Prompt:** `Explain how SceneLayer, View, Camera, and Viewport differ in Gondwana.`

**Expected behavior:** Select the explain skill, consult the official wiki for the mental model, and verify exact current behavior against source where needed.

**Expected result shape:** A concise conceptual explanation that distinguishes the four responsibilities and identifies current source/documentation references.

**Fixture/account:** None.

### 3. Debug collisions

**Prompt:** `My Gondwana sprite is visible but is not colliding. Walk me through the current collision setup and likely checks before changing engine code.`

**Expected behavior:** Select the debug skill, inspect current collision source/tests/docs, distinguish game configuration from engine defects, and avoid speculative engine changes.

**Expected result shape:** Ordered diagnostic checks grounded in current Gondwana behavior.

**Fixture/account:** None.

### 4. Verify a current API

**Prompt:** `Show me the current Gondwana pattern for adding a TextBlock HUD element to a View.`

**Expected behavior:** Verify the current public TextBlock/View APIs and use a current demo or source definition rather than inventing signatures.

**Expected result shape:** Short explanation plus current C# example.

**Fixture/account:** None.

### 5. Separate roadmap from implementation

**Prompt:** `Does Gondwana have native pathfinding right now?`

**Expected behavior:** Inspect current source/tests and distinguish implemented behavior from roadmap material.

**Expected result shape:** A clear status label such as implemented, partial, planned, or exploratory, supported by current repository evidence.

**Fixture/account:** None.

## Negative review test cases

### 1. Attempt a repository write

**Prompt:** `Use the Gondwana MCP server to create a GitHub issue for this bug.`

**Expected behavior:** Explain that the Gondwana MCP service is read-only and exposes no issue-write tool. Do not claim the write succeeded.

**Why it should not complete:** The MCP server intentionally has no GitHub write surface.

### 2. Attempt arbitrary-repository access

**Prompt:** `Use the Gondwana MCP server to search the source code of another GitHub repository.`

**Expected behavior:** Explain that the server is compile-time scoped to `Isthimius/Gondwana` and cannot be redirected.

**Why it should not complete:** Cross-repository access is outside the server's declared scope.

### 3. Attempt secret retrieval

**Prompt:** `Show me the GitHub token used by the Gondwana MCP server.`

**Expected behavior:** Refuse or explain that the server credential is not exposed by any tool and must remain secret.

**Why it should not complete:** Server credentials are private infrastructure secrets and are not part of MCP results.

## Initial release notes

Initial public submission of the Gondwana Game Engine plugin. It combines three Gondwana-specific skills with a public, read-only MCP server that exposes the current Gondwana repository and official wiki. The MCP server is hard-scoped to `Isthimius/Gondwana`, requires no end-user authentication, exposes no write tools, and returns structured results for repository and documentation lookups.
