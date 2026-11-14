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

Build with the same configuration as Studio. The build copies the plugin DLL,
its `.deps.json`, `plugin.json`, and `Gondwana.Tooling.Importers.dll` into Studio's
`plugins/ExternalImport/` directory. Core/WinForms contract assemblies and engine dependencies are supplied
by Studio; do not deploy private copies. The plugin is not a NuGet package.
Close Studio before rebuilding a loaded DLL. Discovery occurs at startup; use
**View** to recover a hidden panel. Removing the package directory disables this tool
without affecting Studio's native editors.

The manifest declares schema 1, contract 1, and an independent SemVer package
version. The build removes obsolete flat entrypoint copies. See
[Studio plugin packages](../../docs/wiki/Studio-Plugin-Packages.md) for the
manifest schema, compatibility, dependency isolation, discovery diagnostics and
legacy support. Install only trusted plugins; load contexts are not a sandbox.

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

## Studio distribution and Plugin Manager

This is a bundled Studio plugin. Plugin Manager protects its manifest ID and
standard deployment folder from installation, updating and uninstallation. Use
**Plugins > Manage Plugins…** to inspect diagnostics or disable it for the next
startup. The Studio build/release process deploys its package; changes to the
Studio distribution must happen while all Studio instances are closed.

Third-party plugins can use the documented [local ZIP/directory package workflow](../../docs/wiki/Studio-Plugin-Packages.md), including reviewed updates, pending
removal and startup rollback. Activation preferences survive removal/reinstallation.
