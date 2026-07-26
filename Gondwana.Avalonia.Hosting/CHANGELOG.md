# Changelog

All notable changes to this project will be documented in this file.


# v2.5.2 - July 26, 2026




# v2.5.1 - July 19, 2026



## Added
- Implement widget input handling and routing



## Refactoring
- Clean up winforms template structure



## Documentation
- Add XML documentation to all undocumented public and protected members




# v2.5.0 - July 09, 2026



## Added
- Move SplashScreen and add Gondwana.Widgets
- Add base classes for draggable widgets




# v2.4.3 - June 16, 2026



## Fixed
- Remove Blazor WebAssembly SDK imports, stabilize global.json SDK pin, and fix gondwana run wasm




# v2.4.2 - June 11, 2026




# v2.4.1 - June 09, 2026




# v2.4.0 - June 09, 2026




# v2.3.0 - May 20, 2026



## Added
- Add Gondwana.Avalonia and Gondwana.Avalonia.Hosting projects
- Add Engine.StartTimerDriven/Tick and AvaloniaGameHost WASM support
- Add AvaloniaGpuRenderSurfaceControl, adapter, and AvaloniaGpuGameHost
- Wire touch adapter into GameHost lifecycle same as keyboard and mouse



## Fixed
- Use TimeSpan.Zero for DispatcherTimer interval on WASM path



## Other Changes
- Update Gondwana.Avalonia.Hosting/AvaloniaGameHost.cs



