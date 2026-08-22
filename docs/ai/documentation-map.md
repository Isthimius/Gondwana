# Gondwana Documentation Map for AI Assistants

The public wiki is Gondwana's primary human-facing documentation:

https://github.com/Isthimius/Gondwana/wiki

Generated API reference:

https://isthimius.github.io/Gondwana/

Use the wiki for mental models, terminology, intended usage, and architectural explanations. Use current source and tests to verify exact present-day behavior and signatures.

The page titles below match the wiki's current organization. Navigate or search the wiki by title rather than guessing from general game-engine terminology.

## Introduction and Architecture

Start with:

- **Home**
- **Make Your First Game in 1 Hour**
- **Engine Architecture Overview**
- **Gondwana Engine Lifecycle**
- **Gondwana CLI Cheatsheet**

Use these first when the question is broad: project structure, engine lifecycle, what owns what, or how a Gondwana game is normally assembled.

## Core Concepts

Relevant pages include:

- **Assets Files**
- **Tilesheets**
  - **.gts Files**
- **Scenes and SceneLayers**
  - **Coordinate Spaces**
  - **Coordinate Systems**
- **Sprites**
- **Views, Cameras, and Viewports**
  - **Parallax and Multi-View Rendering**
  - **Using Views and Cameras**
- **DirectDrawing**
  - **Particles**
  - **ImageInstanceLayer**
  - **Widgets**
    - **Creating Your Own Custom Widget**
- **Game State Files**
- **Logging**

Use these for ownership, coordinate semantics, scene composition, asset/frame behavior, drawing primitives, widgets, and game-facing APIs.

## Rendering

Relevant pages include:

- **Backbuffers**
  - **Refresh Queues**
  - **Dirty Rectangles**
- **Rendering Pipeline**
  - **GL Rendering Path**
  - **Bitmap Rendering Path**

When a rendering question crosses multiple areas, also consult **Coordinate Spaces**, **Views, Cameras, and Viewports**, and **Understanding Skia Rendering in Gondwana**.

Do not assume the CPU bitmap and GPU paths use the same invalidation or presentation strategy.

## Systems

Relevant pages include:

- **Movement and Controllers**
  - **Moving Sprites and Direct Drawings**
- **Input Handling**
  - **Understanding Gondwana Input**
  - **Keyboard Input Quick Start**
  - **Mouse Input Quick Start**
  - **Gamepad Input Quick Start**
  - **Touch Input Quick Start**
- **Collision Detection**
- **Timing and Ticks**
- **Engine Configuration**
  - **Configuration Settings**

For behavior-sensitive work, pair these pages with searches in `Testing/Gondwana.Tests/`.

## Advanced Topics

Relevant pages include:

- **Serialization and EngineState**
- **Understanding Skia Rendering in Gondwana**
- **Custom Platform Adapters and Render Surfaces**
- **Performance Tuning**
- **Debugging and Instrumentation**
- **Adding a New Deployable Project**
- **GitHub Automation and Workflows**
- **Tooling Scripts**

Use these for implementation and operational details that sit below ordinary game-facing usage.

## Choosing Between Wiki, API Reference, and Source

Use the **wiki** when you need to answer:

- Why is the engine structured this way?
- Which abstraction should a game developer use?
- Which coordinate space is intended here?
- What is the normal workflow?

Use the **generated API reference** when you need a quick inventory of public members or XML-documentation output.

Use **current source and tests** when you need to answer:

- What does this property or method do on this branch right now?
- In what order are these operations performed?
- Is this behavior inherited, cached, copied, or recomputed?
- What regression behavior is guaranteed?
- Does this roadmap or wiki statement already exist in code?

If a public-facing answer depends on an exact API signature, verify it against current source even if the wiki shows an example.
