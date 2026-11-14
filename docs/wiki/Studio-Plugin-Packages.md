# Studio plugin packages

Studio discovers optional tools at startup under `<application directory>/plugins/`.
The six native editors remain built in and independent of plugin discovery.
Use Plugin Manager for reviewed local package changes. Close all Studio instances
and restart to apply them; there is no hot reload or automatic restart.

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
from the choices used at startup or a package change is pending. Reverting every
activation choice clears the notice only when no package changes remain. Studio
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
and reports the error in the manager and Output. A sibling `.lock` file coordinates
writers across Studio instances. If another instance changed the saved choices,
a stale save is rejected rather than overwriting them; restart Studio to load the
latest choices before editing again. A busy save lock reports an error and allows
retrying.

A missing file means all plugins are enabled. Corrupt, unreadable or unsupported
settings also default to enabled with a warning. To avoid destroying preferences
for missing plugins, saving is blocked until the file is repaired or removed and
Studio restarted. The manager identifies the settings path and corrective action.

## Installing, updating and removing local packages

Use **Install Plugin…** and choose **ZIP archive…** or **Package directory…**.
The directory must contain `plugin.json` directly. A ZIP may contain the package
contents at its root or one enclosing directory, with nothing alongside that
directory. Studio copies the source to inactive staging and inspects metadata;
preview does not execute plugin code, including constructors or module initializers.

Review the name, ID, version, author, description, entry assembly, contract,
dependency/support-file summary, destination and operation type. Read the trusted
code warning, check the explicit confirmation box, and choose **Confirm for next
startup**. Cancelling this dialog leaves installed files and pending operations
unchanged. New installations use `plugins/package-<manifest-id>/`. Installation
of an already-present ID becomes a reviewed update of its existing directory.

Select a third-party manifest package and choose **Update from File…** (ZIP or
package directory). Its ID must match. The preview shows old/new versions and
labels upgrades, same-version replacements and downgrades using SemVer precedence
(including prereleases; build metadata does not affect precedence). Every change
requires explicit consent, including replacements and downgrades. A higher version
does not override contract validation. Replacement uses exactly the new package
contents; obsolete files are not merged into it.

**Uninstall…** previews the selected package's name, version and location. Confirm
to queue removal of that directory at startup. Saved activation preferences remain;
reinstalling the same ID restores the previous enabled/disabled choice.

Project Diagnostics and External Import are Studio-distributed packages. Their
manifest IDs and standard deployment directory names are protected from package
installation, replacement and removal. Use their activation checkboxes to disable
them; Studio's own distribution process owns their files. This is a simple
protection policy, not cryptographic publisher verification. Legacy flat DLLs
remain supported for discovery and activation preferences, but these package
file-management actions require manifests. Legacy deployment remains manual while
Studio is stopped.

### Pending changes and progress

The lower grid lists pending installation/update/removal independently of the
current discovery list, including installations not yet discoverable. Select an
operation for copyable diagnostics and its destination. Current plugin instances,
menus and panels remain unchanged. Inspection/staging runs in background work;
controls are disabled while an operation is in progress, and the status line
reports inspection or journal saving. Errors also go to Output.

**Cancel Pending Operation** removes a queued or safely rolled-back failed change.
Activation preferences are unaffected. Contradictory operations on one ID are
rejected; cancel the existing change first. A pending update can be replaced by
another explicitly reviewed update. Pending changes survive normal shutdown and
unexpected process exit. Failed operations are retained for attention and do not
retry automatically. Recovery-required records cannot be cancelled until their
backup data is resolved.

### Transactions, coordination and recovery

Staging, backup directories, lock files and the schema-version-1 `operations.json`
journal reside in `<application directory>/.plugins.operations/`, alongside the
active `plugins/` tree. Files are never silently redirected to another plugin root.
This location is installation-wide; activation preferences remain per user.
Journal saves flush to disk and atomically replace the previous journal. Confirmed
operations contain stable IDs, safe child folder names and SHA-256 content
fingerprints, rather than arbitrary source/destination paths.

At startup, before discovery, Studio obtains an exclusive writer gate and attempts
an exclusive installation lease. Every running Studio holds a reader lease for
its entire process lifetime (closing a pane or form does not unload assemblies).
If another instance is running, package changes are deferred: close **all** Studio
instances and launch again. An operation competing for the writer gate reports a
busy/permissions error and can be retried. This coordination is independent of
incidental DLL file locks. Older Studio versions and external deployment tools do
not participate; close them before managing packages.

Source packages on other volumes are copied fully into inactive staging first.
Stage, backup and destination then share the same installation volume. Before
applying, Studio revalidates the staged package, hashes and destination identity.
It writes an Applying journal record before moving the old directory to backup,
then moves the complete replacement into place, then records Committed before
cleaning the old backup. Removal uses the same backup/commit boundary. No partial
copy is exposed as an installed package.

A failed/interrupted operation restores the verified original when possible and
becomes Failed; unrelated operations and plugin discovery continue. If external
changes prevent safe rollback, RecoveryRequired retains the journal and backup,
with paths and diagnostics. Close all Studio instances before manually inspecting
these files; preserve unknown content, restore the verified backup to the stated
package directory if appropriate, and retain a copy of the journal before repairing
its affected record. Do not delete the entire plugin root or unrelated transaction
folders. RecoveryRequired records do not retry automatically. A Committed record
with cleanup diagnostics means the change applied but obsolete data was retained.

Cancellation and normal completion clean known staging/backup content. Unexpected
or altered content is retained with a warning rather than deleted. Abandoned
previews from a process killed before confirmation are not applied; unreferenced
transaction directories may be inspected and removed manually while Studio is
closed. Unrecognized files are never treated as pending operations.

The application directory must permit creation of the coordination/staging area
and modification of managed package directories. Fix permissions or use a writable
Studio installation; do not run untrusted plugins elevated merely to install them.
If Studio cannot establish its process lease, it withholds optional plugin loading
and reports the reason, while the built-in editors and Plugin Manager remain
available. This avoids an unregistered reader racing a package writer. Corrupt or
unsupported journals are reported and left intact; installed packages are not
modified.

### Distributing and validating a ZIP

Keep `plugin.json`, the entry DLL and dependency metadata at the package root,
with permitted private dependencies at the locations described by that metadata.
For example, after preparing `ExamplePlugin/`:

```powershell
Compress-Archive -Path ./ExamplePlugin/* -DestinationPath ./ExamplePlugin.zip
```

Do not include host/framework/engine DLL copies, nested packages, links/junctions,
absolute paths, traversal segments, Windows device names or case-colliding names.
Standard single-volume ZIP archives only: ZIP64 is not needed/supported. Limits
are 4096 filesystem/archive entries, 256 MiB archive and expanded content, 4 MiB
ZIP central directory, 16 relative path components, 240 relative path characters,
and 64 KiB for the manifest. Directory packages use the same content/path limits.

Validation reuses the manifest schema, contract and shared-assembly rules below.
It also checks readable managed entry metadata, detectable private assembly
references and declared dependency assets. Missing libraries are named where
available. Corrupt archives, conflicting IDs, stale destinations and unsafe paths
are rejected before installed files are changed. Native/lazy binding and arbitrary
plugin behavior still require runtime testing; passing validation does not certify
security or complete compatibility.

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
