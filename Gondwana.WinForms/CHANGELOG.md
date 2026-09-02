# Changelog

All notable changes to this project will be documented in this file.


# [Unreleased]



## Fixed
- Improve bitmap render adapters



## Refactoring
- Tighten touch and mouse polling behavior

# v2.5.2 - July 26, 2026




# v2.5.1 - July 19, 2026



## Added
- Implement widget input handling and routing



## Documentation
- Add XML documentation to all undocumented public and protected members




# v2.5.0 - July 09, 2026




# v2.4.3 - June 16, 2026



## Added
- Add missing sounds and improve New Game dialog




# v2.4.2 - June 11, 2026




# v2.4.1 - June 09, 2026




# v2.4.0 - June 09, 2026




# v2.3.0 - May 20, 2026

## Added
- Add `GpuBackbuffer`, `WinFormGpuRenderSurfaceControl`, and the corresponding adapter
- Move GPU rendering to the GL thread
- Expose target FPS and VSync controls and report measured GPU FPS
- Bring the GPU and bitmap render-surface control/adapter pairs to feature parity

## Fixed
- Forward inner GL-control mouse events through the outer render-surface control
- Correct resize, zero-size, fallback-surface, disposal, and frame-invalidation behavior on the GPU path
- Prevent GPU-path flicker and throttle invalidation to one pending request per frame
- Make `SpriteManager` iteration thread-safe
# v2.1.0 - April 20, 2026

## Added
- Add `WinFormsKeyboardAdapter.GetKeyFromString` for case-insensitive conversion from configured key names

## Changed
- Route WinForms engine extensions through the instance-based `Engine.Input` APIs
- Move SDL2 gamepad integration into the dedicated Gondwana.Input.SDL2 package

## Documentation
- Expand XML documentation for WinForms keyboard, mouse, gamepad, audio, and rendering adapters

## Packaging
- Add package-specific NuGet metadata, README, and icons
