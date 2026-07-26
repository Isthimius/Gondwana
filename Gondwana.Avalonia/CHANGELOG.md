# Changelog

All notable changes to this project will be documented in this file.


# v2.5.2 - July 26, 2026




# v2.5.1 - July 19, 2026



## Added
- Implement widget input handling and routing




# v2.5.0 - July 09, 2026




# v2.4.3 - June 16, 2026




# v2.4.2 - June 11, 2026



## Added
- Add support for .gts files and tilesheets




# v2.4.1 - June 09, 2026




# v2.4.0 - June 09, 2026




# v2.3.0 - May 20, 2026



## Added
- Add Gondwana.Avalonia and Gondwana.Avalonia.Hosting projects
- Add AvaloniaGpuRenderSurfaceControl, adapter, and AvaloniaGpuGameHost
- Add touch input adapter with gesture recognizers for Avalonia (Android/iOS)
- Refactor touch to poll-driven pattern matching Mouse
- Add TouchAdapter to EngineInputSystems and wire through Engine.Initialize



## Fixed
- Address code review feedback - ConcurrentQueue, Render dest rect, README accuracy
- Dispose GRBackendRenderTarget per-frame with using in OnOpenGlRender
- Address PR review feedback on touch gesture recognizers and adapter
- Setter owns adapter disposal; callers assign via TouchAdapter only



## Refactoring
- Address code review feedback on gesture recognizers and touch adapter
- Address code review feedback on TouchEventPoller and AvaloniaTouchInputAdapter
- Address remaining code review feedback



## Other Changes
- Update Gondwana.Avalonia/Rendering/AvaloniaGpuRenderSurfaceControl.cs



