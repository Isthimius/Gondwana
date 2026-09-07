# Changelog

All notable changes to this project will be documented in this file.


# [Unreleased]



## Added
- Add hyperlink widget and container refactor ([#252](https://github.com/Isthimius/Gondwana/pull/252))
- Add oblique left coordinate system ([#256](https://github.com/Isthimius/Gondwana/pull/256))
- Add collision adjustment and persistence ([#257](https://github.com/Isthimius/Gondwana/pull/257))
- Add self-contained platformer demo ([#258](https://github.com/Isthimius/Gondwana/pull/258))
- Add spaceduel demo ([#263](https://github.com/Isthimius/Gondwana/pull/263))
- Add collision configuration support for Frame and .gts files ([#264](https://github.com/Isthimius/Gondwana/pull/264))
- Add effect subsystem ([#280](https://github.com/Isthimius/Gondwana/pull/280))
- Add popup and toast overlays ([#281](https://github.com/Isthimius/Gondwana/pull/281))
- Add direct light rendering ([#282](https://github.com/Isthimius/Gondwana/pull/282))
- Add side scroller and zelda prototypes ([#289](https://github.com/Isthimius/Gondwana/pull/289))
- Add DirectSceneLayerDarknessOverlay ([#293](https://github.com/Isthimius/Gondwana/pull/293))
- Add WebGL GPU rendering path ([#307](https://github.com/Isthimius/Gondwana/pull/307))



## Fixed
- Fix hex partial coordinate movement bug ([#251](https://github.com/Isthimius/Gondwana/pull/251))
- Fix zoom calculations and movement timing ([#253](https://github.com/Isthimius/Gondwana/pull/253))
- Fix sprite cloning composition ([#259](https://github.com/Isthimius/Gondwana/pull/259))
- Persist engine config changes ([#260](https://github.com/Isthimius/Gondwana/pull/260))
- Handle image instance layer updates ([#261](https://github.com/Isthimius/Gondwana/pull/261))
- Support custom logger dependency injection ([#270](https://github.com/Isthimius/Gondwana/pull/270))
- Correct view coordinate conversion on GPU path to address occasional tearing ([#290](https://github.com/Isthimius/Gondwana/pull/290))
- Resolve timer edge case ([#295](https://github.com/Isthimius/Gondwana/pull/295))



## Refactoring
- Tighten touch and mouse polling behavior ([#249](https://github.com/Isthimius/Gondwana/pull/249))
- Use fixed-step simulation timing ([#305](https://github.com/Isthimius/Gondwana/pull/305))



## Other Changes
- Standardizing CollisionAdjust and Overlap values

# v2.5.2 - July 26, 2026



## Added
- Add CollisionAdjust to improve collision handling



## Fixed
- Resolve collision behavior and movement checks
- Only show collision boxes for collidable tiles



## Refactoring
- Simplify widget inheritance structure




# v2.5.1 - July 19, 2026



## Added
- Add oblique coordinate system support
- Implement and refine SplashScreen widget
- Implement widget input handling and routing



## Documentation
- Add XML documentation to all undocumented public and protected members




# v2.5.0 - July 09, 2026



## Added
- Add Spot.Blazor WebAssembly demo
- Move SplashScreen and add Gondwana.Widgets
- Add tilesheet provenance document models
- Add base classes for draggable widgets




# v2.4.3 - June 16, 2026



## Fixed
- Remove Blazor WebAssembly SDK imports, stabilize global.json SDK pin, and fix gondwana run wasm



## Other Changes
- Hotfix for Spot serialization runtime error; also working version bump
- Adding option to EngineState serialization to either include TilesheetDefinitions in save file, or as individual gts files.




# v2.4.2 - June 11, 2026



## Added
- Add support for .gts files and tilesheets




# v2.4.1 - June 09, 2026




# v2.4.0 - June 09, 2026



## Refactoring
- Improve Tilesheet and region handling
- Enhance TilesheetRegion structure




# v2.3.0 - May 20, 2026

## Added
- Add the GPU backbuffer and GL-thread rendering path with configurable target FPS, VSync, MSAA, and measured GPU FPS
- Add timer-driven engine execution through `Engine.StartTimerDriven` and `Engine.Tick` for browser hosts
- Add poll-driven touch input, gesture events, and adapter integration through `EngineInputSystems`
- Add engine/Studio plugin infrastructure and runtime loaders for animation, scene, and tilesheet assets
- Add plugin pre-frame, post-frame, and post-scene canvas hooks
- Add first-class SVG assets through `AssetTypes.Svg`, `SvgResource`, and `DirectSvg`
- Add a platform-agnostic splash overlay
- Add collision response helpers that cancel velocity only along blocked axes

## Changed
- Bypass dirty-rectangle accumulation on the GPU path and always render the full GPU surface
- Make `Frame.DurationSeconds` immutable after construction

## Fixed
- Make `SpriteManager`, `RefreshQueue`, and render-surface host access safe across engine and GL threads
- Fall back safely when an MSAA sample count is unsupported or an SKSurface cannot be created
- Correct touch throttling, queue draining, gesture behavior, and adapter disposal ownership
- Resolve Studio integration and runtime asset-loader issues
- Correct SVG bitmap ownership and avoid redundant bitmap copies
- Ensure post-scene canvas hooks invalidate CPU backbuffers and are skipped when no views exist

# v2.0.1 - March 07, 2026

## Packaging
- Set the core package and assembly version to 2.0.1
- Add complete NuGet metadata, a package README and icon, Source Link, symbol packages, and XML documentation output

## Dependencies
- Update SkiaSharp and Microsoft.Extensions dependencies

## Fixed
- Fix for smooth movement
- Fix for startup mis-rendering; removing RecreateBackbufferOnResize
- Fixed TargetFPS not updating
- Fix for SpriteManager _lastTick; fixes "fast" movement
- Fix for the diagsquare
- Fixed DirectDrawing refresh issue
- Fix for tile selection bug; still getting weird culling cutting off when zoomed in
- Fix for dirty rectangle shift bug on camera move
- Fix for tile select; now works with Zoom and Camera shift; still debugging rendering to adapter ui
- Fix for WorldPxToScreenPx; more testing and debugging
- Fixed the ghosting / smearing
- Fix for intermittent null exception on Engine Dispose
- Fix for arrow keys being seen by the KeyboardAdapter
- Fix sprite smearing in all movement instances
- Fixed mismatch in clipping between world space and screen space
- Fix for tile transparency
- Fix for occasional race condition in KeyboardEventPoller
- Fix for full-screen refresh clipping
- Fix for collision drawing
- Fix for drawing DirectDrawings with no visible SceneLayers
- Fix for DirectDrawing follow sprite
- Fix for occasional ghosting when drawing outside of SceneLayer grid range; issue was not with rendering to Backbuffer, rather it was the presentation of the dirty rectangle on the Backbuffer to the UI adapter Canvas
- Fixing build; Collision refactoring
