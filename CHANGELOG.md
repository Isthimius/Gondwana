# Changelog

All notable changes to this project will be documented in this file.

# v2.2.3 - May 05, 2026



## Added
- Add gondwana run and gondwana run wasm commands
- Update SpotAvalonia to use BrowserAudioManager for WASM audio (PR 4)
- Add WASM publish/deploy scripts and gondwana publish wasm command (PR 3)
- Add gondwana-wasm template, gondwana new wasm CLI command, docs (PR 2)
- Add Gondwana.Audio.Browser WASM audio adapter (PR 1)
- Add --include-loader flag to generate-keys command
- Add --password and --encrypt switches to asset/pack commands
- Strip Co-authored-by, Agent-Logs-Url, Signed-off-by from CHANGELOG
- Group CHANGELOG entries by assembly



## Fixed
- Address PR review feedback (App.cs BROWSER guard, duplicate JS, PS5.1 $IsWindows, dead code, doc fix)
- Improve --encrypt error message wording
- Use pull_request event in labeler to avoid manual approval
- Remove extra blank lines between bullet points in cliff.toml template
- Replace unsupported --json flag in gh issue create with URL parsing
- Use \r?\n? in preprocessors to handle both line endings
- Construct PR URL from pr_number instead of non-existent pr_url field
- Restore TAG HANDLING inline comments and mention -PreviewOnly in confirmation prompt
- Align cliff.toml format, harden heading detection, resolve paths from PSScriptRoot
- Use # headings in cliff.toml, align awk extractor, fix preview and first-run changelog
- Fetch origin/master and rebase before pushing CHANGELOG to master
- Push CHANGELOG directly to master instead of via PR in release workflow



## Maintenance
- Add dev install scripts for Gondwana.Cli and Gondwana.Templates
- Run label-PR workflow automatically using pull_request_target
- Add PR auto-labeler workflow and rules
- Update CHANGELOG for v2.2.2 ([#94](https://github.com/Isthimius/Gondwana/pull/94))



## Changed
- feat: WASM audio adapter, gondwana-wasm template, publish scripts, SpotAvalonia audio ([#128](https://github.com/Isthimius/Gondwana/pull/128))
- Add developer install scripts for Gondwana.Cli and Gondwana.Templates ([#127](https://github.com/Isthimius/Gondwana/pull/127))
- feat(cli): add --password and --encrypt switches to asset/pack commands ([#126](https://github.com/Isthimius/Gondwana/pull/126))
- fix: switch Label PR workflow to `pull_request` to remove manual approval gate ([#125](https://github.com/Isthimius/Gondwana/pull/125))
- fix: remove extra blank lines between bullet points in changelog output ([#124](https://github.com/Isthimius/Gondwana/pull/124))
- fix(ci): gh issue create does not support --json flag ([#103](https://github.com/Isthimius/Gondwana/pull/103))
- fix: cliff.toml template crash on commit.remote.pr_url ([#100](https://github.com/Isthimius/Gondwana/pull/100))
- utilize git-cliff for CHANGELOG ([#97](https://github.com/Isthimius/Gondwana/pull/97))
- Use `pull_request_target` for labeler to skip fork approval gate ([#99](https://github.com/Isthimius/Gondwana/pull/99))
- feat(changelog): group CHANGELOG entries by assembly ([#96](https://github.com/Isthimius/Gondwana/pull/96))
- fix: push CHANGELOG directly to master instead of PR merge in release workflow ([#95](https://github.com/Isthimius/Gondwana/pull/95))




# v2.2.2 - May 03, 2026
## What's Changed
### Other Changes
* Add `gondwana help` command to Gondwana.Cli
* fix: always full-render surface when using GpuBackbuffer
* fix(release): clean CHANGELOG bullets, fix separator bleed, harden merge step
* Add MSAA and VSync as configurable EngineConfiguration properties that propagate to GpuBackbuffer
* feat(changelog): include release date in CHANGELOG version heading
* feat: WASM-safe timer-driven engine loop for Avalonia hosting
* fix: GpuBackbuffer throws on Canvas access when MSAA sample count is unsupported
* Add SpotAvalonia: WASM-capable Spot demo using Avalonia adapters and timer-driven engine loop
* fix: ensure Scene is initialized before InitializationComplete fires
* Add top-level `gondwana pack` shorthand command
* SpotAvalonia: show splash screen and player selection instead of auto-starting
* Fix: capture IsChecked on UI thread in SpotAvalonia menu item click handlers
* fix: repair YAML parse error in release.yml caused by unindented Python script
* Fix: register Avalonia Fluent theme so SpotAvalonia menu bar renders
* fix(SpotAvalonia): New Game dialog clips Start/Cancel buttons
* Merge SpotGL into Spot: GPU Acceleration option with persistent settings
* Fix startup crash in Spot: guard AddClouds against uninitialized BackgroundGameField
* fix: music stops immediately on startup in Spot project
* Persist new game dialog selections between sessions
* fix: remove stale NestedProjects entry causing MSB5023 on restore
* feat: add --backbuffer option to gondwana new winforms/avalonia commands
* Spot: fix double-speed moves and index-out-of-bounds when starting a new game mid-computer-turn
* fix: add --force to git fetch --tags in release.ps1

**Full Changelog**: https://github.com/Isthimius/Gondwana/compare/v2.2.1...v2.2.2

---

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