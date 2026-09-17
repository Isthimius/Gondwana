Gondwana provides an official plugin for ChatGPT and Codex that helps AI tools work with the current Gondwana engine instead of relying on generic game-engine assumptions or stale examples.

The plugin can help you:

- create games, demos, and mechanics using current Gondwana APIs
- debug Gondwana game code and engine behavior
- explain engine types, subsystems, and architecture
- inspect the current public Gondwana source code and tests
- consult the official Gondwana wiki when documentation is useful

You do **not** need to clone the Gondwana repository, configure a GitHub token, or manually connect an MCP server to use the published plugin.

---

## Installing the Gondwana plugin

> [!NOTE]
> The Gondwana Game Engine plugin is currently awaiting approval for public
> listing in OpenAI's Plugin Directory.
>
> Until that listing is approved and published, the plugin is not generally
> available for public installation. The instructions below describe the
> installation process that will apply once the public listing is available.

The Gondwana plugin is designed to work without requiring you to clone the
Gondwana repository, create a GitHub token, or manually configure its MCP
server.

Once **Gondwana Game Engine** is publicly available in OpenAI's Plugin Directory, installation is straightforward for users whose ChatGPT account or workspace supports plugins.

### Install it in ChatGPT

1. Open **ChatGPT**.

2. Open **Plugins** from the ChatGPT interface to enter the Plugin Directory.

3. Search for:

   **Gondwana Game Engine**

4. Select **Gondwana Game Engine** from the search results.

5. Review the plugin details, then select **Install plugin**.

6. Wait for installation to complete.

That's it.

The Gondwana plugin does **not** require you to sign in to GitHub or provide a
GitHub personal access token. Its Gondwana repository and documentation access
is handled by the plugin's public, read-only MCP service.

After installation, you can use Gondwana from ChatGPT when asking
Gondwana-related questions. Where the current ChatGPT interface provides plugin
selection, you can also explicitly select or mention **Gondwana Game Engine**.

For example:

```text
Using Gondwana, explain how SceneLayer, View, Camera, and Viewport differ.
```

### Install it in Codex

1. Open **Codex**.

2. Open **Plugins** and find **Gondwana Game Engine**.

3. Select the plugin and choose **Install plugin** if it is not already
   installed.

4. Open the Gondwana game project you want to work with.

5. When starting a Codex task, open **Sources**.

6. Select **Use plugins**.

7. Find and select **Gondwana Game Engine**.

Codex can now use the Gondwana-specific workflows and current engine context
while working on your project.

For example:

```text
Make a simple Pong game using Gondwana and WinForms.
```

or:

```text
My Sprite renders correctly but is not colliding. Diagnose the problem using
the current Gondwana APIs.
```

You do not need to tell Codex where the Gondwana source code lives, configure
the Gondwana MCP endpoint yourself, or check out the engine repository beside
your game.

### If you cannot find Gondwana

First, make sure you are searching the **Plugin Directory**, not the general
ChatGPT conversation search.

If **Gondwana Game Engine** still does not appear:

1. Confirm that the public Gondwana plugin listing is available.
2. Refresh or restart ChatGPT or Codex.
3. Check whether plugins are available for your account, workspace, and role.
4. If you are using a managed workspace, ask your administrator whether the
   Gondwana plugin is available to your role.

Plugin availability can vary by ChatGPT plan, workspace configuration, role,
region, and product surface. Changes to the Plugin Directory may also take time
to appear in Codex.

For general information about plugin installation and availability, see
OpenAI's official
**[Plugins in ChatGPT and Codex](https://help.openai.com/en/articles/20001256/)**
documentation.

---

## Using Gondwana in Codex

Codex is the best option when you want the AI to actually create or modify a Gondwana project.

### 1. Open your game project

Open the folder or repository containing your Gondwana game as a Codex project.

This can be:

- an existing Gondwana game
- a project created from a Gondwana template
- a new project you want Codex to help build

You do not need the Gondwana engine source checked out beside your game project.

### 2. Make sure the Gondwana plugin is installed

Open **Plugins** and confirm that **Gondwana Game Engine** appears in your installed plugins.

The plugin includes Gondwana-specific workflows for creating games, debugging problems, and explaining the engine.

### 3. Ask for what you want

You can normally just describe the task naturally.

For example:

```text
Make Pong with Gondwana.
```

```text
Add a health bar above each enemy ship.
```

```text
My sprite is visible but isn't colliding. Help me figure out why.
```

```text
Explain how SceneLayer, View, Camera, and Viewport differ.
```

```text
Show me the current Gondwana pattern for adding a TextBlock HUD element.
```

Codex can recognize that the request concerns Gondwana and use the appropriate Gondwana workflow automatically.

You do not normally need to specify the name of a skill or tell Codex how to search the Gondwana source.

---

## Using Gondwana in ChatGPT

The same plugin can also be used for questions, design work, API explanations, and debugging discussions in ChatGPT.

After installing the plugin, you can invoke it from ChatGPT by selecting it from the available tools/plugins or by mentioning it in your prompt when supported by the current interface.

For example:

```text
Explain how collisions are configured for a Sprite in Gondwana.
```

```text
What is the difference between world space and screen space in Gondwana?
```

```text
How should I structure a simple WinForms Gondwana game?
```

```text
Does Gondwana currently support native pathfinding?
```

For implementation questions, the plugin can inspect the current public engine source and documentation before answering.

This is particularly useful for APIs that have changed over time.

---

## Creating a game from scratch

You can also use Codex to build a small Gondwana game from a very short request.

For example:

```text
Make a simple Breakout clone with Gondwana.
```

The Gondwana workflow is designed to inspect the current engine APIs, templates, and relevant demos before generating the implementation.

A typical workflow may include:

1. examining the current Gondwana project structure
2. checking the closest existing demo or template
3. verifying the public APIs needed for the game
4. creating or modifying files in your project
5. building the project
6. fixing compile errors or integration problems
7. running relevant tests or validation where appropriate

You remain in control of file changes and commands through the normal Codex approval mechanisms.

---

## Debugging an existing game

The plugin is also useful when something in a Gondwana game is not behaving as expected.

Instead of starting with a broad engine rewrite, the debugging workflow attempts to determine whether the problem is in:

- your game code
- engine configuration
- scene or layer setup
- rendering
- input
- collisions
- timing or lifecycle behavior
- the Gondwana engine itself

For example:

```text
My sprite moves correctly but doesn't wrap when it leaves the SceneLayer.
```

or:

```text
My DirectDrawing TextBlock is visible at 1080p but positioned incorrectly at 4K.
```

Providing the relevant code, error message, or behavior description will usually produce a much more targeted result.

---

## Asking about the Gondwana API

You can use the plugin as a documentation assistant as well.

For example:

```text
How does MovementController work?
```

```text
When should I use a View instead of another SceneLayer?
```

```text
How does the GPU rendering path differ from the bitmap path?
```

```text
Show me how collision settings flow from a Tilesheet Region to a Sprite.
```

The plugin can use both:

- the current Gondwana source and tests
- the official Gondwana wiki

The source and tests are treated as authoritative for current behavior, while the wiki provides the intended architecture and mental model.

---

## What the Gondwana service can access

The plugin's remote Gondwana service is deliberately limited.

It can read and search:

- the public `Isthimius/Gondwana` GitHub repository
- the official Gondwana GitHub wiki

It cannot use that service to:

- create commits
- create branches
- open or modify GitHub issues
- create pull requests
- modify the Gondwana repository
- browse arbitrary GitHub repositories
- retrieve private GitHub data
- expose the server's GitHub credentials

The Gondwana service itself is **read-only**.

Codex may still create or modify files in **your local project** when you ask it to and approve those actions. That capability comes from Codex, not from the Gondwana repository service.

---

## You do not need a GitHub token

The official Gondwana plugin connects to the Gondwana repository through the public Gondwana service.

You do not need to provide:

- a GitHub personal access token
- your GitHub account
- repository credentials
- an MCP configuration file

The service is already configured for the official Gondwana repository and documentation.

---

## Plugin not appearing?

The Plugin Directory is available across ChatGPT plans, but installation and use can depend on your plan, workspace settings, role, region, and the product surface you are using.

If you cannot find or install Gondwana:

1. confirm that you are looking in the **Plugin Directory**
2. restart or refresh ChatGPT or Codex
3. check whether your workspace administrator restricts plugin installation
4. in a managed workspace, ask an administrator to confirm that the Gondwana plugin is available for your role

Changes to the Plugin Directory can also take some time to appear in Codex.

---

## Plugin installed, but not being used?

If a Gondwana-specific request appears to be answered without using the plugin, explicitly select or mention **Gondwana Game Engine** and repeat the request.

For example:

```text
Using the Gondwana Game Engine plugin, explain how SceneLayer wrapping works.
```

In Codex, also make sure your Gondwana game folder is the active project when asking for changes to your code.

---

## Good prompts

You do not need elaborate prompt engineering.

Describe the result you want and include relevant constraints.

Good:

```text
Make a two-player Pong game using Gondwana and WinForms.
```

Better when requirements matter:

```text
Add collisions between the player ship and NPC ships.
Keep the existing AABB collision system.
Add a particle explosion when a ship dies.
Do not change the rendering architecture.
```

For debugging, include the actual behavior:

```text
The game compiles and the sprite renders, but CollisionType is -1 after I create the Sprite from this Frame.
```

The more concrete the problem, the more useful the engine-specific inspection can be.

---

## Keeping answers current

Gondwana continues to evolve.

The plugin is intentionally designed to consult the current public engine source rather than depending only on information learned when the AI model was trained.

For source-sensitive questions, it can inspect the current repository, tests, demos, templates, and documentation before answering.

This makes prompts such as:

```text
What is the current way to do this in Gondwana?
```

particularly useful when working with an API that may have changed between releases.

---

## Privacy, terms, and support

The Gondwana AI service reads only the public Gondwana repository and wiki and does not require an end-user GitHub login.

For additional information, see:

- [Plugin Privacy Policy](https://github.com/Isthimius/Gondwana/blob/master/plugins/gondwana-game-engine/PRIVACY.md)
- [Plugin Terms](https://github.com/Isthimius/Gondwana/blob/master/plugins/gondwana-game-engine/TERMS.md)
- [Gondwana Discussions](https://github.com/Isthimius/Gondwana/discussions)
- [Gondwana Issues](https://github.com/Isthimius/Gondwana/issues)

For general Gondwana development documentation, return to the [Gondwana Wiki](https://github.com/Isthimius/Gondwana/wiki).
