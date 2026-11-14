# Studio plugin packages

Studio discovers optional tools at startup under `<application directory>/plugins/`.
The six native editors remain built in and independent of plugin discovery.
Close Studio before replacing files; restart after any package change. There is
no installation UI or hot reload. Activation preferences are managed in Studio.

## Plugin Manager and activation

Open **Plugins > Manage Plugins…** or **View > Plugin Manager**. The built-in,
dockable pane works even when the plugins directory is absent or every plugin
fails. Its position and visibility follow normal workspace layout persistence;
closing it hides it, and either menu can reopen it.

The list shows name, version, current state, next-startup preference, compatibility,
and packaged/legacy source. Select a row for manifest description and author,
stable ID, entry assembly, locations, implementation records, and complete
selectable/copyable diagnostics. Missing legacy metadata is shown as unavailable.
Multiple implementations in one package share one row and activation choice;
redundant legacy copies use the same preference identity as their packaged source.

Change **Enabled at next startup** to save immediately. Loaded instances, menus,
panels and lifecycle participation remain unchanged until Studio is closed and
started again. **Restart required** appears while any requested choice differs
from the choices used at startup; changing every choice back clears it. Studio
does not restart automatically or unload assemblies.

At the next startup, user-disabled implementations are discovered but never
constructed, given host services, queried for contributions, or notified of
project lifecycle events. Validation, compatibility checks and duplicate checks
still run. Enabling an incompatible, invalid or conflicting package does not
bypass those checks. A runtime failure is distinct from a user-disabled preference
and does not silently change that preference.

### Settings and identity

Preferences are independent of the working directory and stored at:

```text
%LOCALAPPDATA%\Hidden Worlds Games\Gondwana\Tooling\Gondwana.Studio.WinForms\Studio\plugin-preferences.json
```

This uses Studio's existing application-settings root (including any configured
application/root override). The versioned file records only explicit disables:

```json
{
  "schemaVersion": 1,
  "disabledPluginIds": ["gondwana.project-diagnostics"]
}
```

Packaged plugins use manifest IDs. Legacy IDs use assembly simple name, public-key
token and full implementation type, independent of installation path and display
name. Preferences for missing plugins are retained for reinstallation. New plugins
are enabled by default. Writes flush a temporary sibling file before atomically
replacing the settings file; a failed save leaves the requested choice unchanged
and reports the error in the manager and Output.

A missing file means all plugins are enabled. Corrupt, unreadable or unsupported
settings also default to enabled with a warning. To avoid destroying preferences
for missing plugins, saving is blocked until the file is repaired or removed and
Studio restarted. The manager identifies the settings path and corrective action.

## Package layout and manifest

Each immediate child directory is one package. A missing `plugin.json` is an
invalid package, not an invitation to scan its DLLs as legacy plugins.

```text
plugins/
  ProjectDiagnostics/
    plugin.json
    Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics.dll
    Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics.deps.json
  ExternalImport/
    plugin.json
    Gondwana.Tooling.Studio.Plugin.ExternalImport.dll
    Gondwana.Tooling.Studio.Plugin.ExternalImport.deps.json
    Gondwana.Tooling.Importers.dll
```

A version 1 manifest is a JSON object with these case-sensitive properties:

```json
{
  "schemaVersion": 1,
  "id": "gondwana.project-diagnostics",
  "name": "Project Diagnostics",
  "version": "1.0.0",
  "entryAssembly": "Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics.dll",
  "contractVersion": 1,
  "description": "Project validation and diagnostics",
  "author": "Hidden Worlds Games"
}
```

| Property | Meaning |
| --- | --- |
| `schemaVersion` | Required integer; only 1 is supported. |
| `id` | Required stable lowercase letters/digits separated by dots or hyphens. Unique across packages. |
| `name` | Required nonblank display name. |
| `version` | Required SemVer 2.0.0 package version; prerelease/build metadata are supported. |
| `entryAssembly` | Required DLL filename directly in this directory. ASCII letters/digits/underscore first, then letters/digits/underscore/dot/hyphen; `.dll` suffix. No subpaths, absolute paths, or alternate data streams. |
| `contractVersion` | Required positive integer; this host accepts exactly 1. |
| `description`, `author` | Optional strings for description and publisher/author attribution. |

Unknown properties are ignored for additive metadata evolution. Contract 1 means
the current Core `IStudioPlugin` lifecycle, optional host-services interfaces,
and the WinForms derived panel/menu contract when hosted in WinForms Studio.
It is **not** the engine, assembly, or Studio release version. This declaration
is checked before loading code; it cannot certify compatibility with every other
Gondwana API a plugin may call. Test plugins against their intended host release.
Changes that break the plugin contracts require a new contract generation.

The entry assembly may contain multiple concrete plugin types with public
parameterless constructors. Their package metadata is shared; status rows name
each implementation separately. Empty entry assemblies fail discovery.

## Dependency boundaries

Keep private managed/native libraries and the matching entry `.deps.json` inside
the package. `AssemblyDependencyResolver` resolves those assets; an adjacent DLL
fallback supports simple managed packages without dependency metadata. Resolved
paths must stay within the package. Managed dependencies missing from a package
cannot silently bind to unrelated assemblies already loaded by another plugin or
the application. Native libraries must be resolved from package dependency metadata.

The host shares the Core contract, supplied platform contracts, and their available
transitive host dependencies from the default context. Framework assemblies also
retain runtime identity. The runtime-provided dependency manifests identify
framework assets, including Windows Desktop and self-contained runtime packs;
application-local `System.*` filenames do not authorize shared fallback. Packages may not contain private copies of these, or
Gondwana engine/host assemblies. Both filenames and managed assembly identities
are checked, including nested DLLs. `Gondwana.Tooling.Studio.Plugin.*` entrypoints
and the private `Gondwana.Tooling.Importers` library are allowed unless they are
already part of the host's shared dependency graph. Other third-party private
libraries can have different versions in different packages.

Do not copy the entire plugin build output into a package. The official build
targets copy only the manifest, entry DLL, dependency metadata, and (for External
Import) `Gondwana.Tooling.Importers.dll`. Debug/Release use the corresponding
Studio output. The shared Windows publish script stages the same packages for
nightly bundles and tagged releases.

All filesystem symbolic links and junctions in package paths (including ancestors)
are rejected, even links pointing inside the package. This deliberately conservative
rule avoids escaping links and recursion cycles. Unreadable directories, invalid
JSON, missing entrypoints, and unsupported contracts remain visible as failures.

## Deterministic discovery and legacy compatibility

1. Inspect all immediate package directories in ordinal path order.
2. Reject **all** packages claiming the same explicit ID before constructing any
   of them. Invalid/incompatible packages with readable IDs also participate.
3. Activate eligible packages in ordinal order, then inspect top-level DLLs in
   ordinal order as legacy sources.
4. Identify implementations by case-normalized assembly simple name, public-key token, and case-sensitive full
   type name, ignoring assembly version. Only the first attempted implementation
   is eligible. Packages therefore precede legacy copies; two differently named
   packages containing the same implementation use the first ordinal source.

Legacy flat DLLs continue to use the original default-context dependency fallback
and shared contract identity, including when private contract copies exist beside
them. Legacy discovery derives `legacy:<assembly>:<public-key-token>:<type>` IDs;
manifest version and compatibility are **unknown**, not guessed. Legacy dependency DLLs with no plugin types are ignored rather than reported
as failed plugins. Legacy names
come from `IStudioPlugin.Name` after successful construction.

Discovery is additive, but every source is attempted only once per host. Correcting
files requires a restart. A new explicit-ID collision found by a subsequent call
disables earlier instances and marks both sources conflicting; it cannot undo
code already executed. There is no unloading or reloading. Deploy a complete
set before startup. Built-in deployment removes obsolete flat entrypoint copies;
redundant legacy copies are also prevented from activating twice by the host.

## Diagnostics and trust

`StudioPluginHost.Discovery` returns a read-only snapshot of immutable
`PluginDiscoveryInfo` rows: ID, name, known version, source directory, entrypoint,
legacy flag, compatibility, implementation, status and diagnostic. Statuses are
`Loaded`, `Incompatible`, `InvalidPackage`, `FailedToLoad`, `Conflict`, and
`Disabled` (after a lifecycle failure), and `UserDisabled` (startup preference). A failed constructor does not stop other
types or packages. Load/type-inspection failures reject that entry assembly.
Missing dependencies referenced only by later plugin operations may fail at that
operation rather than startup; .NET loads assemblies lazily.

The existing Output log remains active. Lifecycle failures disable the affected
instance; panel/menu failures retain their existing logging/skip behavior.
The manager reads this same discovery snapshot; it does not scan independently.
For missing assemblies/dependencies, restore the named files from the plugin's
publisher. Correct invalid manifests or conflicting IDs, and obtain a compatible
package for unsupported contracts. Constructor and lifecycle failures retain full
exception details for troubleshooting; consult Output and the package author.
User-disabled status is informational, not a loading error.

**Install only trusted executable code.** `AssemblyLoadContext` provides dependency
isolation, not a security sandbox. Layout checks do not make malicious code safe,
prevent a plugin explicitly loading other files, or protect against concurrent
filesystem replacement during loading. Manual deployment should happen while
Studio is stopped.
