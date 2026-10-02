# GPU render frame snapshots (implementation design)

Baseline: master f8e4c0c87857d2bae98aeecbd6516cbd03bbaf73.

The engine currently skips GPU hosts at foreground cadence; desktop callbacks
enter RenderStateSynchronization and call RenderToBackbufferGpuFull. That method
resolves views, effects, visible ordered drawables, animation frames, overlays,
and post-scene hooks while GL holds the simulation gate.

## Proposed representation and ownership

Use an internal RenderFrameSnapshot containing a completed Skia picture: an
immutable native command stream rather than thousands of managed command objects.
Record through a separate BackbufferBase implementation on the engine thread.
Reuse the full-frame composition algorithm, including DirectDrawing dispatch,
against that recording destination. No GPU surface/context is created there.
GL replays the completed picture to its existing GpuBackbuffer.

Three reusable slots transition Free -> Building -> Published -> Rendering ->
Free. Publishing replaces and releases an unacquired Published frame. A short
mailbox monitor protects ownership transitions only; recording, replay, and
native resource release run outside it. Closing retires published storage but
does not dispose a Rendering frame until its consumer releases it.

Skia recordings copy paint/path state and retain native image/font references.
Tile images are CPU SKImage instances produced from region slice bitmaps;
DirectSvg uses CPU rasterization; DirectImage and widgets draw through SKCanvas.
DirectDrawingBase.Dispose currently takes the simulation gate. Keep that gate
for recording versus mutation/disposal, but remove it from desktop replay.
Native retention and mutable-bitmap capture must be tested before switching.
Context-backed images supplied by applications require an explicit supported
contract; they cannot simply be recorded on the engine thread.

## Compatibility and remaining design checks

- RenderBackbufferPostScene and plugin canvas callbacks currently promise a
  current GL context. Recording these callbacks changes their threading contract;
  this needs explicit documentation and validation, not silent substitution.
- GPU initialization raises SizeChanged and changes live viewports. Hand off that
  change under a short state gate; never protect ordinary replay with it.
- Snapshots carry logical dimensions; reject mismatched frames after resize.
  CPU recordings must not retain old context-bound render targets across MSAA or
  context recreation.
- Browser WebGL retains its synchronous live render path initially. Bitmap dirty
  rendering remains unchanged.
- Existing scene/query/draw diagnostics become producer build/query/record
  timings. Add separate replay, age, publication/drop, and slot measurements.
- Validate ownership/concurrency/lifetime first, then visual equivalence and
  desktop, browser, bitmap, and Scene Viewer regressions. Hardware performance
  conclusions require actual Scene Viewer dogfood measurements.

This note records the design under investigation, not completed acceptance.
