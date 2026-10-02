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
| Esc | Close viewer |

Movement is continuous, in world pixels, with elapsed-time integration and normalized diagonal speed. Normal/fast speeds are 320/960 pixels per second. Zoom uses a 1.2 multiplier per wheel notch, bounded to 0.125–8. The camera starts at world origin with zoom 1, without scene-bound clamping. Losing focus clears held keys.

The window is resizable. Gondwana's current GPU host preserves the initial logical backbuffer and fits its presentation to the window, retaining aspect ratio. The viewer uses that normal runtime policy. Wheel zoom changes the runtime viewport zoom, independently of window presentation scaling.

## Runtime loading

The viewer derives from `WinFormsGpuGameHost`, using the template's GPU surface and normal host lifecycle. It validates GSCN, loads its explicit `TilesheetSources` and `AnimationSources`, registers runtime tilesheets/cycles, and calls `SceneDefinitionSerializer.ToScene`. GANI's own explicit GTS sources are also loaded. Shared logical tilesheets must resolve to the same source; conflicting sources fail clearly.

Loose sources resolve relative to the containing definition. Packed sources use the existing GAF definition loaders; filesystem references within packed definitions resolve relative to the archive's directory, as in the runtime GTS loader. Packed GTS image entries may refer to the same archive. Entry names and types are explicit. Missing archives are never created. Encrypted dependencies requiring a password are unsupported because GSCN source metadata has no password mechanism; the viewer does not guess passwords or scan directories.

Cycles are registered through the GANI serializer, then their public `NextCycle` links are connected, supporting self-links and mutual transitions. Every referenced next-cycle key needs an explicit GANI source. Actual animation timing, per-tile animators, transforms, overhang, padding, collision geometry, projection, layer visibility/Z order, parallax, and wrapping belong entirely to Gondwana.

GSCN currently has no canonical collection of associated GSPR sources. Automatic sprite discovery is therefore unavailable. Future explicit sources can materialize sprites after the owning scene/layers exist. This version never scans for or guesses GSPR relationships.

## View Scene from tooling

The reusable GSCN editor's preview toolbar contains **View Scene**. Both the standalone Scene editor and Studio use this command. New or dirty documents require confirmation and a successful save through the host's normal save workflow. Canceling or failing to save never launches stale content.

Each launch starts an independent process with safe `ProcessStartInfo.ArgumentList` arguments. Closing either application does not close the other. Multiple viewers can run independently. Engine configuration is in memory so unrelated working-directory game configuration/state is not loaded or modified.

Development builds locate the viewer in its sibling tooling project's matching `bin/<configuration>/<framework>[/rid]` directory. Build the viewer in the same configuration as the editor. Published layouts may place the complete viewer output beside the editor, under `SceneViewer/`, under `Gondwana.Tooling.SceneViewer.WinForms/`, or in a sibling directory with that project name. Deploy the entire output, including runtime libraries. Missing installations produce an actionable error.

## Scope and verification

This is a saved-content Scene Viewer, not an editor or gameplay host. There is no snapshot transport, hot reload, save-back, scripting, gameplay, debugger, or asset discovery.

Automated tests cover arguments, dependency registration/materialization (loose and packed), missing sources, camera movement/zoom/reset, and saved-document launch decisions. The Scene editor's lightweight, definition-driven preview remains an authoring tool, using representative animation frames without an Engine.

For manual acceptance, open a multi-layer scene with two animated tiles sharing a GANI, flipped/rotated frames, overhang, tile padding and collision adjustments. Check CLI launch, standalone editor launch, Studio launch, every control above, resize/minimize/restore, animation timing, transforms, layer order, parallax/wrapping where authored, closing/reopening, and two simultaneous viewer processes. Automated non-UI tests do not establish GPU visual correctness.
