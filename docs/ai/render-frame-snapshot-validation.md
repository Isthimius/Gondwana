# Render snapshot validation

## Recorded October 2, 2026

- Release solution build: passed (existing package compatibility, obsolete Skia,
  nullable, and documentation warnings remain).
- Core suite: 772 passed. The localhost server test requires execution outside
  this workspace's restricted network sandbox.
- Scene Viewer suite: 14 passed before adding the opt-in hardware tests.
- `DesktopGpuSnapshotTests`: passed against a native OpenTK GL control, exercising
  snapshot replay, MSAA changes, logical resize rejection, and GRContext replacement.
- Mailbox tests exercise latest-frame selection, three-slot bounded storage,
  retained active ownership, shutdown, concurrent production/replay, and immutable
  capture after source bitmap mutation/disposal.
- Integration tests compare recorded/live composition, including layer opacity,
  wrapping and overlays, replay after Scene/DirectDrawing disposal, failed builds,
  and replay while another thread owns the simulation gate.

## Actual Scene Viewer dogfood

`SceneViewerSnapshotDogfoodTests` hosts the real SceneViewerGameHost, Engine loop,
WinForms GPU surface and F3 diagnostics in an offscreen window. Scene:
`assets/island.gscn`, 1024x768 logical pixels, zoom 0.125, 8,178 visible tiles,
2,155 animators, TargetFPS 60, VSync enabled, MSAA 1.

| F3 sample | CPS | Engine FPS | GPU FPS | Build avg ms | Replay avg ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Animations running | 24,460.3 | 60.0 | 60.0 | 4.878 | 15.467 |
| Animations paused | 15,341.8 | 59.1 | 51.1 | 6.814 | 18.079 |
| Paused, artificial 100 ms delay per GL callback | 20,507.6 | 59.3 | 8.7 | 5.585 | 17.197 |

The artificial delay occurs after normal GL timing collection. Its cost therefore
appears in presentation cadence, not the replay duration. At the delayed sample,
950 snapshots had been published and 266 replaced without consumption; one of the
three slots was occupied at acquisition. Broad simulation-gate wait/hold is zero
for replay by construction, not a measurement of mailbox or resize handoff locks.

These short samples demonstrate independent simulation progress and bounded
latest-frame delivery, not a portable GPU performance guarantee. Other builds/tests
ran on the machine during parts of this session; do not compare these figures
directly with the original user's hardware baseline. A native F3 image was captured
and visually inspected for readable diagnostics and intact composition.

Run hardware tests explicitly and separately from normal CI:

```powershell
$env:GONDWANA_GPU_TESTS = '1'
dotnet test Testing/Gondwana.Tooling.SceneViewer.WinForms.Tests -c Release --filter DesktopGpuSnapshotTests
```

Run the hosted viewer test alone (Engine lifecycle is process-global):

```powershell
$env:GONDWANA_VIEWER_DOGFOOD = '1'
$env:GONDWANA_VIEWER_SCENE = (Resolve-Path assets/island.gscn).Path
dotnet test Testing/Gondwana.Tooling.SceneViewer.WinForms.Tests -c Release --filter SceneViewerSnapshotDogfoodTests
```

Optional `GONDWANA_GPU_CAPTURE` names a PNG output path for the native F3 image.

## Remaining validation at this checkpoint

Avalonia lifecycle inspection and platform validation, browser JavaScript tests,
remaining Windows tooling suites, final formatting/analyzer review, and the final
post-cleanup build/test pass remain to be completed. Callback threading migration
and CPU-resource requirements are intentional compatibility changes and must be
called out in the final PR description.
