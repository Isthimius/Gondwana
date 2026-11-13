# External Asset Importer — Studio plugin

One Studio panel converts supported Godot 3.x and 4.x TileSets (`.tres`), Tiled
tilesets/maps (`.tsx`/`.tmx`) and Aseprite sprites (`.ase`/`.aseprite`) to native
GTS/GANI/GSCN definitions and generated PNG atlases. Conversion requires no
installed copy of the originating applications.

## Build and deploy

```console
dotnet build Tooling/Gondwana.Tooling.Studio.Plugin.ExternalImport -c Release
dotnet run --project Tooling/Gondwana.Tooling.Studio.WinForms -c Release --no-build
```

Build with the same configuration as Studio. The build deploys `plugin.json`,
the entry DLL and its `.deps.json`, and `Gondwana.Tooling.Importers.dll` to
Studio's `plugins/ExternalImport/` directory. Studio supplies the contracts,
engine, SkiaSharp, and Newtonsoft.Json. To deploy manually, copy the complete package directory
into the distribution's `plugins/` directory and restart Studio. To remove the
plugin, close Studio and remove that package directory.

The plugin contributes an **External Asset Importer** panel. Use the
**Plugins** menu to show it and **View** to recover a hidden panel. See the
[Studio plugin guide](https://github.com/Isthimius/Gondwana/wiki/Gondwana-Studio#plugins)
for manifest compatibility and deployment details.

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

Headless tests generate their own XML, PNG and binary ASE fixtures; they assert
native definitions, timing, geometry and pixels. Studio integration tests cover
discovery, analysis/import state, overwrite protection, host services and lifecycle.

```console
dotnet test Testing/Gondwana.Tooling.Importers.Tests -c Release
dotnet test Testing/Gondwana.Tooling.Studio.WinForms.Tests -c Release
```
