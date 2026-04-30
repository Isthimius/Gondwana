# v2.2.1
<!-- Release notes generated using configuration in .github/release.yml at v2.2.1 -->

## What's Changed
### Other Changes
* fix(release): use REST API to merge release PR, bypassing required-review branch protection by @Copilot in https://github.com/Isthimius/Gondwana/pull/64
* Add Gondwana.Cli .NET global tool by @Copilot in https://github.com/Isthimius/Gondwana/pull/65
* docs: update README for v2.1.0–v2.2.0 by @Copilot in https://github.com/Isthimius/Gondwana/pull/66
* Add gondwana-avalonia dotnet new template and CLI command by @Copilot in https://github.com/Isthimius/Gondwana/pull/67
* Add `gondwana new avalonia` to CLI docs and fix doctor template check by @Copilot in https://github.com/Isthimius/Gondwana/pull/68
* docs: Add missing `gondwana new avalonia` command to CLICHEATSHEET by @Copilot in https://github.com/Isthimius/Gondwana/pull/69

## New Contributors
* @github-actions[bot] made their first contribution in https://github.com/Isthimius/Gondwana/pull/63

**Full Changelog**: https://github.com/Isthimius/Gondwana/compare/v2.2.0...v2.2.1

---
# v2.2.0
<!-- Release notes generated using configuration in .github/release.yml at v2.2.0 -->

## What's Changed
### New Features
* move GpuBackbuffer rendering to the GL thread by @Copilot in https://github.com/Isthimius/Gondwana/pull/52
### Other Changes
* feat: auto-prepend GitHub release notes to CHANGELOG.md on release by @Copilot in https://github.com/Isthimius/Gondwana/pull/57
* fix: YAML parse error in release.yml due to unindented bash heredoc by @Copilot in https://github.com/Isthimius/Gondwana/pull/58
* feat: add Gondwana.Avalonia and Gondwana.Avalonia.Hosting by @Copilot in https://github.com/Isthimius/Gondwana/pull/54
* feat: add `dotnet new gondwana-winforms` template package by @Copilot in https://github.com/Isthimius/Gondwana/pull/56
* feat: add Gondwana.Studio — dark-themed cross-platform Avalonia IDE with dockable windows by @Copilot in https://github.com/Isthimius/Gondwana/pull/55


**Full Changelog**: https://github.com/Isthimius/Gondwana/compare/v2.1.2...v2.2.0

---
# v2.1.2
## Versioning & Packaging
- Bumped engine and package version to **2.1.2**
- Added ONBOARDING.md
- Modified deployment script to preseent option to install missing required npm packages
- Added "Make Your First Game in 15 Minutes" tutorial
- Modified README to include demo previews, and links binary downloads

## Core Engine Enhancements
- Implemented GpuBackbuffer and wired up GPU render surface adapter/control

---
# v2.1.1
## Versioning & Packaging
- Bumped engine and package version to **2.1.1**

---
# v2.1.0
## Versioning & Packaging
- Bumped engine and package version to **2.1.0**
- Updated multiple NuGet dependencies and versioning configuration

## Core Engine Enhancements
- Introduced centralized `EngineManagers` and `EngineInputSystems` aggregation layers:
  - `Engine.Managers` for resource management
  - `Engine.Input` for unified input handling
- Refactored **SpriteManager** from static usage to a singleton instance model
- Improved engine initialization, dispatching, and logging defaults
- Introduced new **Gondwana.Hosting** project:
  - Provides cross-platform host lifecycle abstraction
  - Standardizes engine initialization, input, content loading, and shutdown
- Added **Gondwana.WinForms.Hosting** project:
  - WinForms-specific host implementation
  - Simplifies wiring of input, audio, and rendering for desktop apps
- Removed legacy bootstrap code

## Rendering & Particles
- Extended particle system:
  - Added **per-emitter blend modes**
  - Added **per-particle blend modes**
  - Improved visual flexibility during rendering
- Updated particle rendering pipeline to respect blend modes at draw time
- Added **ImageInstanceLayer** for efficient direct-drawing of reusable/movable bitmap instances
- Introduced **sprite jiggle system** (visual-only offsets and scaling effects)
- Expanded sprite resizing into **pulse/loop behaviors** with completion events
- Improved **TextBlock rendering**:
  - Better wrapping, clipping, and layout fitting
  - Ellipsis support for truncated text

## Input System Updates
- Centralized input handling via `Engine.Input`
- Enhanced mouse input with helper methods and convenience properties
- Refactored gamepad handling:
  - SDL2 support moved into a dedicated `Gondwana.Input.SDL2` project
  - Improved separation of platform-specific input systems

## Assets & Resource Management
- Reworked **AssetsFile**:
  - In-memory buffering of asset data
  - Stream-based asset addition
  - Improved save/load behavior
  - Fallback lookup by base name
- Added **Font asset type** and introduced a centralized **FontManager**
- Renamed audio API (`LoadFromEngineResourceFile` → `LoadFromEngineAssetsFile`)

## Serialization & Data Handling
- Adjusted serialization behavior for:
  - `Scene`, `SceneLayer`, `Tile`, and `Tilesheet`
  - `ValueBag` instances (no longer serialized)
- Added JSON support for `CollisionGroupRegistry`

## Collision System
- Improved handling and serialization of collision groups

## Tilesheets & Drawing
- Improved DirectDrawing infrastructure:
  - Made `DirectDrawingManager` singleton publicly accessible
  - Updated internal registration/keying behavior
  - Defaulted `Nickname` to `Id` when not explicitly set (across drawing types)
- Added disposal support and indexing improvements to `TilesheetRegistry`
- General documentation and structural improvements across drawing systems

## Scene & Layer Enhancements
- Made `SceneLayer` constructor `protected internal` to support external inheritance
- Added `Scene.AddLayer(SceneLayer)` overload for more flexible layer composition
- Improved XML documentation for scene bounds and usage

## Audio & Video
- Enhanced video subsystem documentation and API clarity
- Improved MIDI playback internals:
  - Fixed rendering/seek-forward behavior
  - Expanded SoundFont and reader documentation
- Improved audio playback control:
  - Prevented unintended looping/restart when manually stopping audio
  - Added internal tracking for stop requests
- Updated MIDI/audio dependencies
- Clarified supported audio formats in WinForms adapter

## Documentation Improvements
- Expanded XML documentation across:
  - WinForms input adapters (keyboard, mouse, gamepad)
  - SDL2 and XInput integrations
  - Video playback APIs (LibVLCSharp)
  - MIDI/audio systems
  - Rendering adapters and engine extensions
- Improved clarity of lifecycle, disposal, and polling behaviors

## Spot Demo Game
- Added new **SpotGameHost** using new hosting system
- Rebuilt Spot game structure:
  - SceneLayer-based game field
  - Player model and game state management
  - New game dialog with color selection
- Updated UI:
  - Menu options for new game and audio toggles
  - Improved window initialization and resource handling
- Added new assets (bubble sprites, icon, audio attribution)

---
# v2.0.1
## Versioning & Packaging
- Bumped engine and package version to **2.0.1**
- Updated Nerdbank.GitVersioning configuration (including assembly version)

## NuGet Packaging Improvements
- Enhanced NuGet package metadata:
  - Added README for package display
  - Added package icon
  - Included Source Link support
  - Enabled symbol packages and XML documentation output
- Added supporting package assets for improved distribution and documentation

## Dependencies
- Updated core dependencies:
  - SkiaSharp
  - Microsoft.Extensions.*
  - LibVLCSharp
- Updated WinForms adapter dependency:
  - `SkiaSharp.Views.WindowsForms` to latest version

## Project Updates
- Updated Gondwana core project (`Gondwana.csproj`) with improved packaging configuration
- Updated Gondwana.WinForms and Gondwana.Video project dependencies

---
# v2.0.0
- initial NuGet release