# Changelog

All notable changes to this project will be documented in this file.

# [Unreleased]



## Added
- Set TargetFPS = 0 in original Spot demo for comparison
- Replace SpotSplashForm with platform-agnostic DirectImage splash
- Add support for .gts files and tilesheets ([#209](https://github.com/Isthimius/Gondwana/pull/209))
- Add missing sounds and improve New Game dialog ([#211](https://github.com/Isthimius/Gondwana/pull/211))
- Move SplashScreen and add Gondwana.Widgets ([#225](https://github.com/Isthimius/Gondwana/pull/225))
- Add base classes for draggable widgets ([#226](https://github.com/Isthimius/Gondwana/pull/226))
- Improve template structure and logging ([#230](https://github.com/Isthimius/Gondwana/pull/230))
- Add oblique coordinate system support ([#235](https://github.com/Isthimius/Gondwana/pull/235))
- Implement and refine SplashScreen widget ([#239](https://github.com/Isthimius/Gondwana/pull/239))



## Fixed
- Guard AddClouds against null BackgroundGameField before game starts
- Guard SetMusicEnabled against playing already-playing music on startup
- Cancel pending computer-move timers when starting a new game in Spot
- Guard against empty moves list in computer turn timer callback
- Delay Spot startup visuals/music until post-splash and hold Gondwana splash for 3s
- Make gpu acceleration restart prompt owned by Spot window
- Remove Blazor WebAssembly SDK imports, stabilize global.json SDK pin, and fix gondwana run wasm ([#212](https://github.com/Isthimius/Gondwana/pull/212))
- Support custom logger dependency injection ([#270](https://github.com/Isthimius/Gondwana/pull/270))



## Refactoring
- Improve Tilesheet and region handling ([#186](https://github.com/Isthimius/Gondwana/pull/186))



## Tests
- Testing



## Other Changes
- Folder reorg
- Unused import remove
- Changing namespace HWG to Gondwana.Demos
- Namespace updates to match directory reorg
- Putting the icon back
- Making demo and tooling explicitly not packable
- Velcro on SpriteMoveStarted; I'll see if it grows on me
- Fix CS8632: enable nullable in Spot.csproj and fix resulting nullable warnings
- Use local sprite variable in event handlers for clarity
- Warnings
- Commenting out trace write
- Merge from master
- Merge SpotGL into Spot: add GPU Acceleration option with settings persistence
- Address code review: named constants, single config read, simplify else-if
- Fix design-time guard, double-dispose, and _particleSurface null-out after ClearAll
- Merge remote-tracking branch 'origin/copilot/add-gpu-acceleration-option' into copilot/add-gpu-acceleration-option
- Persist new game dialog selections between games
- Add clarifying comments for combo box offset constants
- Add Spot startup splash with fade in/out and init overlay
- Harden Spot splash startup exception handling
- Fix Spot splash fade hang in GPU mode
- Adding gondwana-logo-text
- Including new image in assets/
- Make GPU acceleration restart dialog reliably visible when disabling GPU mode ([#185](https://github.com/Isthimius/Gondwana/pull/185))
- Adding option to EngineState serialization to either include TilesheetDefinitions in save file, or as individual gts files.
- Fix link to Gondwana Game Engine in README ([#271](https://github.com/Isthimius/Gondwana/pull/271))
