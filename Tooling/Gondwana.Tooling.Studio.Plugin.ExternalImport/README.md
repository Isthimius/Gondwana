# External Asset Importer — Studio plugin

One Studio panel converts supported Godot 4 atlas TileSets (`.tres`), Tiled
tilesets/maps (`.tsx`/`.tmx`) and Aseprite sprites (`.ase`/`.aseprite`) to native
GTS/GANI/GSCN definitions and generated PNG atlases. Conversion requires no
installed copy of the originating applications.

## Build and deploy

```console
dotnet build Tooling/Gondwana.Tooling.Studio.Plugin.ExternalImport -c Release
dotnet run --project Tooling/Gondwana.Tooling.Studio.WinForms -c Release --no-build
```

Build with the same configuration as Studio. The build copies the plugin DLL,
its `.deps.json`, and `Gondwana.Tooling.Importers.dll` into Studio's `plugins/`
directory. Core/WinForms contract assemblies and engine dependencies are supplied
by Studio; do not deploy private copies. The plugin is not a NuGet package.
Close Studio before rebuilding a loaded DLL. Discovery occurs at startup; use
**View** to recover a hidden panel. Removing the plugin DLL disables this tool
without affecting Studio's native editors.

## Architecture and lifecycle

`ExternalImportPlugin` has a parameterless constructor and implements the current
WinForms plugin interface plus optional `IStudioPluginHostServicesAware`.
`ExternalImportPanel` handles inputs, snapshots, and UI-thread timer polling.
Providers are discovered through the headless registry. Workers capture only an
immutable request, provider and cancellation token; they never access controls.
Changing working directory, cancellation and disposal invalidate pending results.
Abandoned task faults are observed. Cancellation is cooperative between parsing,
rendering and filesystem operations and does not block the UI.

`Gondwana.Tooling.Importers` targets net8.0 without WinForms. Its shared pipeline
owns diagnostics, naming, native validation/serialization, conflict detection,
staging and rollback. Providers contain all foreign-format knowledge. Native
editors and runtime factories remain unaware of foreign formats.

Optional host services refresh the browser, log paths and open a primary native
document after success. Existing plugins need not implement the new interface.
Services are called only from UI-thread completion polling.

## Use and validation

Choose source/output, Analyze, review diagnostics and proposed files, then Import.
Overwrite is opt-in. Errors prevent writing; warnings explain omitted behavior.
See the [support matrix](../../docs/wiki/External-Asset-Importing.md) for exact limits, naming, relative image dependencies and playback semantics.
limits, naming, relative image dependencies and playback semantics.

Headless tests generate their own XML, PNG and binary ASE fixtures; they assert
native definitions, timing, geometry and pixels. Studio integration tests cover
discovery, analysis/import state, overwrite protection, host services and lifecycle.

```console
dotnet test Testing/Gondwana.Tooling.Importers.Tests -c Release
dotnet test Testing/Gondwana.Tooling.Studio.WinForms.Tests -c Release
```
