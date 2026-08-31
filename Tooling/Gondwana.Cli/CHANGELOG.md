# Changelog
All notable changes to this project will be documented in this file.


# v2.5.2 - July 26, 2026




# v2.5.1 - July 19, 2026




# v2.5.0 - July 09, 2026



## Added
- Add Spot.Blazor WebAssembly demo
- Gondwana run blazor auto-launches the browser when the dev server is ready
- Enhance blazor deploy with destination mirrors



## Fixed
- Handle .slnx solutions created by .NET 9+ SDK
- Gondwana new always creates .sln (not .slnx) regardless of SDK version



## Refactoring
- Clean up blazor template and README




# v2.4.3 - June 16, 2026



## Fixed
- Fix browser run serving directory listing instead of game
- Restore SpotAvalonia desktop splash and about dialog




# v2.4.2 - June 11, 2026




# v2.4.1 - June 09, 2026




# v2.4.0 - June 09, 2026



## Fixed
- Locate correct index.html serve root for gondwana run
- Handle index under wwwroot in run serve



## Other Changes
- Fix `gondwana publish` desktop argument handling (`MSB1001 --project`) and align docs
- Automate butler installation via itch.io broth CDN
- Debugging gondwana doctor error
- Improve `gondwana doctor` dependency reporting for LibVLC and Gondwana Templates
- Align generated GameHost filename with derived host class name




# v2.3.0 - May 20, 2026

## Added
- Introduce Gondwana CLI as a .NET global tool with doctor, project scaffolding, template, asset, and project-information commands
- Add WinForms, Avalonia, and Blazor scaffolding with selectable backbuffers
- Add commands for running and publishing Blazor projects and deploying builds, including itch.io deployment
- Add single-file desktop publishing
- Add asset-pack overwrite/append modes, password-based encryption, and optional loader generation
- Add `gondwana help`, the top-level `gondwana pack` shorthand, and `gondwana doctor --fix`
- Expand doctor checks to cover Git, Nerdbank.GitVersioning, Gondwana CLI, templates, SkiaSharp, VLC, and browser workloads

## Changed
- Reuse an existing solution when scaffolding projects, or create a holding solution when none exists
- Report generated project, solution, and publish-output locations

## Fixed
- Harden asset command argument handling, path traversal checks, identifier generation, and process exit-code handling
- Correct template and dependency detection across Windows and non-Windows hosts
- Correct Blazor run/publish output handling and tighten deployment validation
- Improve project-file and solution discovery
- Refresh `PATH` correctly after dependency installation and correct the git-cliff package identifier
