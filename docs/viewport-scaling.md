# Viewport scaling

Issue #39 separates logical rendering resolution from host presentation size.

## Investigation on master 7c8291cb

`RenderSurfaceHost` creates a Backbuffer from adapter dimensions. Its resize subscription
requests a CPU resize and scales View rectangles. `BitmapBackbuffer.BeginFrame` consumes
the deferred resize. WinForms GPU's resize event, Avalonia's GL callbacks, and Blazor's
per-paint size comparison independently recreate GPU render targets at adapter size.

WinForms bitmap paints dirty patches at identical source/destination coordinates. Avalonia
copies logical dirty patches to a retained bitmap and stretches that bitmap to control bounds.
Blazor bitmap uploads patches directly to its visible canvas and uses image dimensions as
canvas dimensions. GPU paths present full frames; Blazor draws the GPU surface directly.
ViewManager's layout helpers use adapter dimensions. Pointer adapters return host coordinates;
WidgetInputRouter consumes those values directly. Screenshots snapshot the Backbuffer itself.

TextBlock computes its destination in ScreenPx and applies View.Zoom to scene-mode font size,
padding, outline, and shadow. View-mode text uses a scale of one. Darkness overlays project
world points through View, and DirectRectangle's local shader scale does not use adapter size.
These drawing calculations belong inside the logical render and must not apply presentation scale.

## Configuration and coordinate contract

```csharp
Engine.Instance.Configuration.RenderScale = 0.5f;
Engine.Instance.Configuration.RenderScalingFilter = RenderScalingFilter.Linear;
// Or RenderScalingFilter.NearestNeighbor for pixel art.
float actualScale = renderSurface.Host.PresentationScale;
// The same derived state is available on renderSurface.Adapter.Presentation.
```

Set RenderScale before constructing a surface where practical. As with the existing rendering
configuration, changing it later updates active surfaces. Finite values greater than zero are
accepted, including values above one. Dimensions round to the nearest integer with midpoint
rounding away from zero, with a minimum of one. Dimensions beyond the integer range are rejected.
Backend allocation limits still apply.

The Backbuffer's initial resolution is the adapter's initial dimensions times RenderScale.
Avalonia, Blazor, and WinForms bitmap controls may be constructed before layout; their first
valid layout establishes resolution once. Subsequent adapter resizes, including maximize,
restore, and fullscreen layout changes, retain the Backbuffer object, canvas, logical dimensions,
View rectangles, camera position, and zoom. Presentation alone changes.

An explicit RenderScale change uses the current adapter dimensions and queues a resolution
request. Bitmap applies it at a render-frame boundary; GPU applies it in EnsureInitialized on
the owning GL/WebGL callback. Resolution requests publish their width/height atomically.
An explicit request during a zero-size state waits for a valid adapter size. Existing View
rectangles scale proportionally when the logical resolution actually changes, preserving the
engine's logical-resolution-dependent field-of-view behavior. Camera zoom is not modified.

PresentationTransform is the common fitting and inverse-input calculation. Adapters publish an
immutable, coherent transform when logical or presentation dimensions change. It fits the entire
Backbuffer, centers it, and clears the remaining area using existing background semantics.
PresentationScale is derived and read-only; it is not a second configuration value.

- AdapterPx means the host's existing presentation units: WinForms client pixels, Avalonia
  bitmap bounds or GPU device pixels, and Blazor's existing canvas units.
- ScreenPx means logical Backbuffer/View pixels. Existing public ScreenPx APIs retain their names.
- WorldPx and Grid conversions retain their existing math. Viewport.Zoom remains independent.

Platform mouse adapters normalize when polled, so a resize under a stationary mouse updates its
logical position. Touch adapters normalize before publishing touch state. Avalonia GPU input
converts DIPs to its existing device-pixel adapter units before applying the inverse transform.
Browser touch first subtracts the current canvas bounding rectangle, including page scrolling.
Margin coordinates remain outside, without clamping or truncating negative fractions to zero.
WidgetInputRouter excludes points outside the logical buffer from hit testing, while retaining
capture and release routing. Widgets do not inspect RenderScale.

Custom adapter/input implementations should apply AdapterPxToScreenPx at their input boundary.
TryAdapterPxToScreenPx on PresentationTransform additionally reports whether a point is inside.
Do not feed already-normalized ScreenPx through the inverse presentation transform twice.

## Rendering and dirty regions

WinForms and Avalonia GPU presentation sample the finished render-target image into the fitted
rectangle. Blazor retains its SKGLView frame scheduler and GPU-only rendering. Nearest-neighbor
and unscaled integer-offset presentation draw the surface directly. SkiaSharp 3.119.2 DrawSurface
has no sampling-options overload and ignores paint filtering, as verified by a pixel regression
test. Linear scaling therefore uses a scoped GPU-backed snapshot with explicit SKSamplingOptions;
it does not read pixels back to the CPU or add a second render target.

Bitmap dirty regions remain logical. WinForms maps invalidation edges outward with floor/ceil,
including a texel halo for linear sampling, then paints from a retained complete snapshot.
Expose and resize paints can reuse that image without a new logical render. Full-image updates
invalidate margins too, preventing stale pixels after an explicit aspect-ratio change.
Avalonia still uploads logical dirty patches into a retained logical WriteableBitmap; its compositor
fits that bitmap to the destination. Blazor uploads dirty patches into a retained logical canvas,
then uses Canvas 2D drawImage for fitted presentation. Adapter resize preserves that logical canvas.
GPU paths still present full frames and do not acquire dirty-region bookkeeping.

Captures and screenshots remain Backbuffer snapshots at logical resolution. DirectDrawing
continues to operate inside the logical buffer. TextBlock's scene-mode font, padding, shadow,
and outline zoom correction is unchanged; view-mode text remains screen-sized. Presentation
scales the completed result once. Darkness-overlay world projections and rectangle-local shader
transforms also remain independent of adapter size.

## Validation and performance

The Release solution build passes across core, WinForms, Avalonia, Blazor, hosting packages,
and demos. All 310 tests in Gondwana.Tests pass, and both Node browser-helper tests pass.
Workload restore (with advertising-manifest updates skipped) and Release package restore succeeded.

The core regression coverage includes reduced, default, supersampled, fractional, and minimum
resolutions; invalid scale values; first-layout establishment; repeated resize identity/canvas
stability; deferred explicit changes; centered letterboxing; dirty-edge expansion; ScreenPx to
WorldPx to Grid; mouse/touch widget click, focus, capture, and drag; and rendered TextBlock pixels
with non-unit zoom before and after adapter resize. Image and direct-surface presentation tests
verify filtering and margin clearing. GPU host tests exercise its full-frame path using the
existing CPU fallback surface; they do not claim hardware/driver coverage.

Run the browser helper tests with:

```console
node --test Testing/BlazorPresentationTests.mjs
```

The optional CPU presentation microbenchmark is outside the solution and adds no benchmark
framework dependency:

```console
dotnet run --project Testing/ViewportScalingBenchmark/ViewportScalingBenchmark.csproj -c Release
```

On the Windows development host (.NET 8.0.425 SDK, SkiaSharp 3.119.2), 200 measured frames after
20 warmup frames produced the following representative results using the explicit sampling API:

| Logical image | Destination | Filter | Milliseconds/frame |
| --- | --- | --- | ---: |
| 1920 x 1080 | 1920 x 1080 | Linear | 1.25 |
| 1920 x 1080 | 1920 x 1080 | NearestNeighbor | 1.22 |
| 1920 x 1080 | 3840 x 2160 | Linear | 29.06 |
| 1920 x 1080 | 3840 x 2160 | NearestNeighbor | 9.08 |

The measured loop allocated zero managed bytes per draw. These are CPU raster presentation
measurements, including background clear, with a prebuilt gradient image; they exclude scene
rendering, uploads, UI dispatch, and the desktop compositor. They are not GPU/WebGL measurements
or frame-rate guarantees. Linear 4K CPU scaling can exceed a 60 Hz frame budget on this host.
The compatibility path is correct, but GPU presentation is preferable when that cost matters.
Native interactive GPU, browser device-pixel-ratio, and fullscreen smoke checks remain useful
release validation across supported drivers and platforms.

Changelog updates are generated by the existing PR workflow; generated release history is not
edited manually for this change.
