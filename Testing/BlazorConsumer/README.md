# Blazor package-consumer regression

Run with PowerShell 7, Node 22+, SDK 8 (repository build), and the latest serviced
SDK 10. From a directory outside the repository's `global.json` scope, select
SDK 10 and install `dotnet workload install wasm-tools`. On Linux install
Chromium's OS dependencies with `npx --yes playwright@1.56.1 install-deps chromium`.
Then, from the repository:

```powershell
./Testing/BlazorConsumer/Run.ps1
```

The script reports its temporary work directory. Use a fresh `-WorkRoot` outside
the checkout to retain logs at a known location. No generated projects, package
feeds, browser downloads or npm installations belong in source control. The
temporary directory is deliberately retained for diagnosis; remove it after use.

The test packs the six required runtime packages, CLI and distributed templates
with the repository SDK 8, then selects SDK 10 with an independent `global.json`.
It verifies `wasm-tools` is installed for that SDK, installs the local CLI and
templates, generates `net10.0-browser` via `gondwana new blazor`,
and pins every Gondwana reference to the actual packed NBGV version. An isolated
NuGet cache and package source mapping prevent published Gondwana packages from
satisfying the restore. Third-party dependencies come from NuGet.org.

Assertions cover package/nuspec metadata, resolved package assets, actual MSBuild
Compile items, Debug build, Release build/publish, and absence of a manually copied audio
module. Playwright/Chromium uses ANGLE SwiftShader and tests both the published
output (under `/game/`) and the development server. Startup must remove the
template loader, create a sized Gondwana canvas, load WASM and import audio,
and continuously submit native Skia WebGL draws and allocate GPU textures. Page errors,
console errors and failed asset requests fail the test. HTTP 200 or a canvas
alone cannot pass. Browser traces, screenshots, console/network logs and MSBuild
binary logs remain in the temporary directory.

## v2.6.2 audit

- The template explicitly enables `WasmBuildNative`. Clean SDK 10 CI passed
  published Skia/WebGL startup but reproduced `libSkiaSharp` loading failure
  with development hosting when native linking was left implicit. Both hosting
  paths must pass the browser test; publish success alone is insufficient.
- Clean .NET 10 testing found that the merged `None` audio item is copied to
  build/publish output but is absent from the static-web-assets manifest: the
  Blazor DevServer returns 404 for `/gondwana-audio.js`. Plain `Content` registers
  an incorrect consumer-source content root and returns an empty 200 response in
  SDK 10 development hosting. The audio package therefore uses the Razor SDK's
  static-web-asset pipeline (like Gondwana.Blazor), preserving the root module URL.
  The demo's now-conflicting manual copy is removed. The consumer test guards
  MIME type, nonempty content, actual startup import and exclusion from C#
  compilation; no consumer workaround is used. Its library target remains .NET 8.
- WinForms stays `net8.0-windows`; Avalonia stays `net8.0`. Neither uses the WASM
  native-link toolchain, so this issue does not justify desktop retargeting.
  Framework support lifecycle and desktop upgrades belong in a separate review.
- The templates package itself stays `net8.0`; its template files are packed as
  content. The solution's existing release `dotnet pack` includes the updated
  Blazor project without requiring release packaging to build a .NET 10 app.
- CLI generation delegates to `dotnet new gondwana-blazor`; framework selection
  and publish output paths are derived from the generated project. The only
  hard-coded Blazor SDK 8 references in CLI code were help examples. Existing
  tests for SDK 8 consumers remain useful backward-compatibility coverage.
- The automated generator entry point is the packaged CLI's `gondwana new blazor`,
  which invokes the installed, distributed `dotnet new` template.

### HarfBuzz resolution

The repository's restored graph reproduces the reporter's versions:
`Svg.Skia 4.6.0` requests `HarfBuzzSharp` and Linux/Win32/macOS native packages
at minimum `7.3.0.3`. `SkiaSharp.HarfBuzz 3.119.2` requests managed
`HarfBuzzSharp 8.3.1.3`, which in turn raises Win32/macOS to `8.3.1.3` but does
not depend on the Linux package. NuGet consequently leaves Linux at `7.3.0.3`.
These edges can be inspected in `obj/project.assets.json` and package nuspecs.

This is expected NuGet minimum-version resolution, **not proof of native ABI
compatibility**. Linux native assets are not the browser's Skia WASM library and
cannot explain `libSkiaSharp` failing to load in WASM. The mixed managed/native
HarfBuzz versions merit a separate Linux text-shaping test before any forced
upgrade. This empty-template test does not exercise HarfBuzz text shaping, and
does not certify Linux native compatibility. No HarfBuzz versions are changed.

## Published-release verification (v2.6.2)

Issue #531 was closed as completed on November 4, 2026. The following remains a regression-verification checklist for the published packages:

1. Update the CLI (`dotnet tool update --global Gondwana.Cli`) and templates
   (`dotnet new update`). Confirm the installed versions include v2.6.2.
2. In a fresh directory using SDK 10 and its `wasm-tools`, run
   `gondwana new blazor MyGame` (or `dotnet new gondwana-blazor -n MyGame`).
3. Restore, build, run and Release-publish without copying JavaScript or excluding
   contentFiles. Verify `net10.0-browser` and matching SDK/workload selection.
4. Confirm the loader disappears, a WebGL canvas initializes, audio imports, and
   no `libSkiaSharp` or `renderSurfaceHost` exception appears in the console.

This test does not publish packages, tag a release, or close the issue.
