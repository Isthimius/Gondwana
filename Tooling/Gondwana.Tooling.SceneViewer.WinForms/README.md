# Gondwana Scene Viewer (WinForms)

A standalone utility that answers: what does this saved authored scene look like when Gondwana loads and renders it?

## Launch

```powershell
Gondwana.Tooling.SceneViewer.WinForms.exe --scene "Content\Scenes\level1.gscn"
```

Relative paths resolve against the launch working directory. With no arguments, an Open File dialog selects a `.gscn`. Invalid arguments, unreadable files, invalid definitions, missing dependencies, and initialization failures produce an error dialog and a nonzero exit code. Detailed exceptions are sent to the .NET trace output.

Build or run from the repository root:

```powershell
dotnet build Tooling/Gondwana.Tooling.SceneViewer.WinForms -c Release
dotnet run --project Tooling/Gondwana.Tooling.SceneViewer.WinForms -c Release -- --scene "Content\Scenes\level1.gscn"
```

## Controls

| Input | Action |
| --- | --- |
| W / Up | Camera north |
| S / Down | Camera south |
| A / Left | Camera west |
| D / Right | Camera east |
| Shift | Faster camera |
| Mouse wheel | Zoom |
| Home | Restore initial camera position and zoom |
| F3 | Toggle this Scene Viewer's runtime diagnostics overlay |
| F4 | Pause/resume all active tile animations |
| Esc | Close viewer |

Movement is continuous, in world pixels, with elapsed-time integration and normalized diagonal speed. Normal/fast speeds are 320/960 pixels per second. Zoom uses a 1.2 multiplier per wheel notch, bounded to 0.125–8. The camera starts at world origin with zoom 1, without scene-bound clamping. Losing focus clears held keys.

The window is resizable. Gondwana's current GPU host preserves the initial logical backbuffer and fits its presentation to the window, retaining aspect ratio. The viewer uses that normal runtime policy. Wheel zoom changes the runtime viewport zoom, independently of window presentation scaling.

### Diagnostics

Press **F3** to toggle this Scene Viewer's black, white-text diagnostics overlay in the upper-left of the View. This is a Scene Viewer control—including when the viewer was launched through **View Scene** from the `.gscn` editor or Studio—not a global Gondwana Engine hotkey. The overlay is hidden by default and reports sampled runtime information without replacing the normal Gondwana rendering path.

The overlay is implemented with `Gondwana.Widgets.Hud.ProfilerWidget`. It enables the widget's generic Scene/View/backbuffer/configuration/MSAA context and uses `AdditionalLinesProvider` for the viewer-specific scene/stress, F4 animation-state, and explicit `GPU FPS (presentation.count)` summary lines. This makes the Scene Viewer a real runtime consumer of the same diagnostics widget available to games.

The Scene Viewer starts with a curated measurement set rather than every low-level/per-layer diagnostic. Use the built-in **Metrics...** control to enable or disable any individual source or measurement while the viewer is running. Hover a displayed metric to see its definition; **Hover definitions** in the same selector toggles that behavior. Mouse-wheel input over the diagnostics panel scrolls diagnostics instead of zooming the scene.

It reports:

- engine-cycle and foreground-production rates plus an explicit actual GPU FPS line
- average and maximum background-work time across the sample window
- render build/query/sort/record/overlay and GPU replay/presentation timings
- average visible drawable/tile and atlas-batch counts
- snapshot age and mailbox publication/drop/occupancy diagnostics
- active animating tile count and pause state
- scene layer count and total grid-cell count
- camera world position and viewport zoom
- viewport and logical backbuffer dimensions
- target FPS and VSync state
- requested, actual, and maximum supported MSAA sample counts

Lower-level diagnostics—including picture/flush stages, command counts, and detailed
per-layer query/record/drawable/tile/dimension/Z/transform measurements—remain
available from **Metrics...** without being shown by default.

The background timing spans Gondwana's normal background phase from `BeforeBackgroundTasksExecute` through `AfterBackgroundTasksExecute`, so it includes input polling, tile animation, sprite movement, collision resolution, and camera updates. GPU diagnostics are collected only while a subscriber is attached and break the GL frame into query/sort, draw, overlay, snapshot/finalize, blit, and flush work. Press **F4** to pause or resume all currently active tile animations without editing GANI files; paused tiles stay in the active animation list, making this useful for isolating frame-change/render cost from the cost of polling animators.

## Runtime loading

The viewer derives from `WinFormsGpuGameHost`, using the template's GPU surface and normal host lifecycle. It validates GSCN, resolves its `TilesheetSources` and `AnimationSources`, registers runtime tilesheets/cycles, and calls `SceneDefinitionSerializer.ToScene`. The content root is the directory containing the opened GSCN, independent of the process working directory.

Loose sources resolve relative to the containing definition. Packed sources use the existing GAF definition loaders; filesystem references within packed definitions resolve relative to the archive's directory, as in the runtime GTS loader. Packed GTS image entries may refer to the same archive. Entry names and types are explicit. Missing archives are never created. Encrypted dependencies requiring a password are unsupported because GSCN source metadata has no password mechanism; the viewer does not guess passwords. Unreadable adjacent archives or malformed definitions fail with their source path.

Cycles are registered through the GANI serializer, then their public `NextCycle` links are connected, preserving authored self-links and mutual links in the registry. Every referenced next-cycle key needs an explicit GANI source. Current runtime `Cycle.GetAnimationCycle` clones share the frame sequence and reset `NextCycle` to the clone itself, so independently timed copies and cross-cycle playback retain those existing runtime limitations. The viewer does not patch or simulate them. Actual animation timing, per-tile animators, transforms, overhang, padding, collision geometry, projection, layer visibility/Z order, parallax, and wrapping belong entirely to Gondwana.

The viewer scans only the content root, non-recursively, for loose `.gts`, `.gani`, `.gspr`, and typed GTS/GANI/GSPR entries in every adjacent `.gaf`. Serializers establish logical GTS names and GANI keys; filenames and archive entry names need not match those identities. Resolution uses **loose definition > packed definition > explicit source fallback**. GANI → GTS and GSPR → GTS use the same resolver as GSCN → GTS. Explicit fallback paths remain relative to their containing definition or archive. Shared logical tilesheets must select one consistent source throughout a load.

Multiple matching loose definitions fail; multiple matching packed definitions fail when no loose match overrides them. Files, archives, and entries are processed in ordinal order, and conflict errors list source paths and packed entry names.

After scene/layer materialization, adjacent GSPR documents contribute only entries whose `SceneId` matches the viewed scene using ordinal comparison. Other scene entries are validated but not instantiated, and `SceneSources` never cause other scenes to load. Only selected sprites' frame tilesheets are required; their document's `TilesheetSources` supply fallback locations. Sprites use normal runtime layer, position, frame, and state materialization.

Across documents, a matching non-empty ID **or** non-empty nickname identifies a duplicate sprite. Loose entries override matching packed entries. Duplicate identities among surviving loose or packed entries fail; anonymous entries with neither identity remain distinct. Each complete GSPR document is structurally validated, even if some entries target another scene or are overridden. Source documents are not changed. The combined filtered sprite collection does not claim the provenance of any single source document.

This is a Viewer convention; the portable definition schemas and runtime serializers retain their existing explicit behavior.

## View Scene from tooling

The reusable GSCN editor's preview toolbar contains **View Scene**. Both the standalone Scene editor and Studio use this command. New or dirty documents require confirmation and a successful save through the host's normal save workflow. Canceling or failing to save never launches stale content.

Each launch starts an independent process with safe `ProcessStartInfo.ArgumentList` arguments. Closing either application does not close the other. Multiple viewers can run independently. Engine configuration is in memory so unrelated working-directory game configuration/state is not loaded or modified.

Development builds locate the viewer in its sibling tooling project's matching `bin/<configuration>/<framework>[/rid]` directory. Build the viewer in the same configuration as the editor. Published layouts may place the complete viewer output beside the editor, under `SceneViewer/`, under `Gondwana.Tooling.SceneViewer.WinForms/`, or in a sibling directory with that project name. Deploy the entire output, including runtime libraries. Missing installations produce an actionable error.

## Scope and verification

This is a saved-content Scene Viewer, not an editor or gameplay host. There is no snapshot transport, hot reload, save-back, scripting, gameplay, or debugger.

Automated tests cover arguments, dependency registration/materialization (loose and packed), missing sources, camera movement/zoom/reset, and saved-document launch decisions. The Scene editor's lightweight, definition-driven preview remains an authoring tool, using representative animation frames without an Engine.

For manual acceptance, open a multi-layer scene with two animated tiles sharing a GANI, flipped/rotated frames, overhang, tile padding and collision adjustments. Check CLI launch, standalone editor launch, Studio launch, every control above, resize/minimize/restore, animation timing, transforms, layer order, parallax/wrapping where authored, closing/reopening, and two simultaneous viewer processes. Automated non-UI tests do not establish GPU visual correctness.
