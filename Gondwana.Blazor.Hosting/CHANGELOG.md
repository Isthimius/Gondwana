# Changelog

All notable changes to this project will be documented in this file.


# v2.4.3 - June 16, 2026



## Fixed
- Restore SpotAvalonia desktop splash and about dialog



## Added
- Add Gondwana.Blazor.Hosting project with BlazorGameHost base class
- BlazorGameHost wires keyboard, mouse, and touch adapters for the Blazor render surface
- Timer-driven engine loop via PeriodicTimer for browser/WASM targets
- Standard background-thread engine loop for non-browser (Blazor Server) targets


