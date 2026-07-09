# Changelog

All notable changes to this project will be documented in this file.

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
- Replace SpotSplashForm with platform-agnostic DirectImage splash
- Add publish and deploy CLI commands
- Update setup script to refresh tool/template/workload installs
- Add --publish-single-file option to gondwana publish (desktop)
- Create holding sln in gondwana new commands
- Reuse existing solution when scaffolding new project
- Add RenderBackbufferPostScene event and OnPostRenderCanvas plugin hook



## Fixed
- Add log warnings to SplashScreen.TryCreate for missing/invalid image file
- Delay Spot startup visuals/music until post-splash and hold Gondwana splash for 3s
- Make gpu acceleration restart prompt owned by Spot window
- Cache splash image as SKImage and fix disposing event handler type
- Decode splash asset once and cache as SKImage
- Tighten itch deploy validation
- Support wasm publish output directory in run command
- Remove redundant git rm --cached in sync-wiki workflow
- Update stale first-game-in-15-minutes.md links across all templates and Gondwana.Templates README
- Harden and clarify solution association messages
- Prefer existing solutions and robustly locate csproj
- Add explicit ProcessHelper namespace import
- Standardise color spelling in new XML docs and lifecycle doc
- Git-cliff PATH not refreshed after winget install; wrong winget ID in release hint
- Gondwana doctor --fix PATH not refreshed after winget install; wrong winget ID in release.ps1



## Refactoring
- Rename holding solution helper to non-Try name



## Documentation
- Add optional ps1 setup section to ONBOARDING.md
- Add InitializeGameContent to ONBOARDING.md lifecycle diagram
- Update CLI publish and deploy guides
- Add output-location notes to publish wasm, deploy, and deploy itch sections
- Replace first-game-in-15-minutes.md with wiki page; add CLI method tutorial
- Make Method B in first-game guide fully standalone



## CI
- Swap Anthropic API for GitHub Copilot CLI in format-pr-title workflow



## Maintenance
- Refine workload update messaging in setup script



## Other Changes
- Add first-class SVG asset support (`AssetTypes.Svg`) with `SvgResource` and `DirectSvg`
- Fix SVG bitmap ownership and DirectSvg disposal issues
- Avoid redundant bitmap copies in DirectSvg
- Clarify DirectSvg bitmap ownership in dispose path
- Add engine lifecycle documentation
- Clarify engine lifecycle document introduction
- Add TOC to engine-lifecycle.md
- Add Spot startup splash with fade in/out and init overlay
- Harden Spot splash startup exception handling
- Fix Spot splash fade hang in GPU mode
- Adding gondwana-logo-text
- Including new image in assets/
- Add splash post-fade-in callback
- Refine splash callback docs
- Clarify splash callback comment
- Make publish wasm emit AppBundle path and warn when missing
- Remove duplicate AppBundle path line in publish wasm output
- Emit AppBundle path before publish guidance output
- Bump for next version
- Add recovered wiki pages to .github/wiki with updated links
- Removing defunct one-time use files
- Add core Gondwana unit test project and initial coverage suite
- Refine core test coverage and verify solution tests
- Add explicit Gondwana namespace import in TypedValueBag tests
- Fix remaining test-review issues in CoreUtility and Timer tests
- Ensure post-scene canvas hooks invalidate CPU backbuffers
- Skip post-scene hooks when no views exist
- Update CI master and labeler PR workflow behavior
- Refine CI failure gate condition for unit test step
- Add git-cliff and butler setup + doctor checks/docs
- Refactor doctor winget fix logic for git-cliff and butler
- Fix doctor --fix early return when no issues off Windows
- Rename doctor always-fix flag for clarity
- Tighten doctor always-fix predicate per item
- Updating cliff.toml
- Fix malformed `format-pr-title` workflow YAML by hardening multiline script blocks
- Detect Gondwana project references in `gondwana info`
- Enforce Gondwana.Tests in PR checks and release pre-flight
- Migrate CoordinateTest demo from BitmapBackbuffer to GPU backbuffer path
- Add repository ROADMAP.md generated from open issues with grouped, collapsible ticket details
- Improve `gondwana new` output with explicit project and solution locations
- Add local `Gondwana.Cli` reinstall script under `Solution Items/scripts`
- Correct git-cliff winget ID and make butler check-only in setup/doctor flows
- Collision detection helper



Full Changelog: https://github.com/Isthimius/Gondwana/compare/v2.2.4...v2.3.0


# v2.2.4 - May 09, 2026



## Added
- Add studio editors and runtime asset loaders
- Add core engine/studio plugin infrastructure
- Add missing new wasm, run, run wasm, and publish wasm rows to HelpCommand
- Add Git, nbgv, Gondwana CLI, and wasm-tools checks
- Wire touch adapter into GameHost lifecycle same as keyboard and mouse
- Add TouchAdapter to EngineInputSystems and wire through Engine.Initialize
- Refactor touch to poll-driven pattern matching Mouse
- Add touch input adapter with gesture recognizers for Avalonia (Android/iOS)
- Add --fix flag to gondwana doctor



## Fixed
- Set trim=false and add trailing blank line to fix section boundary
- Filter merge commits and clean up changelog format
- Scope git-cliff to repository root
- Resolve studio build issues and finalize editor integration
- Add missing [DefaultValue] attributes to Configuration options
- Resolve ChangelogPath and CliffConfigPath relative to repo root in release.ps1
- Tighten IsNuGetPackageCached exception handling; document lowercase convention
- Detect SkiaSharp installed via NuGet global packages cache
- Drop --columns from template check; probe VLC install dirs on Windows
- Wrap Where-Object results in @() to avoid .Count on scalar under strict mode
- Add UTF-8 BOM to remaining ps1 files with non-ASCII characters
- Add UTF-8 BOM to Setup-Gondwana-Dev.ps1 for PowerShell 5.1 compatibility
- Safe IsWindows check for PS 5.1; use Invoke-Cmd for winget in step 10
- Set EnableWindowsTargeting unconditionally in Directory.Build.props
- Enable Windows targeting for non-Windows restore in Directory.Build.props
- TouchEnded always drains before throttle; _lastEventTick advances only on emitted events
- Setter owns adapter disposal; callers assign via TouchAdapter only
- Add EnableWindowsTargeting to Directory.Build.props for cross-OS restore
- Address PR review feedback on touch gesture recognizers and adapter



## Refactoring
- Address remaining code review feedback
- Address code review feedback on TouchEventPoller and AvaloniaTouchInputAdapter
- Address code review feedback on gesture recognizers and touch adapter



## Documentation
- Update gondwana doctor docs in README and CLICHEATSHEET
- Add README for Solution Items/scripts folder
- Update Packages and Core Namespaces in README
- Add gondwana doctor --fix to CLICHEATSHEET



## Maintenance
- Address validation feedback and finalize implementation
- Add Setup-Gondwana-Dev.ps1 and update scripts README



## Other Changes
- Adding new .puml files to sln
- Add separate BitmapBackbuffer and GpuBackbuffer flowchart PUML files
- Improve XML docs on new interfaces
- Add XML documentation for public/protected members in touched C# files
- OnPreFrameRender and OnPostFrameRender to IEnginePlugin
- Remove dead reference
- Refine ViewLocator content resolution semantics
- Optimize ViewLocator reflection path and remove redundant null pattern
- Fix Studio Dock tab content resolution to prevent black editor panels
- Make Frame.DurationSeconds readonly for consistency with other fields
- Fix black content panels, add named tiles filter, per-frame durations, AllowDrop, plugin resolver, and other review fixes
- Add NotifyCanExecuteChangedFor to ImagePath, TileWidth, TileHeight for RebuildGridCommand
- Fix PlantUML repeat syntax in rendering pipeline flowchart
- Add rendering-pipeline-flowchart.puml with CPU and GPU rendering lines
- Changed --> Other Changes
- Sln maint
- Fix .Count on scalar in strict mode for .NET SDK check
- Script organization
- Moving .puml to correct folder
- Add PlantUML diagrams to documentation folder
- Making dir structure match sln
- Moving old .puml to old folder
- Document recognizer lifetime ownership on TouchEvent
- Add GestureType, GestureEventArgs, and TouchEvent on TouchEventPoller
- Add TouchAdapter setter to EngineInputSystems and wire through Engine.Initialize
- Fix gondwana run wasm: publish then serve via dotnet-serve instead of dotnet run
- Version bump
- CHANGELOG cleanup
- Clean up CHANGELOG v2.2.3



Full Changelog: https://github.com/Isthimius/Gondwana/compare/v2.2.3...v2.2.4

---

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

**Full Changelog**: https://github.com/Isthimius/Gondwana/compare/v2.2.2...v2.2.3

---

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
* docs: update README for v2.1.0ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã¢â‚¬Å“v2.2.0 by @Copilot in https://github.com/Isthimius/Gondwana/pull/66
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
* feat: add Gondwana.Studio ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â dark-themed cross-platform Avalonia IDE with dockable windows by @Copilot in https://github.com/Isthimius/Gondwana/pull/55


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
- Renamed audio API (`LoadFromEngineResourceFile` ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ `LoadFromEngineAssetsFile`)

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
