# Gondwana Animations (WinForms)

Standalone .NET 8 Windows editor for Gondwana `.gani` animation definitions.

Run from the repository root:

```console
dotnet run --project Tooling/Gondwana.Tooling.Animations.WinForms -c Release
```

## Design

The editor is definition-driven. It edits `AnimationDefinition` directly and never
boots a `GameHost`, creates a runtime `Animator`, or serializes runtime playback
state.

`AnimationEditorControl` is a public, hostable WinForms surface. The standalone
application places that control inside a DockPanelSuite document, but a future
Gondwana Studio host can reuse the same editor surface without depending on
`MainForm`.

## Workflow

- **File → New** creates a GANI definition with a repeating cycle and 0.1-second
  throttle.
- **File → Open GANI** opens one or more `.gani` documents.
- **File → Add GTS source** loads one or more loose `.gts` definitions as authoring
  sources for the active animation.
- The left source browser shows tilesheets, regions, rows and frames lazily. Expand
  a row and double-click a frame (or use **Add frame**) to append its logical
  reference to the animation.
- The sequence panel supports removal and Up/Down reordering.
- The property panel edits the GANI key, throttle, cycle type, hide-on-end flag and
  next-cycle key.
- **Play/Pause**, **Restart**, and **Step** preview the current sequence. Fit, 1x, 2x
  and 4x preview scales use nearest-neighbor sampling.
- Structural errors come from `AnimationDefinitionValidator`. Missing or
  unresolvable loaded GTS sources appear as warnings because a GANI file is allowed
  to reference content that is not currently open in the editor.
- **Ctrl+S**, **Ctrl+Shift+S**, and **Ctrl+W** save, save as and close. Dirty documents
  prompt on close.

## GTS dependency model

GANI persists logical references only:

```text
Tilesheet name
Region name
X/Y frame coordinates
```

Loading a GTS into the editor does not embed the GTS or its source image into the
GANI file.

The editor refuses to load two GTS files with the same logical
`TilesheetDefinition.Name` into one document because the resulting GANI references
would be ambiguous.

Loose GTS image files are decoded with Skia and displayed through WinForms/GDI+.
Mask metadata is applied for preview parity. Packed image references remain valid
authoring references but do not have a preview in this first pass; later GAF tooling
integration can extend that path.

## Studio direction

The standalone executable is intentionally only one host:

```text
MainForm / DockPanel
        |
        v
AnimationEditorControl
        |
        v
AnimationDocument + AnimationDefinition
```

Studio can eventually host `AnimationEditorControl` directly while using its own
project/reference model around it.
