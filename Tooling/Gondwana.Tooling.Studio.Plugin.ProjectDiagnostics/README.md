# Project Diagnostics — Studio plugin reference

Project Diagnostics is an optional, read-only tool for the active Studio working
directory. It discovers `.gaf`, `.gts`, `.gani`, `.gsnd`, `.gscn`, and `.gspr`
files, shows load status and explicit references, and reports load, validation,
missing-file, malformed-path, and missing packed-entry problems.

This is cross-cutting project inspection, so it belongs in a removable plugin.
Studio's six authoring editors remain built in. Removing this plugin does not
remove any editing functionality or prevent Studio from starting.

## Build and run

On Windows, from the repository root:

```console
dotnet build Tooling/Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics -c Release
dotnet run --project Tooling/Gondwana.Tooling.Studio.WinForms -c Release --no-build
```

The plugin targets only `net8.0-windows`, matching the current WinForms contract.
Building the plugin (including through `Gondwana.sln`) builds Studio and copies
the plugin DLL to the matching Studio output's `plugins/` directory. For the
default Release build this is:

```text
Tooling/Gondwana.Tooling.Studio.WinForms/bin/Release/net8.0-windows/plugins/
  Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics.dll
```

Studio itself has no reference to this project. Building only Studio leaves
plugin deployment unchanged; build the plugin again after editing it. Close
Studio before rebuilding a loaded DLL. To remove the plugin, close Studio and
remove its DLL from `plugins/`; building the plugin again reinstalls it.

The project is intentionally not a NuGet package. For a separately published
Studio distribution, place the built plugin DLL in that application's
`plugins/` directory. Plugin packaging/version distribution policy is a follow-up.

## What is checked

| Source | Explicit references inspected |
| --- | --- |
| GANI | `TilesheetSources`: loose GTS or GAF plus typed entry |
| GSCN | `TilesheetSources` and `AnimationSources`: GTS/GANI or packed entries |
| GSPR | `TilesheetSources` and `SceneSources`: GTS/GSCN or packed entries |
| GTS | `Image.FilePath` or `Image.AssetsFilePath` / `AssetEntryName` |
| GSND | Loose media, GAF audio entries, and media URI syntax via the public validator |
| GAF | Public archive structure/key validation |

Paths are resolved relative to the containing definition file, matching the
authoring document models. Serialized load provenance is not treated as a
dependency. Runtime tilesheet names, animation keys, scene IDs and next-cycle
keys are not guessed into filenames. In particular, current GSPR has no GANI
source list, and current GSCN has no GSND source list.

The scanner uses the public `*DefinitionSerializer.Load` and
`*DefinitionValidator.Validate` APIs. Successful parsing and validation are
distinct: a loaded file can still have validation problems. GAF uses
`AssetsFile.Validate(testData: false)` to check structure without decompressing
all media. Packed references use a detached `AssetsFile` and typed `Get`; missing
entries and unreadable/password-protected packages become problems. No password
prompt is presented. Packed definitions are not recursively inspected, media
is not decoded, and URIs are not fetched or treated as project-relative file paths.
Both relative and absolute audio URIs are supported by the current model.
An existing loose reference is checked
for existence; its target is parsed separately if discovered under the root.

Scans skip `bin`, `obj`, `.git`, `.vs`, `node_modules`, and filesystem links or
junctions. Enumeration errors become source-specific problems. No watcher,
runtime registry, game loop, editor internals, or alternate JSON parser is used.

## Plugin anatomy

Start with [ProjectDiagnosticsPlugin.cs](ProjectDiagnosticsPlugin.cs). It has a
public parameterless constructor and implements
`Gondwana.Tooling.Studio.WinForms.Extensibility.IStudioPlugin`, which extends
the framework-neutral Core interface.

- `Name` identifies the panel and plugin in Studio.
- `CreatePanel` contributes a `Control`. Studio owns its dock container,
  recursively applies the dark theme, and disposes the control at shutdown.
  `View` can restore the hidden panel. Hiding a panel does not unload a plugin.
- `CreateMenuItem` contributes a root **Project Diagnostics** item with
  **Rescan**, under Studio's existing **Plugins** menu.
- `OnProjectOpened` receives the working-directory path, cancels old work, and
  starts a fresh scan. These callbacks represent a directory, not an EngineState.
- `OnProjectClosed` cancels the current task, forgets the directory, and clears
  displayed results. Studio calls it before switching directories and on shutdown.

[ProjectDiagnosticsScanner.cs](ProjectDiagnosticsScanner.cs) is independent of
WinForms controls. It returns snapshots containing paths, status and diagnostics;
it retains no loaded models or archive handles. Public engine APIs suffice; this
plugin introduces no new engine API or `InternalsVisibleTo` access.

[ProjectDiagnosticsPanel.cs](ProjectDiagnosticsPanel.cs) owns presentation and a
small WinForms timer. Scanning runs through `Task.Run`. The timer observes the
current task on the UI thread, so workers never access controls, including before
the panel has a window handle. Rescan immediately clears the old result and
replaces the task reference. Completion from a cancelled, superseded scan cannot
repopulate the panel. Cancellation is checked between filesystem operations;
an in-flight synchronous serializer/archive call may finish before cancellation.
Closing never waits on that call. Panel disposal stops the timer and cancels work.

Keep lifecycle callbacks short and handle errors in worker tasks: the host only
catches synchronous lifecycle exceptions. The reference plugin reports scan
failures in its panel and observes abandoned task faults through `Trace`.

## Discovery and dependency identity

At startup, `StudioPluginHost` enumerates top-level `*.dll` files in
`<Studio application directory>/plugins/`. Each assembly is inspected for
concrete Core `IStudioPlugin` implementations with public parameterless
constructors. WinForms contributions additionally require its derived interface.
Load/constructor failures are logged; a synchronous lifecycle failure disables
that plugin. Panel/menu contribution failures are logged. An absent `plugins/`
directory is supported. There is no enable/disable settings UI or hot reload.

Each plugin assembly gets a `PluginLoadContext` using
`AssemblyDependencyResolver`. The host now explicitly shares
`Gondwana.Studio.Core` and `Gondwana.Studio.WinForms` from its own context **before**
private resolution. This prevents duplicate interface identities even if a
plugin accidentally ships private contract copies. Platform hosts can supply
their own additional contract assemblies to the Core host constructor.

This plugin has no private dependencies: Studio already provides Gondwana and
the serializers' dependencies. Its build copies only the plugin DLL. Do not copy
the whole build output into `plugins/`, especially host/engine assemblies.
For another plugin with private dependencies, deploy its dependency files and
matching `.deps.json` so `AssemblyDependencyResolver` can locate managed/native
libraries, while keeping host contracts out of that private set. Test against
the Studio version you intend to support. Load contexts provide dependency
isolation, not a security sandbox; install only trusted plugins.

## Minimal plugin

Create a non-packable `net8.0-windows` class library with `UseWindowsForms=true`
and a reference to the current Studio WinForms project/assembly. The current
interface signatures are:

```csharp
using System.Windows.Forms;
using Gondwana.Tooling.Studio.WinForms.Extensibility;

public sealed class ExamplePlugin : IStudioPlugin
{
    public string Name => "Example";

    public void OnProjectOpened(string projectPath) { }
    public void OnProjectClosed() { }
    public Control? CreatePanel() => null;
    public ToolStripMenuItem? CreateMenuItem() => null;
}
```

Deploy its DLL beneath Studio's `plugins/` directory, then restart Studio.
Use this project's plugin, scanner and panel as the full working example.

## Tests and intentional limits

```console
dotnet test Testing/Gondwana.Tooling.Studio.WinForms.Tests -c Release
```

The existing Windows CI job runs scanner tests without UI automation, plus STA
integration tests for lifecycle, menu, panel, real DLL discovery, and contract
identity. Existing Studio composition tests continue to cover built-in editors
with plugin loading disabled. The solution also builds on non-Windows CI with
Windows targeting; WinForms tests execute only in the Windows job.

Future work may include incremental scanning, packed-definition traversal,
password handling, richer navigation and distribution policy. Filesystem
watching, editing, runtime semantic validation, and a dependency graph visualizer
are deliberately outside this first plugin.
