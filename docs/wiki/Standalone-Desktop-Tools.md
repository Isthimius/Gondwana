# Standalone Desktop Tools

Gondwana provides focused Windows applications for each native authoring format, plus a separate runtime Scene Viewer.

The standalone editors are not reduced copies of Gondwana Studio. Their reusable editor controls are the authoritative authoring surfaces that Studio hosts directly.

## Native content editors

| Format | Project | Primary job |
| --- | --- | --- |
| GAF / ZIP | `Gondwana.Tooling.Studio.Assets.WinForms` | Create and manage typed asset containers. |
| GTS | `Gondwana.Tooling.Studio.Tilesheets.WinForms` | Author tilesheets, regions, frames, collision metadata, and image sources. |
| GANI | `Gondwana.Tooling.Studio.Animations.WinForms` | Author animation definitions and GTS-backed frame sequences. |
| GSND | `Gondwana.Tooling.Studio.Audio.WinForms` | Author portable sound definitions and source metadata. |
| GSCN | `Gondwana.Tooling.Studio.Scenes.WinForms` | Author scenes, SceneLayers, sparse tile placements, GTS/GANI sources, and scene preview. |
| GSPR | `Gondwana.Tooling.Studio.Sprites.WinForms` | Author collections of sprite definitions with scene/layer and frame references. |

Run a tool from the repository root with the normal `dotnet run --project ...` command. For example:

```console
dotnet run --project Tooling/Studio/Gondwana.Tooling.Studio.Tilesheets.WinForms -c Release
```

Replace the project name with the editor you want to run.

## About and third-party artwork

Each of the six standalone editors exposes **Help → About** with the same
third-party artwork credits as Studio. Published tooling includes
`THIRD-PARTY-NOTICES.md` and the Oxygen notice and license files.
See the [repository notices](https://github.com/Isthimius/Gondwana/blob/master/THIRD-PARTY-NOTICES.md).

## Shared architecture

The common pattern is:

```text
standalone MainForm
    |
    +-- working-directory / source browser
    |
    +-- one or more outer editor documents
            |
            +-- reusable editor UserControl
                    |
                    +-- editor-local DockPanelSuite panes
```

The same reusable `UserControl` is what [[Gondwana Studio]] embeds.

This means fixes to an editor's authoring behavior should normally be made in the editor itself, not separately in Studio.

## Format documentation remains authoritative

The standalone applications edit native Gondwana definitions; they do not define separate tooling-only formats.

Use the corresponding format pages for serialization and runtime semantics:

- [[Assets Files]]
- [[.gts Files|GTS-Files]]
- [[.gani Files|GANI-Files]]
- [[.gsnd Files|GSND-Files]]
- [[.gscn Files|GSCN-Files]]
- [[.gspr Files|GSPR-Files]]

The editor applications add authoring workflows such as source browsing, previews, property editing, validation, docking, and Save As.

## Working directories and documents

The standalone editors use a working-directory or source-browser model appropriate to their content type. Multiple authoring documents can remain open at once where the editor supports it.

Editor-specific behavior remains with the editor. Examples include:

- GAF entry import/export/filtering and password-protected containers;
- GTS image and packed-image browsing;
- GANI GTS source browsing and animation preview;
- GSND portable audio-source editing without requiring a backend just to edit;
- GSCN scene/layer editing, GTS/GANI source browsing, sparse tile editing, and definition-driven preview;
- GSPR sprite collection editing, GSCN layer assignment, GTS frame assignment, and sprite preview.

## Docking and layout preferences

The newer standalone editors use an outer application workspace plus an inner editor-owned docking workspace.

Layouts are stored under:

```text
%LOCALAPPDATA%/Hidden Worlds Games/Gondwana/Tooling/<entry-application>/Docking/
```

Editor layout preferences are scoped by entry application, so a standalone editor and Studio can arrange the same editor type differently.

These preferences affect only UI layout. They do not alter native definitions or EngineState.

## Navigation sizing

All six standalone editors and Studio provide **View → Icon Size → Small / Medium / Large**.
Small uses 16-pixel icons and 9-point text; Medium uses 32-pixel icons and 11-point
text; Large uses 64-pixel icons and 13-point text. Icon dimensions and navigation
row heights adapt to display DPI.

The choice applies immediately to working-directory and editor navigation trees.
It preserves tree selection and expanded nodes. Editor canvases, property grids,
editing grids/lists, toolbars, and document tabs retain their existing sizing.
Trees without icons remain text-only.

One per-user preference is shared by Studio and all six standalone editors.
Studio's previous Working Directory choice is migrated automatically. Existing
defaults remain until a preference is saved. Newly opened tools use that preference;
already running tools pick up another tool's choice when activated or when opening
the Icon Size submenu. Resetting a docking layout does not reset this preference.

## Scene Viewer

`Gondwana.Tooling.Studio.SceneViewer.WinForms` is a separate utility rather than another definition editor.

Its purpose is:

> Show what a saved authored scene looks like when the Gondwana runtime actually loads and renders it.

Launch it directly with:

```powershell
Gondwana.Tooling.Studio.SceneViewer.WinForms.exe --scene "Content\Scenes\level1.gscn"
```

or use **View Scene** from the GSCN editor. The command is available in both the standalone Scene editor and Studio because it belongs to the shared `SceneEditorControl`.

The viewer:

- validates and materializes the saved GSCN;
- resolves authored GTS/GANI dependencies;
- discovers applicable adjacent GSPR definitions according to the viewer's documented resolution rules;
- uses the WinForms GPU host;
- runs real animation playback and Gondwana rendering;
- supports camera movement and zoom;
- can show the runtime diagnostics overlay.

Common controls are:

| Input | Action |
| --- | --- |
| W/A/S/D or arrows | Move camera |
| Shift | Faster movement |
| Mouse wheel | Zoom |
| Home | Reset camera and zoom |
| F3 | Toggle runtime diagnostics |
| F4 | Pause/resume active tile animations |
| Esc | Close |

The viewer is read-only. There is no save-back, gameplay scripting, hot reload, or editor transport.

See the Runtime Scene Viewer section of [[.gscn Files|GSCN-Files]] for dependency resolution and runtime behavior.

## Studio or standalone?

Use [[Gondwana Studio]] when you want several content types open under one shell and want shared global tools or plugins.

Use a standalone editor when the task is focused on one format, when you are testing that editor independently, or when you prefer a smaller dedicated application.

The content model is the same either way.

## Where to read next

- [[Studio & Desktop Tools|Studio-and-Desktop-Tools]]
- [[Gondwana Studio]]
- [[External Asset Importing]]
- [[.gscn Files|GSCN-Files]]

## Workspace sessions

Each standalone authoring editor restores its own working directory, saved file
tabs, and active document. **File > Restore workspace on startup** controls this
preference. Use **Start empty on next launch**, hold Shift while launching, or
pass `--empty-workspace` for an empty startup. Missing or invalid files are skipped
with a summary; unsaved contents are not recovered. See
[Gondwana Studio — Workspace sessions](Gondwana-Studio#workspace-sessions) for the
shared save/close, privacy, and recovery behavior.

## External content changes

All six standalone WinForms editors use the same source-version and filesystem
monitoring contracts as Studio. Watchers cover open documents and their explicit
local dependencies, including references in unselected logical definitions and
exact typed GAF entries. They do not recursively watch the workspace, and remote
media URIs are not watched. Filesystem events request SHA-256 verification; equal
file sizes and timestamps do not hide changed content.

Document captions distinguish:

- `*`: unsaved local edits.
- `[reload]`: the primary source changed externally; the clean document can reload.
- `[conflict]`: the primary source changed while local edits exist.
- `[preview]`: a referenced definition, image, or media source changed.

Right-click a document tab for **Reload from Disk**, **Keep My Edits**, **Save As**,
**Reload dependency previews**, and **Check sources now**. Background notifications
never interrupt editing with a dialog. Missing, renamed, or inaccessible primary
sources are explained in the tab tooltip. A rename does not silently redirect the
editor to a new path.

Reload loads the replacement before releasing the current editor. A clean reload
needs no confirmation; dirty documents require explicit discard confirmation.
Failed or cancelled reloads retain the document and its Undo/Redo history.
Successful reload starts a clean history and preserves the selected definition,
docking position, activation, and supported tilesheet/scene zoom where possible.
Other transient view state may reset.

**Keep My Edits** preserves the model and history but does not authorize an
overwrite or advance the accepted source version. Ordinary Save remains guarded.
A failed save offers conflict actions once per observed external version; the tab
menu remains available afterward. Native-definition **Save As** uses existing
reference rebasing and changes the active filename. The standalone **Assets Save
As** creates a separate copy, retaining the original active archive, dirty state,
and history. It does not adopt Studio's archive Save As behavior.

Dependency preview refresh works while the primary document is dirty. It uses the
existing format-specific preview loaders without replacing the document or its
history. GSND refreshes its existing media validation; this feature does not add
an audio playback engine. GAF container changes remain distinct from changes to a
specific typed dependency entry. An archive editor owns the whole GAF, so an
external change anywhere in that primary archive requires reload/conflict handling.

Own saves accept the exact staged version returned by persistence, without a
suppression timer. External writes immediately afterward remain detectable.
Closing a document releases its subscriptions; shutdown releases the host monitor.
Changing the working directory does not discard open documents or their necessary
subscriptions, and no subscriptions are retained solely for an old workspace.

**Monitor external changes** on the tab menu toggles automatic monitoring for the
current standalone application session. Disabling it releases watchers but does
not disable guarded-save conflict checks. Re-enabling reconciles current document
references. Missing parent directories or unavailable watcher resources may need
a later edit, reopen, or explicit check after access is restored. A paused partial
write can appear stable; failed loads still preserve the old document. Atomic
replacement is not a portable filesystem compare-and-swap: an unrelated process
can still write after the final pre-install version check.

Document-source monitoring applies to authoring editors; it does not replace
content in a running game or Scene Viewer. Studio's Dependency Explorer examines
saved content and invalidates stale scan results; open documents may contain
unsaved edits. Project Diagnostics remains an independent validation plugin.
