# Changelog

All notable changes to this project will be documented in this file.


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


