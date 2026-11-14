# Gondwana Studio

Gondwana Studio is the Windows (WinForms) workspace for authoring Gondwana assets.
It brings the standalone editors together in one dockable, multi-document interface:

- **Assets:** `.gaf` and `.zip` containers
- **Tilesheets:** `.gts`
- **Animations:** `.gani`
- **Sound:** `.gsnd`
- **Scenes:** `.gscn`
- **Sprites:** `.gspr`

Studio reuses the standalone editor controls. It does not require a running game
or `GameHost`.

## Running

From the repository root on Windows:

```console
dotnet run --project Tooling/Gondwana.Tooling.Studio.WinForms --configuration Release
```

## Working with files

Choose **File > Open working directory…** to browse a folder. The **Working
directory** pane groups loose files and entries from `.gaf` archives by asset
type. Double-click a supported definition to open it in a document tab; the
active tab is also highlighted in the directory tree when present.

Use **File > New** to create a document, or right-click a type group in the
working directory and choose **Add New …**. **File > Open…** opens an existing
file directly. Each document has its own editor panes; use **View** to reveal
hidden panes.

**Save** writes the active document, **Save As** chooses a new destination,
and `*` on a tab indicates unsaved changes. Closing a dirty document prompts
you to save or discard its changes.

## Editing packed definitions

Studio can open `.gts`, `.gani`, `.gsnd`, `.gscn`, and `.gspr` definitions
stored inside a `.gaf` archive, including files containing multiple definitions.
Select the entry under the archive in **Working directory** and open it as you
would a loose file. Dependencies can be previewed without unpacking the archive.

- **Save** updates that entry in its original archive while preserving other entries.
- **Save As** writes a loose file, switches the tab to the new file, and leaves
  the original archive entry unchanged.
- If the archive or entry changes elsewhere, Studio can reject a conflicting save.
  Use **File > Reload document from disk** or **Save As** as appropriate.

Encrypted archives require a password to open. Non-definition binary entries
are not edited as native Studio documents.

## Editing and navigating

**Edit > Undo / Redo** (Ctrl+Z / Ctrl+Y; Ctrl+Shift+Z also redoes) supports
document edits in the built-in editors. File relocation, deletion, and other
committed cross-file operations are not covered by document history; not every
canvas tool participates in undo.

Use **File > Find References**, **Show Dependencies**, or **Show Dependents**
to inspect relationships among authoring resources. Right-click a working-directory
entry for the same commands.

### Dependency Explorer

**View > Dependency Explorer** restores the results pane. Open a result to
navigate to the referenced document or definition. **Refresh / Rescan** rebuilds
the reference index after saved files change. Results cover saved definitions in
the current working directory, not unsaved edits or arbitrary game code;
incomplete scans report diagnostics rather than implying a resource has no users.

### Drag and drop

Drag compatible tilesheet, animation, or scene resources from **Working
directory** or **Dependency Explorer** onto supported editor targets. Studio
prompts for a member, frame, or layer when a selection is needed.

For visual sprite placement, open a GSPR and GSCN side by side. Drag a sprite
from **Sprites** onto the desired scene layer, then reposition it in **Scene
Preview**. Hold **Alt** for fractional placement and save the GSPR afterward.
See [Place and move sprites](../Gondwana.Tooling.Scenes.WinForms/README.md#place-and-move-sprites).

## Workspace session restoration

Studio remembers the working directory, saved documents, tab order, and active
document between launches. It reloads documents from disk; **unsaved changes
are not recovered**.

Use **File > Restore workspace on startup** to enable or disable restoration.
To start without reopening the previous session, choose **File > Start empty on
next launch**, hold **Shift** during startup, or pass `--empty-workspace`:

```console
dotnet run --project Tooling/Gondwana.Tooling.Studio.WinForms -- --empty-workspace
```

Unavailable files are skipped, and encrypted archives may ask for their
password again. Session preferences are separate from content files.

## Layout and appearance

Dock layouts persist per application and editor type. Use **View > Reset
application layout** for Studio's outer panes or **View > Reset active editor
layout** for the current editor.

**View > Theme** offers Dark, Dark Blue, and Light. Theme selection is shared
with the standalone WinForms editors and takes effect on the next launch.
See [shared WinForms themes](../Shared/WinForms/Theming/README.md) and
[workspace preferences](../Shared/WinForms/README.md) for implementation details.

## Optional plugins

Studio's six editors are built in. Optional plugins can add tools to the
workspace:

- [Project Diagnostics](../Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics/README.md)
  scans authoring files and references.
- [External Asset Importer](../Gondwana.Tooling.Studio.Plugin.ExternalImport/README.md)
  imports supported Tiled, Godot, and Aseprite content.

Each plugin's README explains how to build, deploy, and use it. Studio runs
without either plugin. See [plugin package conventions](../../docs/wiki/Studio-Plugin-Packages.md)
for manifests, legacy compatibility, dependency boundaries, and structured status.

## Development

Studio owns the outer docked workspace, file handling, and plugin integration;
the standalone WinForms projects supply the actual format editors. Shared
resource resolution is documented in
[Gondwana.Tooling.Assets](../Gondwana.Tooling.Assets/README.md).

Windows UI tests live in `Testing/Gondwana.Tooling.Studio.WinForms.Tests`.
Studio uses [DockPanel Suite](https://github.com/dockpanelsuite/dockpanelsuite)
for docking (originally created by Weifen Luo).

For the broader authoring toolset, see the [Tooling overview](../README.md).
