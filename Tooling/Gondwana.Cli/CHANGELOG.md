# Changelog

All notable changes to this project will be documented in this file.


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
- Add gondwana-avalonia template and CLI command
- Add gondwana help command
- Add --backbuffer option to gondwana new winforms/avalonia commands
- Add --password and --encrypt switches to asset/pack commands
- Add --include-loader flag to generate-keys command
- Add gondwana-blazor template, gondwana new blazor CLI command, docs (PR 2)
- Add browser publish/deploy scripts and gondwana publish blazor command (PR 3)
- Add gondwana run and gondwana run blazor commands
- Add --fix flag to gondwana doctor
- Add Git, nbgv, Gondwana CLI, and wasm-tools checks
- Add missing new blazor, run, run blazor, and publish blazor rows to HelpCommand
- Add publish and deploy CLI commands
- Add --publish-single-file option to gondwana publish (desktop)
- Create holding sln in gondwana new commands
- Reuse existing solution when scaffolding new project



## Fixed
- Add gondwana templates update hint to WinForms and Avalonia new commands on --backbuffer failure
- Improve --encrypt error message wording
- Drop --columns from template check; probe VLC install dirs on Windows
- Detect SkiaSharp installed via NuGet global packages cache
- Tighten IsNuGetPackageCached exception handling; document lowercase convention
- Add missing [DefaultValue] attributes to Configuration options
- Tighten itch deploy validation
- Support wasm publish output directory in run command
- Harden and clarify solution association messages
- Prefer existing solutions and robustly locate csproj
- Add explicit ProcessHelper namespace import
- Gondwana doctor --fix PATH not refreshed after winget install; wrong winget ID in release.ps1



## Refactoring
- Rename holding solution helper to non-Try name



## Documentation
- Add gondwana new avalonia to README and check both templates in doctor
- Update gondwana doctor docs in README and CLICHEATSHEET
- Update CLI publish and deploy guides
- Add output-location notes to publish blazor, deploy, and deploy itch sections



## Maintenance
- Add dev install scripts for Gondwana.Cli and Gondwana.Templates



## Other Changes
- Add Gondwana.Cli .NET global tool with doctor, new, templates, assets, and info commands
- Fix single-char segment crash in ToConstantName and improve CheckSkiaSharp readability
- Move asset type extension mapping to gondwana-asset-types.json config file
- Update Tooling/Gondwana.Cli/Commands/DoctorCommand.cs
- Update Tooling/Gondwana.Cli/Commands/Assets/AssetsPackCommand.cs
- Update Tooling/Gondwana.Cli/Commands/InfoCommand.cs
- Update Tooling/Gondwana.Cli/Commands/Assets/AssetsPackCommand.cs
- Update Tooling/Gondwana.Cli/Commands/InfoCommand.cs
- Update Tooling/Gondwana.Cli/Commands/Assets/AssetsGenerateKeysCommand.cs
- Assets pack: overwrite by default, add --append/-a flag to preserve existing entries
- Apply 5 review comments: ArgumentList, path traversal, identifier sanitization, exitCode check, string escaping
- Fix path-traversal comparison (Ordinal), remove unused lambda param, correct doc comment
- Add top-level `gondwana pack` command alias
- Fix whitespace alignment in HelpCommand for pack entry
- Document gondwana pack shorthand in README and CLICHEATSHEET
- Make type-map optional: embed built-in defaults + fix NuGet pack path for gondwana-asset-types.json
- Fix gondwana run blazor: run via dotnet run (Blazor dev server) instead of dotnet-serve
- Add first-class SVG asset support (`AssetTypes.Svg`) with `SvgResource` and `DirectSvg`
- Make publish blazor emit the publish output path and warn when missing
- Remove duplicate publish output path line in publish blazor output
- Emit publish output path before publish guidance output
- Add git-cliff and butler setup + doctor checks/docs
- Refactor doctor winget fix logic for git-cliff and butler
- Fix doctor --fix early return when no issues off Windows
- Rename doctor always-fix flag for clarity
- Tighten doctor always-fix predicate per item
- Detect Gondwana project references in `gondwana info`
- Improve `gondwana new` output with explicit project and solution locations
- Correct git-cliff winget ID and make butler check-only in setup/doctor flows

