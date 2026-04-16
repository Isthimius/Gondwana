# v2.1.0
## Versioning & Packaging
- Bumped engine and package version to **2.1.0**
- Updated multiple NuGet dependencies and versioning configuration

## Core Engine Enhancements
- Introduced centralized engine systems:
  - `Engine.Managers` for resource management
  - `Engine.Input` for unified input handling
- Refactored **SpriteManager** from static usage to a singleton instance model
- Added `EngineManagers` and `EngineInputSystems` aggregation layers
- Improved engine initialization, dispatching, and logging defaults

## Rendering & Visual Systems
- Added **ImageInstanceLayer** for efficient direct-drawing of reusable/movable bitmap instances
- Introduced **sprite jiggle system** (visual-only offsets and scaling effects)
- Expanded sprite resizing into **pulse/loop behaviors** with completion events
- Improved **TextBlock rendering**:
  - Better wrapping, clipping, and layout fitting
  - Ellipsis support for truncated text
- Applied jiggle effects at render time for better performance

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
- Added disposal support and indexing improvements to `TilesheetRegistry`
- General documentation and structural improvements across drawing systems

## Audio
- Updated MIDI/audio dependencies
- Clarified supported audio formats in WinForms adapter

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
