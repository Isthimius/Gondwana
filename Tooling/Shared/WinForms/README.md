# Shared WinForms docking

The five reusable editor controls and Studio source-link the docking helpers. There
is no runtime/engine dependency on these preferences and no additional docking framework.

`DockLayoutStore` centralizes the per-user directory and atomic writes. The default
root is `%LOCALAPPDATA%/Hidden Worlds Games/Gondwana/Tooling`; the entry assembly's
simple name selects the application directory (without its version). `shell.xml`
stores outer tool windows, while `gaf.xml`, `gts.xml`, `gani.xml`, `gsnd.xml`, and
`gscn.xml` store independent editor-type arrangements under `Docking`.

Embedding hosts may set these process-wide AppContext values **before constructing
any editor or shell**:

- `Gondwana.Tooling.SettingsRoot`: an alternate preference root.
- `Gondwana.Tooling.ApplicationId`: a stable application directory name.

Ordinary hosts need no configuration: `Assembly.GetEntryAssembly()` keeps Studio
independent from each standalone executable. The shared xUnit isolation attribute
sets both values before every WinForms test, using a unique temporary root. The
store also accepts explicit root/application arguments for isolated use.

`EditorDockWorkspace` receives a profile key, creates panes with dedicated logical
IDs, registers default placements, and starts persistence once all panes exist.
`DockLayoutPersistence` uses DockPanelSuite 3.1.1 `SaveAsXml` / `LoadFromXml`. Its
resolver returns only registered instances, never documents or arbitrary types.
Unknown IDs are skipped; newly added panes use their registered default placement.
Invalid preferences fall back to the current defaults with a trace warning.

A 400 ms UI timer compares in-memory library snapshots. Two matching observations
debounce changes before writing; unchanged snapshots cause no filesystem access.
Disposal flushes pending changes before disposing any panes. Transient keyboard
focus is excluded so an unchanged sibling editor does not overwrite another's
arrangement. Tab selection and pane visibility remain part of the layout. Outer
documents are replaced with anonymous IDs in saved XML and ignored on restore.
No document paths, document data, or main-window placement are stored.

View provides **Reset application layout** and **Reset active editor layout**.
Reset clears the applicable profile and reapplies the current default placements
to the existing contents. Open documents survive shell reset. Inner panes remain
document-only and owned by their editor, including while hidden.

## Workspace sessions

`WorkspaceSessionStore` and `WorkspaceSessionRestore` implement the UI-independent,
versioned JSON preference and recovery policy. `WorkspaceSessionController` adds
shell menus and delegates opening/activation to the owning shell. The shared files
are source-linked like the docking helpers; they introduce no runtime dependency.
Application scoping and AppContext overrides are shared with `DockLayoutStore`.

The shell calls `Save` only after close approval and before document teardown.
Restore runs once on `Shown`, after the shell, editor adapters, and plugin panes
exist. The store writes atomically; invalid/missing preferences do not block startup.
Session identity records include only absolute saved-file paths and format keys.
Restoration deduplicates canonical paths, validates supported formats and file
existence, isolates each open failure, and activates only a successfully reopened
file. See the [Studio README](../../Gondwana.Tooling.Studio.WinForms/README.md#workspace-session-restoration)
for controls, empty-start recovery, and exclusions.

Startup recovery messages use a short-lived summary collector. Preference writes
use a separate ongoing reporter: Studio logs failures in Output; each standalone
shows a warning, including for option changes and approved shutdown snapshots.
A failed option write keeps the previous preference and restores its checkbox.

## Navigation icon and text size

`Navigation/` is compiled once into the existing shared WinForms appearance assembly.
`NavigationSizing.Attach` explicitly opts navigation trees into size changes; it does
not traverse arbitrary controls or replace `ToolingAppearance`. Studio, GAF/GTS
workspace browsers, standalone working-directory trees, GANI/GSCN frame-source trees,
GSCN scene structure, and GSPR scene/frame trees use the same binding. Editor canvases,
property grids, editing grids/lists, toolbars, and document tabs keep their sizing.

Each shell provides **View → Icon Size → Small / Medium / Large**, using 16/32/64
logical pixels and 9/11/13 point Segoe UI fonts. Icon dimensions and row padding scale
from the tree's current DPI, while font points are not multiplied a second time.
DPI changes rebuild only presentation resources. Text-only trees stay text-only.
Thumbnail trees retain their original image list and refresh independent scaled
copies after lazy loading; resizing never reloads definitions, archives, or nodes.

The preference is `%LOCALAPPDATA%/Hidden Worlds Games/Gondwana/Tooling/navigation-icon-size.txt`,
independent of the entry application. The settings-root override still applies.
On first use it migrates Studio's previous
`Gondwana.Studio.WinForms/Studio/directory-icon-size.txt` (or its configured
application scope). The original is retained as a backup; the shared preference
then takes precedence. With no valid saved preference, controls retain their
existing defaults (including Studio's Small default). A successful menu choice
updates every registered tree in the process immediately, including hosted editors;
new controls read the saved choice. Other running tools read it when activated or
when opening the size submenu. Layout reset does not change the shared preference.
Failed writes report a warning and retain the current presentation.

Shell activation synchronization is registered independently of menus, so Studio
refreshes even before its lazy View menu is opened. Embedding hosts using an isolated
`NavigationPreferenceStore` pass that source to both `Attach(tree, store: source)`
and `CreateMenu(form, source)` (or `SynchronizeOnActivation` for a lazy menu).
Notifications are scoped by the canonical preference path; isolated roots do not
change the global tools' presentation. Migration publishes only if the shared file
is still absent and reads the winning value when another writer gets there first.
