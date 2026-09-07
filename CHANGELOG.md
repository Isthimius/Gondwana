# Changelog

All notable changes to this project will be documented in this file.

# [Unreleased]

## Gondwana

### Added
- Add hyperlink widget and container refactor
- Add oblique left coordinate system
- Add collision adjustment and persistence
- Add self-contained platformer demo
- Add spaceduel demo
- Add collision configuration support for Frame and .gts files
- Add effect subsystem
- Add popup and toast overlays
- Add direct light rendering
- Add side scroller and zelda prototypes
- Add DirectSceneLayerDarknessOverlay
- Add WebGL GPU rendering path



### Fixed
- Fix hex partial coordinate movement bug
- Fix zoom calculations and movement timing
- Fix sprite cloning composition
- Persist engine config changes
- Handle image instance layer updates
- Support custom logger dependency injection
- Correct view coordinate conversion on GPU path to address occasional tearing
- Resolve timer edge case



### Refactoring
- Tighten touch and mouse polling behavior
- Use fixed-step simulation timing



### Other Changes
- Standardizing CollisionAdjust and Overlap values

## Gondwana.Avalonia

### Fixed
- Improve bitmap render adapters



### Refactoring
- Tighten touch and mouse polling behavior

## Gondwana.Blazor

### Added
- Add WebGL GPU rendering path



### Refactoring
- Tighten touch and mouse polling behavior

## Gondwana.Blazor.Hosting

### Added
- Add WebGL GPU rendering path

## Gondwana.Widgets

### Added
- Add hyperlink widget and container refactor
- Add menubar and dropdown menu widgets
- Add spaceduel demo
- Add popup and toast overlays



### Refactoring
- Tighten touch and mouse polling behavior

## Gondwana.WinForms

### Fixed
- Improve bitmap render adapters



### Refactoring
- Tighten touch and mouse polling behavior

## Tooling / Gondwana.Cli

### Added
- Add WebGL GPU rendering path
- Align blazor tooling with webgl workflow



### Refactoring
- Unify blazor detection, workload checks, and return values

## Tooling / Gondwana.Mcp

### Added
- Add mcp service and ai plugins
- Add agent plugin compliance docs

## Tooling / Gondwana.Templates

### Added
- Add WebGL GPU rendering path
- Align blazor tooling with webgl workflow

## Tooling / Gondwana.Tooling.Assets.WinForms

### Maintenance
- Rename tooling projects and files

## Tooling / Gondwana.Tooling.Studio.Avalonia

### Maintenance
- Rename tooling projects and files

## Tooling / Gondwana.Tooling.Studio.Core

### Maintenance
- Rename tooling projects and files

## Tooling / Gondwana.Tooling.Studio.WinForms

### Maintenance
- Rename tooling projects and files

## Tooling / Gondwana.Tooling.Tilesheets.WinForms

### Maintenance
- Rename tooling projects and files

## Build / Repository

### Added
- Add menubar and dropdown menu widgets
- Add collision adjustment and persistence
- Add self-contained platformer demo
- Add spaceduel demo
- Add flappy bird demo
- Add mcp service and ai plugins
- Add two user-generated demo games, TheGreatPlop and RageToPro
- Add effect subsystem
- Add side scroller and zelda prototypes



### Fixed
- Add explicit permissions block



### Documentation
- Simplify roadmap description
- Revise roadmap wording
- Refine the README content for clarity and structure
- Add ai-assisted workflow section to repository README
- Update game introduction duration
- Add android/ios support to README Roadmap
- Refresh project overview
- Add support section
- Mark blazor WebGL rendering adapter complete



### CI
- Add sourceforge mirror workflow



### Maintenance
- Rename tooling projects and files
- Remove manual funding configuration



### Other Changes
- Version bump
- Revise README for clarity and feature updates
- Rename Gondwana.Movement to Gondwana.Physics.Movement
- Update GitHub Actions workflow for PR title and body
- Update link text for Engine Wiki in README
- Add Platformer Demo section to README
- Add Space Shooter Demo to README
- Add Discussions link to README
- Refactor support section in README
- Revise support section title and link text
- Update wording in support section of README
- Update funding sources in FUNDING.yml
- Add buy_me_a_coffee funding option
- Add funding usernames to FUNDING.yml
- Yml workflow to mirror to SourceForge
- Run workflow only when pushing to master

# [v2.5.2] - 2026-07-26

## Gondwana

### Added
- Add CollisionAdjust to improve collision handling



### Fixed
- Resolve collision behavior and movement checks
- Only show collision boxes for collidable tiles



### Refactoring
- Simplify widget inheritance structure

## Gondwana.Widgets

### Refactoring
- Simplify widget inheritance structure

## Tooling / Gondwana.Studio

### Added
- Add core and winforms support for Gondwana.Studio
- Add CollisionAdjust to improve collision handling

## Build / Repository

### Added
- Add core and winforms support for Gondwana.Studio



### Fixed
- Only show collision boxes for collidable tiles



### Other Changes
- Version bump

Full Changelog: https://github.com/Isthimius/Gondwana/compare/v2.5.1...v2.5.2

# [v2.5.1] - 2026-07-18

## Gondwana

### Added
- Add oblique coordinate system support
- Implement and refine SplashScreen widget
- Implement widget input handling and routing



### Documentation
- Add XML documentation to all undocumented public and protected members

## Gondwana.Audio.Browser

### Added
- Implement widget input handling and routing

## Gondwana.Audio.Midi

### Added
- Implement widget input handling and routing

## Gondwana.Avalonia

### Added
- Implement widget input handling and routing

## Gondwana.Avalonia.Hosting

### Added
- Implement widget input handling and routing



### Refactoring
- Clean up winforms template structure



### Documentation
- Add XML documentation to all undocumented public and protected members

## Gondwana.Blazor

### Added
- Implement widget input handling and routing



### Documentation
- Add XML documentation to all undocumented public and protected members

## Gondwana.Blazor.Hosting

### Added
- Implement widget input handling and routing



### Documentation
- Add XML documentation to all undocumented public and protected members

## Gondwana.Hosting

### Added
- Implement widget input handling and routing

## Gondwana.Video

### Added
- Implement widget input handling and routing

## Gondwana.Widgets

### Added
- Implement and refine SplashScreen widget
- Implement widget input handling and routing



### Documentation
- Add XML documentation to all undocumented public and protected members

## Gondwana.WinForms

### Added
- Implement widget input handling and routing



### Documentation
- Add XML documentation to all undocumented public and protected members

## Gondwana.WinForms.Hosting

### Added
- Implement widget input handling and routing



### Refactoring
- Clean up winforms template structure



### Documentation
- Add XML documentation to all undocumented public and protected members

## Tooling / Gondwana.Templates

### Added
- Add oblique coordinate system support



### Fixed
- Add transparent icon to VS New Project tiles



### Refactoring
- Clean up winforms template structure

## Build / Repository

### Added
- Implement and refine SplashScreen widget
- Implement widget input handling and routing



### Documentation
- Add XML documentation to all undocumented public and protected members



### Other Changes
- Version bump to 2.5.1

Full Changelog: https://github.com/Isthimius/Gondwana/compare/v2.5.0...v2.5.1

# [v2.5.0] - 2026-07-09

## Gondwana

### Added
- Add Spot.Blazor WebAssembly demo
- Move SplashScreen and add Gondwana.Widgets
- Add tilesheet provenance document models
- Add base classes for draggable widgets

## Gondwana.Audio.Browser

### Added
- Add Spot.Blazor WebAssembly demo

## Gondwana.Avalonia.Hosting

### Added
- Move SplashScreen and add Gondwana.Widgets
- Add base classes for draggable widgets

## Gondwana.Blazor

### Added
- Add Spot.Blazor WebAssembly demo
- Gondwana run blazor auto-launches the browser when the dev server is ready

## Gondwana.Blazor.Hosting

### Added
- Add Spot.Blazor WebAssembly demo
- Move SplashScreen and add Gondwana.Widgets
- Add base classes for draggable widgets

## Gondwana.Hosting

### Added
- Move SplashScreen and add Gondwana.Widgets
- Add base classes for draggable widgets
- Improve template structure and logging

## Gondwana.Widgets

### Added
- Move SplashScreen and add Gondwana.Widgets
- Add base classes for draggable widgets

## Gondwana.WinForms.Hosting

### Added
- Move SplashScreen and add Gondwana.Widgets
- Add base classes for draggable widgets
- Improve template structure and logging

## Tooling / Gondwana.Cli

### Added
- Add Spot.Blazor WebAssembly demo
- Gondwana run blazor auto-launches the browser when the dev server is ready
- Enhance blazor deploy with destination mirrors



### Fixed
- Handle .slnx solutions created by .NET 9+ SDK
- Gondwana new always creates .sln (not .slnx) regardless of SDK version



### Refactoring
- Clean up blazor template and README

## Tooling / Gondwana.Studio

### Added
- Add tilesheet provenance document models
- Add base classes for draggable widgets

## Tooling / Gondwana.Templates

### Added
- Improve template structure and logging



### Fixed
- Sync MyGameHost templates with updated GameHostBase hook API



### Refactoring
- Clean up blazor template and README

## Build / Repository

### Added
- Add Spot.Blazor WebAssembly demo
- Enhance blazor deploy with destination mirrors
- Move SplashScreen and add Gondwana.Widgets
- Improve template structure and logging



### Refactoring
- Clean up blazor template and README



### Other Changes
- Version bump

Full Changelog: https://github.com/Isthimius/Gondwana/compare/v2.4.3...v2.5.0

# [v2.4.3] - 2026-06-16

## Gondwana

### Fixed
- Remove Blazor WebAssembly SDK imports, stabilize global.json SDK pin, and fix gondwana run wasm



### Other Changes
- Hotfix for Spot serialization runtime error; also working version bump
- Adding option to EngineState serialization to either include TilesheetDefinitions in save file, or as individual gts files.

## Gondwana.Avalonia.Hosting

### Fixed
- Remove Blazor WebAssembly SDK imports, stabilize global.json SDK pin, and fix gondwana run wasm

## Gondwana.Blazor

### Added
- Add Gondwana.Blazor project with Blazor WebAssembly rendering and input support
- Add BlazorBitmapRenderSurfaceAdapter and BlazorBitmapRenderSurfaceComponent (canvas-based)
- Add BlazorKeyboardAdapter with BlazorKey enum mapping browser KeyboardEvent.code values
- Add BlazorMouseAdapter for pointer/mouse input
- Add BlazorTouchAdapter for touch input
- Add EngineExtensions for InitializeBlazorKeyboardAdapter, Mouse, Touch
- Add gondwana-blazor.js JS module for canvas putImageData rendering

### Fixed
- Restore SpotAvalonia desktop splash and about dialog

## Gondwana.Blazor.Hosting

### Fixed
- Restore SpotAvalonia desktop splash and about dialog

## Gondwana.WinForms

### Added
- Add missing sounds and improve New Game dialog

## Tooling / Gondwana.Cli

### Fixed
- Fix gondwana run wasm serving directory listing instead of game (Avalonia Browser 11.x)
- Restore SpotAvalonia desktop splash and about dialog

## Tooling / Gondwana.Templates

### Fixed
- Remove Blazor WebAssembly SDK imports, stabilize global.json SDK pin, and fix gondwana run wasm
- Restore SpotAvalonia desktop splash and about dialog

## Build / Repository

### Fixed
- Restore required .NET workloads in CI (master)
- Remove Blazor WebAssembly SDK imports, stabilize global.json SDK pin, and fix gondwana run wasm
- Restore SpotAvalonia desktop splash and about dialog



### Other Changes
- Hotfix for Spot serialization runtime error; also working version bump

Full Changelog: https://github.com/Isthimius/Gondwana/compare/v2.4.2...v2.4.3

# [v2.4.2] - 2026-06-11

## Gondwana

### Added
- Add support for .gts files and tilesheets

## Gondwana.Avalonia

### Added
- Add support for .gts files and tilesheets

## Tooling / Gondwana.Studio

### Added
- Add support for .gts files and tilesheets

## Build / Repository

### Other Changes
- New working version

Full Changelog: https://github.com/Isthimius/Gondwana/compare/v2.4.1...v2.4.2

# [v2.4.1] - 2026-06-09

## Build / Repository

### Maintenance
- Fix release workflow restore configuration



### Other Changes
- Version bump

Full Changelog: https://github.com/Isthimius/Gondwana/compare/v2.4.0...v2.4.1

# [v2.4.0] - 2026-06-09

## Gondwana

### Refactoring
- Improve Tilesheet and region handling
- Enhance TilesheetRegion structure

## Gondwana.Hosting

### Refactoring
- Improve Tilesheet and region handling

## Gondwana.WinForms.Hosting

### Refactoring
- Share host logic in base class

## Tooling / Gondwana.Cli

### Fixed
- Locate correct index.html serve root for gondwana run
- Handle index under wwwroot in run serve



### Other Changes
- Fix `gondwana publish` desktop argument handling (`MSB1001 --project`) and align docs
- Automate butler installation via itch.io broth CDN
- Debugging gondwana doctor error
- Improve `gondwana doctor` dependency reporting for LibVLC and Gondwana Templates
- Align generated GameHost filename with derived host class name

## Tooling / Gondwana.Templates

### Other Changes
- Align generated GameHost filename with derived host class name

## Build / Repository

### Fixed
- Handle index under wwwroot in run serve



### Refactoring
- Improve Tilesheet and region handling
- Enhance TilesheetRegion structure



### CI
- Summarize PR comments across the full PR
- Lock conventional PR titles and append automation summaries
- Replace stale gh-copilot extension with GitHub Models REST API in format-pr-title
- Limit format-pr-title workflow to title updates only
- Generate summary-bullets-only PR body with AI summary



### Other Changes
- Ignore package-lock
- Harden `format-pr-title` GitHub Models auth with token fallback

Full Changelog: https://github.com/Isthimius/Gondwana/compare/v2.3.0...v2.4.0

# v2.3.0 - May 19, 2026

## Added
- Add first-class SVG assets through `AssetTypes.Svg`, `SvgResource`, and `DirectSvg`
- Replace the Spot-specific splash form with a platform-agnostic splash overlay, including fade transitions and a post-fade-in callback
- Add CLI publishing and deployment for desktop and browser projects, including single-file desktop publishing and itch.io deployment
- Reuse an existing solution when scaffolding projects, or create a holding solution when none exists
- Add `RenderBackbufferPostScene` and the `OnPostRenderCanvas` plugin hook
- Add collision response helpers that cancel velocity only along blocked axes
- Add the core engine unit-test project and initial coverage suite

## Fixed
- Harden splash loading, cache decoded images, and prevent GPU-mode fade hangs
- Delay Spot startup visuals and music until the splash completes
- Correct browser publish/run output handling and tighten deployment validation
- Improve project and solution discovery in `gondwana new`
- Correct dependency detection, package IDs, and refreshed `PATH` handling in setup and `gondwana doctor --fix`
- Ensure post-scene canvas hooks invalidate CPU backbuffers and are skipped when no views exist

## Documentation
- Add engine lifecycle documentation and expand the onboarding lifecycle diagram
- Update the CLI publishing and deployment guides with output locations
- Replace the bundled first-game guide with the wiki version and update template links
- Restore wiki pages under `.github/wiki` with current links

## CI
- Use GitHub Copilot CLI for PR-title formatting and harden the workflow scripts
- Enforce the core test project in PR checks and release preflight
- Update master/labeler workflow behavior and CI failure reporting

Full Changelog: https://github.com/Isthimius/Gondwana/compare/v2.2.4...v2.3.0


# v2.2.4 - May 09, 2026

## Added
- Add core engine and Studio plugin infrastructure, Studio editors, and runtime asset loaders
- Add poll-driven touch input with Avalonia gesture recognition and host lifecycle integration
- Add `gondwana doctor --fix` and checks for Git, Nerdbank.GitVersioning, Gondwana CLI, and browser workloads
- Add plugin pre-frame and post-frame render hooks

## Fixed
- Resolve Studio build, editor integration, dock-content, and tilesheet-editor issues
- Correct touch-event throttling, queue draining, and adapter disposal ownership
- Make development and release scripts compatible with PowerShell 5.1 and non-Windows hosts
- Improve dependency detection for SkiaSharp, templates, and VLC
- Resolve changelog paths relative to the repository root and correct generated section boundaries
- Enable Windows targeting consistently during cross-platform restore
- Correct `gondwana run wasm` to publish before serving the application

## Documentation
- Document `gondwana doctor --fix`, development scripts, packages, and core namespaces
- Add CPU and GPU rendering-pipeline diagrams

## Maintenance
- Add `Setup-Gondwana-Dev.ps1` and organize local development scripts

Full Changelog: https://github.com/Isthimius/Gondwana/compare/v2.2.3...v2.2.4

---

# v2.2.3 - May 05, 2026

## Added
- Add the browser audio adapter and use it in SpotAvalonia
- Add the browser project template plus CLI commands for creating, running, publishing, and deploying browser builds
- Add password/encryption options to asset commands and optional loader generation
- Add local development installers for Gondwana CLI and templates

## Fixed
- Correct browser guards and remove duplicate JavaScript wiring
- Improve encryption error reporting
- Harden changelog generation across line endings, headings, preview mode, and first-run releases
- Correct PR URL and issue-creation handling in release automation

## CI
- Add automatic PR labeling without a manual approval gate
- Group generated changelog entries by assembly and strip commit-signature trailers
- Push generated changelog updates directly to the release branch

**Full Changelog**: https://github.com/Isthimius/Gondwana/compare/v2.2.2...v2.2.3

---

# v2.2.2 - May 03, 2026

## Added
- Add configurable MSAA and VSync settings for `GpuBackbuffer`
- Add a timer-driven engine loop suitable for Avalonia browser builds
- Add SpotAvalonia and merge the GPU-enabled Spot variant into the main demo
- Add `gondwana help`, the `gondwana pack` shorthand, and template backbuffer selection

## Fixed
- Always perform full-surface rendering on the GPU path and fall back safely when an MSAA sample count is unsupported
- Initialize the `Scene` before raising `InitializationComplete`
- Correct Spot and SpotAvalonia startup, theme, dialog, menu-threading, audio, and turn-state issues
- Remove a stale solution nesting entry that prevented restore
- Harden the release workflow, changelog formatting, and tag fetching

**Full Changelog**: https://github.com/Isthimius/Gondwana/compare/v2.2.1...v2.2.2

---

# v2.2.1 - April 30, 2026

## Added
- Add Gondwana CLI as a .NET global tool
- Add the Avalonia project template and `gondwana new avalonia` command

## Fixed
- Allow the release workflow to merge its protected release PR through the GitHub API

## Documentation
- Update the README and CLI cheat sheet for v2.2.0 and the Avalonia scaffolding command

**Full Changelog**: https://github.com/Isthimius/Gondwana/compare/v2.2.0...v2.2.1

---

# v2.2.0 - April 29, 2026

## Added
- Move `GpuBackbuffer` rendering to the GL thread
- Add Gondwana.Avalonia and Gondwana.Avalonia.Hosting
- Add the `gondwana-winforms` project template package
- Introduce Gondwana.Studio as a dark-themed, cross-platform Avalonia IDE with dockable windows

## CI
- Automatically prepend GitHub release notes to the repository changelog
- Correct the release-workflow YAML used to generate those notes

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
- Renamed audio API from `LoadFromEngineResourceFile` to `LoadFromEngineAssetsFile`

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
