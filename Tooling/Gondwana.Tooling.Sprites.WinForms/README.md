# Gondwana Sprite editor

A Windows WinForms authoring tool for `.gspr` sprite collections. One file may contain a player, several guards, projectiles, or any grouping chosen by the developer. It edits definitions, not running game instances.

## Launch and documents

```console
dotnet run --project Tooling/Gondwana.Tooling.Sprites.WinForms
```

Use File > New (Ctrl+N), Open (Ctrl+O), Save (Ctrl+S), Save As (Ctrl+Shift+S), and Close (Ctrl+W). Several documents can remain open at once. Modified documents show an asterisk and prompt before closing. Saving reports structural validation errors and uses a temporary file followed by replacement. Save As rebases relative GTS/GSCN authoring paths.

Choose a working directory from File. The tree recognizes GSPR, GSCN and GTS. Double-click GSPR to open it; double-click a dependency to add it to the active document, creating a document when necessary.

## Sprite collection workflow

The Sprites pane provides Add, Duplicate and Remove. New entries receive a GUID and a unique nickname, such as `sprite-2`. Scene, layer and frame are initially unassigned. Duplicate copies authored properties and dependency assignments, then generates a fresh GUID and nonconflicting nickname. Removing an entry changes only its definition.

Selecting an entry updates Properties and Sprite preview. Edit nickname, position, visibility, render size, alignment, nudges, rotation, fog and collision settings in Properties. Scene/layer and frame assignments are primarily made through their source trees.

## Scene and frame sources

Use Add GSCN in the source pane or the File menu. Expand the scene and double-click a layer to assign its stable SceneId and SceneLayerId to the selected sprite. Use Add GTS, expand a tilesheet and region, and double-click a frame coordinate to assign a logical frame reference. Sources are shared by all entries in the document.

GTS data and images belong to the editor. They are not registered with TilesheetRegistry. Loading GSCN reads definitions without materializing a runtime scene. Runtime GSPR materialization never reads authoring dependency paths; the game must load its dependencies first.

## Preview and validation

The preview displays the selected frame at its authored size, alignment, nudges and rotation, with a logical anchor and collision bounds. When a source layer is available, projection uses its tile dimensions and coordinate system through Gondwana's public coordinate conversion. Missing source images leave a bounds preview.

Ctrl+wheel zooms by 1.25 per notch. Minus and plus divide/multiply by 1.25. The selector offers 25%, 50%, 100%, 200%, 400% and Fit. Scrollbars appear for oversized content. Zoom is not a dock-layout preference.

Validation distinguishes structural ERROR messages from dependency WARNING messages. Missing files or unresolved authoring sources do not make otherwise valid runtime references structurally invalid. Empty collections and unassigned frames retain existing engine semantics.

## Docking and Studio

Six nested panes are available: Sprites, Sprite preview, GSCN scene/layer sources, GTS frame sources, Properties and Validation. Drag tabs and resize splits to arrange them. Inner panes remain document panes and cannot float. View lists pane visibility, restores hidden panes, and offers Show all sprite panes and Reset active editor layout.

The shell's Working directory pane uses the existing `shell` profile. View > Reset application layout resets the outer shell. Inner layout uses `EditorDockWorkspace("gspr")`; shared DockLayoutStore and DockLayoutPersistence save arrangement and visibility per application. Corrupt/missing preference files fall back to defaults. Preferences never live in GSPR files or beside the executable. Documents are not reopened as a saved session.

Studio references and hosts the same public `SpriteEditorControl`; it supplies its own outer shell. Studio and standalone GSPR layouts remain independent because the shared preference store scopes them by entry application.
