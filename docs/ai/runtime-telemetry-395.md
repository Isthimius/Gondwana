# Runtime telemetry #395: validation and overhead

Implemented from current master `b3fd92c21`, October 6, 2026. Issue #395 already
matched the revised handoff; no Studio description drift was found. Draft PR #440
remains for Mike's review. Studio, transport, and object inspection are deferred.

## Delivered behavior

- `Engine.Profiler`: independent disposable requests, validated shared options,
  detached completed windows/history, reset generations, and bounded extensions.
- Existing Engine and render timing boundaries feed neutral measurements; legacy
  diagnostics remain independent. No profiler event subscriptions enable legacy
  layer allocations. Desktop replay still uses the existing mailbox without taking
  the simulation gate.
- F3 reads the shared snapshots, releases its own request when hidden, and formats
  on the Engine thread at about 4 Hz. White text/background alpha 102 and F4 remain.
- Backend support, CPU timing semantics, history gaps, counter rebasing, and source
  retirement are documented in `docs/wiki/Runtime-Telemetry.md`. A compiling ordinary
  application example is linked into Gondwana.Tests from `docs/examples/`.

## Measurement environment and method

Windows 10 x64, .NET SDK 8.0.425/runtime 8.0.31, Intel Core i9-9900K,
NVIDIA GeForce RTX 3090 Ti (a USB virtual display adapter was also enumerated).
WinForms desktop GPU, 1024x768 off-screen native window, orthogonal existing
SceneViewer stress generator with 1,024 or 65,536 tiles. Engine TargetFPS=0;
**VSync=true**, matching actual current-master ViewerConfiguration (its old comment
incorrectly said otherwise). Presentation was around the display cadence in the
native dogfood capture. These results do not use VSync-limited presentation FPS
as evidence of zero overhead.

The same opt-in `RuntimeTelemetryOverheadTests` harness was compiled against an
archive of master and the revised branch. Each native case used 3 seconds warmup
and three approximately 5-second samples. Cases were repeated in reverse order.
The second batch also forced full GC at the end of warmup and at completion for
retained process-heap readings, and queried CPU stages at sample boundaries.
Default profiler options: 250 ms, 120 history buckets, 16 sources, 128 metrics per
source. F3 off baseline, revised disabled, collecting without F3, and F3 visible
were measured separately. The initial stalled UI-timer attempt was discarded;
the retained harness samples on a background timer.

Raw six-sample results per case: `docs/performance/runtime-telemetry-395.csv`.
Last-window CPU timing samples, retained heap readings, and isolated Engine loop
samples: `docs/performance/runtime-telemetry-395-detail.txt`.

| Tiles | Mode | Mean simulation/foreground cycles/s | Range | Mean process CPU ms per ~5 s | Managed allocation MB/s |
| ---: | --- | ---: | --- | ---: | ---: |
| 1,024 | master, F3 off | 13,825.5 | 11,783–15,873 | 7,320.3 | 36.083 |
| 1,024 | revised disabled | 14,024.2 | 12,368–15,787 | 5,960.9 | 36.601 |
| 1,024 | collecting | 14,039.0 | 12,150–15,946 | 7,471.4 | 36.782 |
| 1,024 | F3 visible | 6,809.3 | 5,756–7,628 | 7,609.4 | 61.179 |
| 65,536 | master, F3 off | 129.2 | 106–150 | 8,703.1 | 0.586 |
| 65,536 | revised disabled | 117.7 | 98–134 | 8,731.8 | 0.540 |
| 65,536 | collecting | 112.3 | 79–139 | 8,226.6 | 0.639 |
| 65,536 | F3 visible | 117.5 | 89–141 | 8,635.4 | 1.470 |

Native throughput varies substantially between batches. In particular, the large
scene's disabled mean is lower than baseline, with overlapping ranges; this data
cannot establish a precise disabled percentage or a hardware-independent bound.
F3 drawing has a clear cost on the small uncapped scene. Collecting/display CPU
stage samples from the large scene put background means around 0.010–0.017 ms and
build means around 8.7–12.0 ms in the reverse-order batch. Full stage samples are in
the detail file; they are individual recent buckets, not whole-run averages.

To isolate Engine guards from native-driver variability, the harness also invoked
the actual private simulation-cycle method through a cached typed delegate with no
foreground render or scene, CPS notifications disabled, 100,000 warmup cycles, and
three runs of one million cycles. Baseline and disabled were repeated in alternating
order. Excluding each process's first measured run (which still showed tiered-JIT
transition), baseline averaged 271.6 ns/cycle and disabled 275.8 ns/cycle: about
**4.2 ns added** on this host. Collecting averaged 468.0 ns/cycle in its final two
runs, about **0.2 microseconds added** to this deliberately minimal workload.

The isolated loop's existing allocation was 248 B/cycle on both baseline and
revised-disabled builds. Collecting reported 248.001–248.003 B/cycle including
occasional bucket publications. A controlled-clock functional test separately
verifies zero managed allocations for 10,000 warmed individual observations when
no bucket closes. These statements concern added telemetry allocation, not an
allocation-free engine overall.

In the reverse native batch, retained process heap changed by roughly 2–5 KB with
collection off and 1.47–1.52 MB with collection/display on during the 15-second
measurement. This includes history filling and the benchmark's dynamic diagnostic
queries. The initial run is shorter than the default 30-second retention horizon
and measures history filling. A separate 65,536-tile collecting run used 35 seconds
warmup and three more 5-second samples, with full GC after each sample. Retained
process heaps after history filled were **29,520,952; 29,523,160; and 29,522,728
bytes**, stable within about 2.2 KB. This is a short plateau observation, not a
long-duration leak guarantee. The deterministic 2,400-window test also verifies
that only the last 120 windows remain. No raw per-cycle samples or runtime-object
references are retained by snapshots.

## Reproduction

Build the SceneViewer test project in Release. Run one test per fresh process:

```powershell
$env:GONDWANA_TELEMETRY_BENCH = '1'
$env:GONDWANA_TELEMETRY_MODE = 'disabled' # collecting or display
$env:GONDWANA_TELEMETRY_TILES = '1024'    # or 65536
dotnet test Testing/Gondwana.Tooling.SceneViewer.WinForms.Tests/Gondwana.Tooling.SceneViewer.WinForms.Tests.csproj -c Release --no-build --filter FullyQualifiedName~MeasureNativeViewer --logger 'console;verbosity=detailed'
# Isolated Engine loop; display mode is not applicable:
dotnet test Testing/Gondwana.Tooling.SceneViewer.WinForms.Tests/Gondwana.Tooling.SceneViewer.WinForms.Tests.csproj -c Release --no-build --filter FullyQualifiedName~MeasureEngineLoop --logger 'console;verbosity=detailed'
```

Copy this reflection-compatible harness into an archive of `b3fd92c21`, build, and
run disabled mode there for baseline. Keep other builds/tests out of timed runs.

## Validation record

- Release workload restore and solution restore succeeded. Workload cleanup emitted
  an existing SDK-version garbage-collection warning; restore still succeeded.
- Full Release solution build, including WebAssembly native linking: passed. Final rebuild used `-m:1` after concurrent Spot asset packing collided on a shared `spot.gaf` output.
- Final core regression suite: **835 passed**, including timer-driven and long-history tests.
- After preallocating the fixed-capacity history queue, all **29** targeted telemetry,
  Engine routing, and rendering/mailbox tests passed again.
- Viewer/native suite after fixing optional registration after collector disposal:
  57 passed, 2 benchmark tests skipped. Includes actual GPU context/MSAA/resize,
  Avalonia context recreation, F3/F4 dogfood, independent F3 request ownership, and
  alpha 102 assertions. F3 screenshot was visually inspected for legibility/layout.
- Browser JavaScript helpers: 11 passed.
- Requested whole-solution IDE0005/IDE0059/IDE0051 style check passed. Whole-solution
  whitespace reports a pre-existing `CHARSET` error in unchanged
  `Demos/Gondwana.CoordinateTest/GameWindow.cs`; changed-file whitespace **passed**.
- Release solution package validation passed (no packages published).
- One earlier full core run hit concurrent removal from the existing global Scene
  list, then cascaded failures from its corrupted state. A fresh full rerun passed
  all 833 tests. A later timing-sensitive widget repeat-key test failed once; the final fresh run passed all 835. No Scene-registry or widget behavior was changed in this PR.
- The sandbox blocked the baseline HTTP listener test; it passed with elevated
  execution. Native runs also used elevated desktop access.

Limits: no live browser/WebGL interaction run, no browser/bitmap performance study,
and no multi-hour memory/leak study. CPU-backed rendering/mailbox
regressions, actual desktop GL runs, WebAssembly build, and browser helper tests
provide the recorded coverage. Existing build warnings remain.

The native F3 capture is available at `docs/performance/runtime-telemetry-f3.png`.
