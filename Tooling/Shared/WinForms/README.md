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
