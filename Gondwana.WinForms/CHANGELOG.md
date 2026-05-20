# Changelog

All notable changes to this project will be documented in this file.


# v2.3.0 - May 20, 2026



## Added
- Expose TargetFps and VSync properties on GpuBackbuffer
- Bring parity between WinFormGpuRenderSurface and WinFormBitmapRenderSurface control/adapter pairs
- Add actual GPU FPS tracking to GpuBackbuffer and CPSCalculated event



## Fixed
- Fix for 2.1.1 patch
- Forward inner GLControl mouse events to outer WinFormGpuRenderSurfaceControl
- Make SpriteManager thread-safe to prevent collection-modified exception



## Tests
- Testing



## Other Changes
- Implement GpuBackbuffer, WinFormGpuRenderSurfaceAdapter, WinFormGpuRenderSurfaceControl
- Address PR review: fallback canvas/snapshot, DrawTileFrame guard, dimension guard, fix resize handler leak, zero-size guard
- Remove _resizeFlag (superseded by _surface null-check), simplify BeginFrame, document resize timing in adapter
- Round 3: raster GpuBackbuffer, adapter disposal, 0x0 resize guard, code style fixes
- Moving project tags explicitly to individual projects
- Option A: move GpuBackbuffer rendering to GL thread
- Address code review feedback: remove unused field, null-safe Canvas, readable Math.Max
- More debugging
- Replace WinForms Timer with Engine.AfterFrameRender for GL invalidation sync
- Throttle BeginInvoke(Invalidate) to at most one pending per GPU frame
- Fix flicker: skip canvas.Clear before full-screen DrawImage in Option A GPU path
- Remove legacy Present path from WinFormGpuRenderSurfaceAdapter
- Replace BeginInvoke with UiDispatcher.Post for GL control invalidation
- Comment clean up
- Merge from master



# v2.1.0 - April 20, 2026



## Fixed
- Fix for arrow keys being seen by the KeyboardAdapter
- Fix for occasional race condition in KeyboardEventPoller



## Refactoring
- Refactoring RenderSurfactHost; simplifying RenderToBackbuffer; namespace organizing



## Other Changes
- Adding Gondwana.WinForms
- KeyboardManager, with WinFormsKeyboardAdapter
- Added XInput for WinForms
- Few more touchups for XInputGamepad
- Log info, and remove dead code
- SoundResource refactoring; still need to smooth out PlatformAudioFactory / temp files
- Minor code organization
- Audio stuff done for now (still needs real-world testing); also creating static EngineExtensions class
- Hmmm... KeyboardHandler not firing
- Okay, got Keyboard firing; need to clean up references, add Manager classes
- Keyboard and Gamepad cleanup
- Comments, mostly
- Namespace fix
- Misc cleanup
- More misc stuff
- Hello, RenderSurfaceHost
- Class renaming, misc updates
- Rendering forms
- Cleaning up Tilesheet and Frame
- Renaming, and new WinForm controls
- Starting to try and wire up revised Puzzle
- This, that, and other things
- Debugging...
- Still debugging; confirmed BitmapBackbuffer is being drawn, rendered when stepping through; logic error somewhere in cycle
- Getting closer...
- More testing and misc adjustments
- Ugh WinForm events... Resize not bubbling up in WinFormBitmapRenderSurfaceControl.cs
- Move Matrix binding from BackbufferBase to RenderSurfaceHost;
- Adding "generic" SDL2 Gamepad support / WinForms implementation
- Modify SDL2 Gamepad support to use SDL_GameController internally
- EngineExtensions
- *** finally got images, but with issues. and it's gnarly. ***
- Semantic cleanup
- Oh we're back, baby...
- Ohmygosh mouse polling across threads works
- UiDispatcher implementation
- Beginning GPU adapter
- Trying to get Resize to fire
- Resize successful! but still exception if resizing too small in Puzzle
- Tidying
- Working on resize backbuffer issue
- More troubleshooting; fix for dirty rectangle bug
- Almost fixed...
- Finally jitter and sizing fixed, with Backbuffer resize
- *** massive CodeMaid cleanup ***
- More random cleanup; marking GPU backbuffer and adapter with NotImplementedException
- Starting CoordinateTest
- CoordinateTest, and it's almost a template
- Camera debugging
- More camera move debugging
- Misc refactor / cleanup; using Keys enum
- Zoom lerp
- The chickens are back
- Tweaks and collision testing
- Last tweaks before breaking the engine...
- The ghost is no longer in the shell
- Clean up; minor debug
- Committing for 2.0.1
- Merge from master v2.0.1
- New test ParticleEmitters, and adapter xml comments
- Moved static Engine input methods to instance of EngineInputSystems
- Spot movement; suddenly an Input class appears
- MouseEventArgs sugar
- Project settings and files for NuGet publication
- Logos and icons
- Per project README files



