Gondwana's Effects system applies time-based transitions to an entire `View` or
`SceneLayer`. It is designed for scene transitions, camera presentation, layer
reveals, split-screen transitions, impacts, and similar effects that should not
rewrite the positions or collision geometry of game objects.

Every render surface owns an `EffectsManager`, exposed as
`RenderSurfaceHostBase.Effects`. Create an effect, optionally subscribe to its
lifecycle events, and pass it to that manager. The engine advances it on the
normal render cadence; game code should not create an `EffectsManager` or tick
an effect itself.

This page describes the API and behavior on Gondwana `master` as of August 30,
2026.

## Table of contents

- [The mental model](#the-mental-model)
- [Quick start](#quick-start)
- [How effects are implemented](#how-effects-are-implemented)
- [Manager ownership and valid targets](#manager-ownership-and-valid-targets)
- [Lifecycle, status, progress, and events](#lifecycle-status-progress-and-events)
- [Cancellation and restoration](#cancellation-and-restoration)
- [Channels, composition, and replacement](#channels-composition-and-replacement)
- [Duration and easing](#duration-and-easing)
- [Directions](#directions)
- [Effect reference](#effect-reference)
  - [FadeInEffect](#fadeineffect)
  - [FadeOutEffect](#fadeouteffect)
  - [SlideInEffect](#slideineffect)
  - [SlideOutEffect](#slideouteffect)
  - [FillEffect](#filleffect)
  - [EraseEffect](#eraseeffect)
  - [ZoomInEffect](#zoomineffect)
  - [ZoomOutEffect](#zoomouteffect)
  - [EarthquakeEffect](#earthquakeeffect)
- [Sequencing patterns](#sequencing-patterns)
- [Monitoring and bulk control](#monitoring-and-bulk-control)
- [Target scope and rendering behavior](#target-scope-and-rendering-behavior)
- [Common pitfalls](#common-pitfalls)
- [Quick reference](#quick-reference)

## The mental model

An effect has four important dimensions:

1. **Host** — the render surface whose manager owns and advances it.
2. **Target** — one `View` or one `SceneLayer` owned by that host.
3. **Channel** — opacity, reveal, transform, or zoom.
4. **Lifecycle** — pending, running, then completed or cancelled.

The target controls the scope of the transition:

- A **View fade, wipe, slide, or earthquake** applies to that view as a composed
  presentation: its background, all scene layers drawn through it, and its
  view-based direct drawings. Zoom instead changes that view's viewport scale;
  screen-space view overlays remain screen-sized.
- A **SceneLayer effect** applies only to that layer's content. Other layers and
  view-based overlays are unaffected.

Except for zoom, effects write private presentation values consumed by the
renderer. These values do not change sprite positions, a layer's `OriginPx`,
camera world position, or collision shapes. Zoom changes `Viewport.Zoom`, which
is also presentation state; it still does not rescale world or collision data.

This distinction has practical consequences. A fully faded or erased target is
not disabled: its game objects can continue to update, collide, and receive
input. Make gameplay-state changes explicitly when the transition completes.

## Quick start

Import the effect namespace and, when selecting an easing curve, the movement
easing namespace:

```csharp
using Gondwana.Effects;
using Gondwana.Physics.Movement.Easing;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
```

Assuming `surface` is the render surface host and `view` and `layer` belong to
it:

```csharp
var fade = new FadeOutEffect(
    durationSeconds: 0.4f,
    easing: EasingKind.EaseInOutCubic);

fade.Completed += _ => Console.WriteLine("Fade finished");
fade.Cancelled += _ => Console.WriteLine("Fade was interrupted");

surface.Effects.Run(view, fade);

surface.Effects.Run(
    layer,
    new SlideInEffect(
        EffectDirection.FromLeftToRight,
        durationSeconds: 0.7f));
```

`Run` returns the same concrete effect instance, so this is also valid:

```csharp
FadeOutEffect fade = surface.Effects.Run(
    view,
    new FadeOutEffect(0.4f));
```

Do not call an update method in game code. `EffectsManager` advances registered
effects automatically while the Gondwana engine cycle is running.

## How effects are implemented

At a user-facing architectural level, an effect proceeds as follows:

1. `surface.Effects.Run(target, effect)` validates that the target belongs to
   `surface` and supports that effect type.
2. The manager resolves any effect already occupying the same target and
   channel.
3. The new effect captures the target's current presentation state and applies
   its starting value.
4. On each foreground/render update, the manager computes elapsed time and
   exposes raw normalized `Progress`. Fade, slide, wipe, and earthquake apply
   their eased presentation values there. Zoom's visible interpolation is
   advanced separately by the view/viewport update path; the manager tracks its
   lifecycle and final snap.
5. The manager requests a full scene refresh. The bitmap and GPU render paths
   consume the same opacity, reveal, transform, and zoom state when composing
   the next frame.
6. At the requested duration, the effect sets its final state, changes to
   `Completed`, raises `Completed`, and is then removed from the active list.

Opacity is implemented as a composited render layer. Reveal is an axis-aligned
clip rectangle. Slide and shake contribute screen-space offsets to Gondwana's
world/screen transforms. Zoom delegates visual interpolation to the existing
`Viewport` zoom animator. This is why effects work at the view/layer level
without visiting and mutating every drawable.

The engine deliberately treats active effects as transient runtime state. Do
not use an effect as the authoritative record of a door being closed, a player
being inactive, or a level being unloaded. Keep that state in game logic and
use an effect to present the change.

Active manager state is not a resumable save-game timeline, and a
`SceneLayer`'s effect presentation fields are excluded from JSON serialization.
After loading state, reconstruct any transition that should still be visible.

### Type hierarchy and custom effects

The namespace exposes these abstract bases:

- `DisplayEffect`
- `FadeEffect`
- `SlideEffect`
- `WipeEffect`
- `ZoomEffect`

They make the built-in type relationships visible, but `DisplayEffect`'s
construction and implementation hooks are restricted to the Gondwana assembly.
It is therefore **not an extension point for effects implemented in a normal
game project**. Use one of the nine concrete effects on this page. For a custom
presentation operation, use an appropriate drawing/render hook or contribute a
new engine effect rather than trying to subclass `DisplayEffect` externally.

## Manager ownership and valid targets

Each `RenderSurfaceHostBase` constructs and retains exactly one manager:

```csharp
EffectsManager effects = surface.Effects;
```

The game-facing manager API is:

```csharp
public ReadOnlyCollection<DisplayEffect> ActiveEffects { get; }

public TEffect Run<TEffect>(View target, TEffect effect)
    where TEffect : DisplayEffect;

public TEffect Run<TEffect>(SceneLayer target, TEffect effect)
    where TEffect : DisplayEffect;

public void Cancel(DisplayEffect effect);
public void CancelAll();
```

The manager constructor is not public; `surface.Effects` is the entry point.

Use that manager only with objects owned by the same surface:

- A `View` must currently appear in `surface.ViewManager.Views`.
- A `SceneLayer` must belong to `surface.Scene` and currently appear in that
  scene's `SceneLayers` collection.

These checks are reference-based. Passing a view from another surface, a layer
from another scene, or a layer removed from the current scene throws
`ArgumentException`.

```csharp
// Correct: both references are obtained from this surface.
View view = surface.ViewManager.Views[0];
SceneLayer layer = surface.Scene.SceneLayers[0];

surface.Effects.Run(view, new FadeOutEffect(0.5f));
surface.Effects.Run(layer, new FillEffect(
    EffectDirection.FromTopToBottom,
    durationSeconds: 0.5f));
```

Fade, slide, fill, and erase support both target kinds. Zoom and earthquake
support `View` only. Unsupported combinations also throw `ArgumentException`.

The manager's lifetime is the host's lifetime. Although `EffectsManager`
implements `IDisposable`, do not put `surface.Effects` in a `using` statement or
dispose it independently. Disposing the host disposes the manager and restores
all still-running effects. Use `CancelAll()` when you merely want to stop the
current transitions.

## Lifecycle, status, progress, and events

Every concrete effect inherits this public runtime information from
`DisplayEffect`:

| Member | Meaning |
| --- | --- |
| `Guid Id` | Stable unique ID for this effect instance. |
| `float DurationSeconds` | Effective duration. Negative constructor values are clamped to `0`. |
| `EasingKind Easing` | Easing kind used by the effect lifecycle. See the zoom exception below. |
| `EffectStatus Status` | `Pending`, `Running`, `Completed`, or `Cancelled`. |
| `float Progress` | Raw, **uneased** normalized time in the range `0` through `1`. |
| `Completed` | `Action<DisplayEffect>` raised after the final state is applied. |
| `Cancelled` | `Action<DisplayEffect>` raised when the effect is cancelled or replaced. |
| `Cancel()` | Cancels a running, manager-owned effect and normally restores its captured state. |

There is no started event, progress-changed event, pause/resume API, public
target property, or public manual-update method. Poll `Progress` from your
existing game/update instrumentation when continuous observation is necessary.

The usual state transitions are `Pending` → `Running` → `Completed`, or
`Pending` → `Running` → `Cancelled` when interrupted.

An effect instance becomes single-use once the manager begins starting it. It
cannot be run again after completion or cancellation, so construct a new
instance for every transition. A validation failure that occurs before start
leaves the effect `Pending` and does not consume it.

```csharp
var first = new FadeOutEffect(0.25f);
surface.Effects.Run(view, first);

// Later: create another instance; do not reuse first.
surface.Effects.Run(view, new FadeOutEffect(0.25f));
```

`Progress` is useful for telemetry, orchestration, and debugging, but it is not
necessarily the fraction of visible distance travelled. For example, at raw
progress `0.5`, an eased effect may have applied substantially more or less
than half of its transition.

### Zero-duration effects

A duration less than or equal to zero completes during the call to `Run`. Its
final state and `Progress == 1` are applied synchronously. Subscribe before
calling `Run` if the duration might be zero:

```csharp
var effect = new FadeOutEffect(durationSeconds: 0f);

effect.Completed += _ => OnHidden();
surface.Effects.Run(view, effect); // OnHidden runs before Run returns.
```

Event handlers run synchronously on whichever thread calls `Run`, `Cancel`, or
`Dispose`, or performs the engine's effect update. Gondwana does not marshal
them to a UI thread or isolate exceptions thrown by handlers. Keep handlers
short, avoid throwing, and dispatch platform UI work when your host requires a
particular UI thread.

On normal completion, `Status` is set and `Completed` is raised just before the
manager removes the effect. A `Completed` handler can therefore momentarily see
that completed instance in a fresh `ActiveEffects` snapshot. Explicit
cancellation removes the instance before raising `Cancelled`. Once `Run`, the
update, or the cancellation call returns, the terminal effect is no longer
active.

## Cancellation and restoration

Explicit cancellation restores the presentation state captured when that
effect began:

```csharp
var slide = surface.Effects.Run(
    view,
    new SlideOutEffect(
        EffectDirection.FromTopToBottom,
        durationSeconds: 1f));

// Later, while it is still running:
slide.Cancel();
// Equivalent: surface.Effects.Cancel(slide);
```

After cancellation, `Status` is `Cancelled`, `Cancelled` has fired, and the
effect is absent from `ActiveEffects`. Calling `Cancel()` on a pending,
completed, cancelled, or otherwise inactive effect has no effect.

Normal completion is different: most effects intentionally retain their final
presentation state. The exact behavior is:

| Effect family | State after completion |
| --- | --- |
| Fade in/out | Fully opaque / fully transparent. |
| Slide in/out | Normal offset / off-screen offset. |
| Fill/erase | Fully revealed / fully clipped. |
| Zoom in/out | Requested zoom after clamping. |
| Earthquake | Original transform restored. |

Once an effect has completed, cancelling it cannot undo that final state. Run
the complementary effect to animate back:

```csharp
var exit = new FadeOutEffect(0.3f);

exit.Completed += _ =>
{
    // FadeOut left opacity at zero; FadeIn starts there and returns to one.
    surface.Effects.Run(view, new FadeInEffect(0.3f));
};

surface.Effects.Run(view, exit);
```

There are two cases in which a cancelled effect is deliberately *not* restored:

- A same-target, same-channel replacement preserves the current presentation
  value so the replacement can continue without a one-frame reset.
- If the target stops belonging to the manager—for example, a layer is removed
  or views are cleared—the next manager update cancels the orphaned effect
  without writing back to the detached object.

If state restoration matters during a scene/view reconfiguration, call
`CancelAll()` before removing targets or rebinding the surface.

`Cancelled` can therefore mean an explicit cancellation, same-channel
replacement, `CancelAll`, manager/host disposal, or loss of target ownership.
Use game-level context if those causes need different policies; the event does
not include a cancellation-reason value.

## Channels, composition, and replacement

The channel is an internal implementation detail, but understanding it is
essential for composing effects predictably:

| Conceptual channel | Effects |
| --- | --- |
| Opacity | `FadeInEffect`, `FadeOutEffect` |
| Transform | `SlideInEffect`, `SlideOutEffect`, `EarthquakeEffect` |
| Reveal | `FillEffect`, `EraseEffect` |
| Zoom | `ZoomInEffect`, `ZoomOutEffect` |

At most one effect may occupy a channel for a particular target. Starting
another effect on the same target and channel cancels and replaces the old one:

```csharp
var fadingOut = surface.Effects.Run(view, new FadeOutEffect(1f));

// If fadingOut is still running, this marks it Cancelled and starts from the
// current opacity rather than restoring opacity first.
surface.Effects.Run(view, new FadeInEffect(0.35f));
```

The replaced effect raises `Cancelled`, not `Completed`. The new effect captures
the value present at replacement time. Consequently, cancelling the replacement
restores that captured midpoint—not necessarily the state before the whole
chain began.

Different channels on the same target run together:

```csharp
surface.Effects.Run(
    view,
    new FadeOutEffect(0.8f, EasingKind.Linear));

surface.Effects.Run(
    view,
    new SlideOutEffect(
        EffectDirection.FromLeftToRight,
        durationSeconds: 0.8f,
        easing: EasingKind.Linear));
```

This fades and slides the view simultaneously. A third reveal effect and a zoom
effect could also run on the same view. An earthquake could not run alongside
that slide because both use the transform channel.

Channels are scoped to the **target reference**, not its visual descendants.
A view fade and a layer fade may run together because the view and layer are
different targets. Their presentation is nested, so the layer's apparent alpha
is the composition of both opacity values. Likewise, view and layer clips
intersect, and their slide offsets add.

There is no built-in effect queue, delay, group, or cross-effect timeline.
Compose simultaneous effects by calling `Run` for each one, and sequence effects
with lifecycle events or game-level state orchestration.

## Duration and easing

Durations are seconds expressed as `float`. The manager derives elapsed seconds
from Gondwana's high-resolution timer and advances effects on foreground/render
updates. Durations are not frame counts, so frame-rate changes alter sampling
smoothness rather than the requested elapsed time.

Fade, slide, fill, and erase accept an `EasingKind` from
`Gondwana.Physics.Movement.Easing`. All currently available choices are:

| Family | Values | Typical character |
| --- | --- | --- |
| Constant | `Linear` | Uniform change. |
| Ease in | `EaseInQuad`, `EaseInCubic`, `EaseInQuart`, `EaseInQuint` | Slow start, accelerating finish; higher powers are more dramatic. |
| Ease out | `EaseOutQuad`, `EaseOutCubic`, `EaseOutQuart`, `EaseOutQuint` | Fast start, settling finish; higher powers settle more strongly. |
| Ease in/out | `EaseInOutQuad`, `EaseInOutCubic`, `EaseInOutQuart`, `EaseInOutQuint` | Slow endpoints with faster movement in the middle. |
| Smooth polynomial | `SmoothStep`, `SmootherStep` | Symmetric, smooth endpoint motion. |

The defaults are deliberately different by effect:

| Effects | Default easing |
| --- | --- |
| `FadeInEffect`, `FadeOutEffect` | `Linear` |
| `FillEffect`, `EraseEffect` | `Linear` |
| `SlideInEffect` | `EaseOutCubic` |
| `SlideOutEffect` | `EaseInCubic` |
| `EarthquakeEffect` | Fixed `Linear` progress; no easing argument. |
| `ZoomInEffect`, `ZoomOutEffect` | No easing argument; see below. |

Ease-out usually works well for something arriving and settling; ease-in works
well for something accelerating away. Quartic and quintic curves can appear to
pause at one end when the duration is short.

Effects accept an enum, not a custom easing delegate. `EasingFunctions.From`
can be used elsewhere in game code, but passing a custom function into a built-in
effect is not supported.

### The zoom easing exception

Zoom effects inherit `Easing == EasingKind.Linear`, and their lifecycle
`Progress` is linear. The visible zoom is actually performed by
`Viewport.ZoomToOverDuration`, which uses a normalized exponential ease-out and
reaches the exact target at the requested duration. Zoom constructors do not
accept an `EasingKind`.

Treat `Progress` as lifecycle progress for zoom, not as a direct description of
the current `Viewport.Zoom` interpolation.

## Directions

`EffectDirection` supplies eight cardinal or diagonal directions plus `None`:

| Direction | Slide travel | Fill origin |
| --- | --- | --- |
| `FromLeftToRight` | Right | Left edge |
| `FromRightToLeft` | Left | Right edge |
| `FromTopToBottom` | Down | Top edge |
| `FromBottomToTop` | Up | Bottom edge |
| `FromTopLeftToBottomRight` | Down and right | Top-left corner |
| `FromTopRightToBottomLeft` | Down and left | Top-right corner |
| `FromBottomLeftToTopRight` | Up and right | Bottom-left corner |
| `FromBottomRightToTopLeft` | Up and left | Bottom-right corner |

`EffectDirection.None` is not valid for slide, fill, or erase constructors and
throws `ArgumentOutOfRangeException`.

For slides, the name describes travel. A clean
`SlideInEffect(FromLeftToRight, ...)` begins one viewport width to the left and
moves right into place; a `SlideOutEffect` with the same direction exits to the
right.

For fills, the name describes the source and travel of the growing clip. For
erases, Gondwana reduces that same source-anchored clip. This reverses the
visible erasing front:

- `FillEffect(FromLeftToRight, ...)` reveals left to right.
- `EraseEffect(FromLeftToRight, ...)` erases right to left.
- To visibly erase left to right, use `FromRightToLeft`.

Diagonal fill/erase effects use an axis-aligned rectangle whose width and
height change together. They are corner-expanding/corner-contracting rectangular
wipes, not triangular or angled-edge masks. With linear easing, a diagonal
fill's visible area grows roughly with the square of progress.

## Effect reference

### FadeInEffect

Fades a `View` or `SceneLayer` to full opacity.

```csharp
public FadeInEffect(
    float durationSeconds,
    EasingKind easing = EasingKind.Linear)
```

If the target is effectively fully opaque (`>= 0.9999f`) when the effect starts,
`FadeInEffect` first makes it transparent and then animates to opacity `1`. This
makes it useful as an entrance effect without requiring a separate "hidden"
setup call. If the target is partially transparent—such as midway through a
fade-out replacement—it continues from that opacity instead.

```csharp
var entrance = new FadeInEffect(
    durationSeconds: 0.45f,
    easing: EasingKind.SmoothStep);

entrance.Completed += _ => EnableMenuInput();
surface.Effects.Run(view, entrance);
```

Completion leaves the target fully opaque. Explicit cancellation restores the
opacity captured immediately before the fade-in began.

### FadeOutEffect

Fades a `View` or `SceneLayer` from its current opacity to fully transparent.

```csharp
public FadeOutEffect(
    float durationSeconds,
    EasingKind easing = EasingKind.Linear)
```

```csharp
var exit = new FadeOutEffect(
    durationSeconds: 0.35f,
    easing: EasingKind.EaseInQuad);

exit.Completed += _ => AdvanceGameState();
surface.Effects.Run(layer, exit);
```

Completion leaves opacity at `0`; it does not change `SceneLayer.Visible`, stop
updates, or disable collisions. Explicit cancellation returns to the opacity
captured at start.

Fade-in and fade-out share the opacity channel and replace each other on the
same target.

### SlideInEffect

Slides a `View` or `SceneLayer` into its normal presentation position.

```csharp
public SlideInEffect(
    EffectDirection direction,
    float durationSeconds,
    EasingKind easing = EasingKind.EaseOutCubic)

public EffectDirection Direction { get; }
```

On a normally positioned target, the effect starts one full viewport width,
height, or both away on the side opposite the travel direction, then ends at
zero presentation offset.

```csharp
surface.Effects.Run(
    layer,
    new SlideInEffect(
        EffectDirection.FromBottomToTop,
        durationSeconds: 0.6f,
        easing: EasingKind.EaseOutQuart));
```

If a nonzero **factor offset** is already in progress or persisted from an
earlier slide, a slide-in continues from that factor rather than teleporting to
the edge implied by `Direction`. In that case the supplied direction does not
determine the visible starting location. A pixel-only offset does not trigger
this continuation rule; for example, replacing an earthquake with a slide-in
still initializes the slide factor at the requested edge while blending the
captured pixel offset toward zero.

Completion leaves factor and pixel offsets at zero. Cancellation restores both
offsets captured at start.

### SlideOutEffect

Slides a `View` or `SceneLayer` away from its current presentation position.

```csharp
public SlideOutEffect(
    EffectDirection direction,
    float durationSeconds,
    EasingKind easing = EasingKind.EaseInCubic)

public EffectDirection Direction { get; }
```

The destination is the absolute travel factor for the direction, not "one more
viewport" added to the existing factor. The effect also blends any captured
pixel offset toward zero.

```csharp
var slide = new SlideOutEffect(
    EffectDirection.FromTopRightToBottomLeft,
    durationSeconds: 0.5f);

slide.Completed += _ => RemoveTransientLayer();
surface.Effects.Run(layer, slide);
```

A cardinal factor of `1` equals the target viewport's width or height. A diagonal
slide covers one full width and one full height and therefore travels farther in
pixels than a cardinal slide of the same duration.

Completion leaves the target at the off-screen factor. Run `SlideInEffect` to
bring it back, or cancel the slide-out while it is running to restore its
captured transform.

### FillEffect

Directionally reveals a `View` or `SceneLayer` by growing a rectangular clip.

```csharp
public FillEffect(
    EffectDirection direction,
    float durationSeconds,
    EasingKind easing = EasingKind.Linear)

public EffectDirection Direction { get; }
```

If the target is effectively fully revealed (`>= 0.9999f`), the effect begins at
reveal `0` and animates to `1`. If it is partially revealed, it continues from
the current amount. This supports a smooth fill replacing an erase.

```csharp
surface.Effects.Run(
    view,
    new FillEffect(
        EffectDirection.FromTopLeftToBottomRight,
        durationSeconds: 0.8f,
        easing: EasingKind.SmootherStep));
```

Completion leaves the target fully revealed and retains the selected reveal
direction. Cancellation restores both the reveal amount and direction captured
at start.

### EraseEffect

Directionally removes a `View` or `SceneLayer` by shrinking its reveal clip to
zero.

```csharp
public EraseEffect(
    EffectDirection direction,
    float durationSeconds,
    EasingKind easing = EasingKind.Linear)

public EffectDirection Direction { get; }
```

The clip stays anchored at the direction's **source**, so the visible erase
front travels opposite the enum wording. To erase a layer visibly from left to
right, anchor the retained rectangle at the right:

```csharp
surface.Effects.Run(
    layer,
    new EraseEffect(
        EffectDirection.FromRightToLeft, // visible removal moves left -> right
        durationSeconds: 0.55f));
```

Completion leaves reveal at `0` but does not set `Visible = false` or change
gameplay state. Cancellation restores both the captured reveal amount and its
previous direction.

Fill and erase share the reveal channel and replace each other on the same
target.

### ZoomInEffect

Animates a `View` to an explicitly supplied positive zoom factor.

```csharp
public ZoomInEffect(float targetZoom, float durationSeconds)

public float TargetZoom { get; }
```

```csharp
surface.Effects.Run(
    view,
    new ZoomInEffect(
        targetZoom: 2f,
        durationSeconds: 0.75f));
```

At start, the actual target is clamped to `view.MinZoom` and `view.MaxZoom`
(defaults `0.1f` and `8f`). `TargetZoom` continues to report the requested value,
not the clamped value. A target less than or equal to zero throws
`ArgumentOutOfRangeException`.

Keep custom bounds valid as `0 < view.MinZoom <= view.MaxZoom`. A reversed range
causes clamping to throw during `Run`, while nonpositive bounds can permit an
invalid effective viewport zoom even when the requested target is positive.

The class name communicates intent; it does not enforce that `targetZoom` is
larger than the current zoom. Passing a smaller value zooms out. The visible
interpolation uses the viewport's fixed-duration exponential ease-out and does
not accept an `EasingKind`.

Completion snaps to the clamped target. Cancellation snaps back to the
`Viewport.Zoom` captured at start.

### ZoomOutEffect

Animates a `View` to an explicitly supplied positive zoom factor, commonly one
smaller than its current zoom.

```csharp
public ZoomOutEffect(float targetZoom, float durationSeconds)

public float TargetZoom { get; }
```

```csharp
surface.Effects.Run(
    view,
    new ZoomOutEffect(
        targetZoom: 0.75f,
        durationSeconds: 0.5f));
```

`ZoomOutEffect` and `ZoomInEffect` have the same implementation semantics; only
their names express the intended direction. `ZoomOutEffect` does not reject a
target larger than the current zoom.

The target is clamped at start, completion snaps exactly to it, and cancellation
restores the starting zoom. Zoom effects support `View` only and replace each
other on the zoom channel.

Avoid running direct `Viewport.ZoomTo`, `ZoomToOverDuration`, `SnapZoom`, or an
anchored zoom concurrently with an Effects-managed zoom. Those APIs share the
same viewport animator but do not participate in the Effects manager's channel
replacement rules; completion or cancellation of the effect can snap over the
other operation.

### EarthquakeEffect

Applies randomized screen-pixel camera shake to a `View` and restores the
captured transform when finished.

Despite the camera-shake use case, this does not move `view.Camera`. It offsets
the complete view presentation, so view-based screen overlays shake with the
scene.

```csharp
public EarthquakeEffect(
    float durationSeconds,
    float intensityPx = 8f,
    bool decay = true,
    int? randomSeed = null)

public float IntensityPx { get; }
public bool Decay { get; }
```

```csharp
var impact = new EarthquakeEffect(
    durationSeconds: 0.4f,
    intensityPx: 12f,
    decay: true,
    randomSeed: 1234);

surface.Effects.Run(view, impact);
```

Each update chooses independent X and Y offsets between negative and positive
amplitude. `IntensityPx` is therefore the maximum magnitude on each axis; a
diagonal sample can have a larger vector length. With the default `decay: true`,
amplitude falls linearly to zero. With `decay: false`, it stays constant until
the final update restores the original transform.

For a nonzero duration and positive intensity, the first random displacement is
applied synchronously during `Run`, at raw progress zero.

A supplied `randomSeed` makes the random sequence repeatable for the same update
sampling pattern, which is useful in tests and deterministic capture workflows.
Without one, the effect chooses a random seed. A negative intensity throws
`ArgumentOutOfRangeException`; zero intensity is valid and runs a no-motion
lifecycle.

Earthquake supports `View` only. It shares the transform channel with both slide
effects, so a shake replaces a slide on the same view and vice versa. Run it
alongside fades, wipes, or zooms when simultaneous impact feedback is desired.

## Sequencing patterns

### Chain effects with `Completed`

Construct and subscribe before running the first effect:

```csharp
var leave = new SlideOutEffect(
    EffectDirection.FromLeftToRight,
    durationSeconds: 0.45f);

leave.Completed += _ =>
{
    SwapSceneContent();

    surface.Effects.Run(
        view,
        new SlideInEffect(
            EffectDirection.FromLeftToRight,
            durationSeconds: 0.45f));
};

surface.Effects.Run(view, leave);
```

Starting the next effect from `Completed` is supported. The new effect begins at
the current final presentation state and advances on subsequent engine updates.

### Handle interruption explicitly

If the sequence owns game state as well as visuals, handle both terminal paths:

```csharp
var transition = new FadeOutEffect(0.3f);

transition.Completed += _ => CommitTransition();
transition.Cancelled += _ => AbortTransition();

surface.Effects.Run(view, transition);
```

A replaced effect takes the cancellation path. Do not put essential cleanup
only in `Completed` unless interruption truly should skip it.

### Await an effect from asynchronous orchestration

Effects are event-based, but a game-level state controller can bridge one to a
task. Attach handlers before `Run` so immediate completion is safe:

```csharp
var completion = new TaskCompletionSource<bool>(
    TaskCreationOptions.RunContinuationsAsynchronously);

var effect = new FadeOutEffect(0.4f);

effect.Completed += _ => completion.TrySetResult(true);
effect.Cancelled += _ => completion.TrySetCanceled();

surface.Effects.Run(view, effect);
await completion.Task;
```

Use your normal game-state cancellation policy around this pattern. Cancelling
the task by itself does not cancel the display effect; call `effect.Cancel()` as
well if the two lifecycles must be coupled.

### Coordinate simultaneous effects

Starting multiple channels is immediate; completion is independent. Count or
await each effect if an operation must wait for the whole group:

```csharp
var fade = new FadeOutEffect(0.5f);
var slide = new SlideOutEffect(
    EffectDirection.FromBottomToTop,
    durationSeconds: 0.5f);

int remaining = 2;
void OneFinished(DisplayEffect _)
{
    if (--remaining == 0)
        OnExitAnimationFinished();
}

fade.Completed += OneFinished;
slide.Completed += OneFinished;

surface.Effects.Run(view, fade);
surface.Effects.Run(view, slide);
```

If cancellation should also count as terminal, subscribe the same handler to
`Cancelled`, taking care that your orchestration policy distinguishes success
from interruption when necessary.

## Monitoring and bulk control

`ActiveEffects` returns a read-only **membership snapshot** of effects currently
owned by the manager. Adding or removing effects does not change a snapshot you
already obtained. The contained `DisplayEffect` references are still live,
however, so their `Status` and `Progress` can continue changing.

```csharp
foreach (DisplayEffect effect in surface.Effects.ActiveEffects)
{
    Console.WriteLine(
        $"{effect.Id}: {effect.GetType().Name} " +
        $"{effect.Status} {effect.Progress:P0}");
}
```

Use the snapshot for diagnostics, UI transition indicators, or deciding whether
your state controller already has an effect in flight. Do not mutate it or use
its count as authoritative gameplay state.

To cancel one known effect:

```csharp
surface.Effects.Cancel(effect);
// or effect.Cancel();
```

To cancel every running effect on that surface and restore each captured state:

```csharp
surface.Effects.CancelAll();
```

`CancelAll` affects only this host. In a multi-surface application, call it on
each host whose effects you intend to stop.

The manager clears its existing active snapshot before raising cancellation
callbacks. If a `Cancelled` handler starts a new effect, that newly started
effect is not part of the in-progress `CancelAll()` operation and can remain
active afterward.

## Target scope and rendering behavior

### View targets

View-level opacity, reveal, and transform effects wrap the complete presentation
group for that viewport. Zoom changes the viewport's world/screen scale
separately. This means:

- A view fade affects its background, scene layers, and view-based overlays.
- A view wipe clips that whole group.
- A view slide offsets its coordinate transforms and presentation bounds.
- A partially transparent, wiped, or translated upper view can reveal a lower
  Z-order view underneath it.
- A view zoom changes the visible world extent and screen/world conversion for
  that view. View-mode direct drawings expressed in screen coordinates remain
  screen-sized rather than being scaled like world content.
- Post-scene canvas hooks run after the view/layer presentation groups and are
  not faded, clipped, or translated by these effects.

### SceneLayer targets

A layer effect wraps only that layer wherever the bound scene renders it:

- Other layers remain unchanged.
- View-based overlays remain unchanged.
- If multiple views show the same scene layer, the layer effect appears in all
  of them.
- A layer slide's factor is converted using each rendering view's viewport size.

View and layer presentation values nest. This enables combinations such as a
whole-view fade while one layer wipes, or a view slide plus a separate layer
slide.

### Coordinate conversion and gameplay state

Slide offsets are part of Gondwana's screen/world transform. Calls such as
`WorldPxToScreenPx` and `ScreenPxToWorldPx` account for the active effect offset,
so picking and round-trip coordinate conversion stay aligned with the shifted
presentation. The underlying camera position and layer origin remain unchanged.

Fade and erase do not change hit-testing or collision by themselves. If hidden
content should stop interacting, coordinate that behavior in game logic.

### Refresh and backbuffers

Normal starts, advances, completions, and successful explicit/bulk cancellations
request a full scene refresh. An orphaned target is the exception: its effect is
cancelled without restoration or invalidation. Gondwana's bitmap path recomposes
view presentation when necessary; the GPU path draws the full frame. The same
effect API is used for both paths, and current regression tests verify view and
layer opacity composition through both the bitmap and GPU/full-frame host paths.

## Common pitfalls

### Expecting an entry effect to be a no-op on a normal target

`Run` applies the effect's initial presentation synchronously. A fade-in on a
fully opaque target starts it transparent, a fill on a fully revealed target
starts it clipped, and a slide-in on a normally positioned target starts it
off-screen. That reset is what makes these useful as entry effects.

### Reusing an effect instance

Instances are one-shot. Always construct a new effect for another run.

### Assuming completion restores state

Only earthquake restores its transform on normal completion. Fade-out, slide-out,
and erase deliberately leave the target hidden/off-screen. Use their counterpart
to animate back.

### Assuming cancellation and replacement are identical

Explicit cancellation restores the state captured at start. Replacement does
not restore; it preserves the midpoint for continuity and then lets the new
effect capture it.

### Expecting slide and earthquake to compose

They share the transform channel on a view. Starting one replaces the other.
Fade, reveal, and zoom use independent channels and can run with either.

### Reading `Progress` as eased visual progress

`Progress` is normalized elapsed time. Apply the effect's easing conceptually
when interpreting it, and remember that zoom has its own viewport curve.

### Attaching handlers after `Run`

Zero or negative durations complete inside `Run`. Subscribe first.

### Misreading erase direction

Erase shrinks a clip anchored at the named source. Its visible removal front
moves opposite the enum name. Test the intended direction or use the inverse
direction from the equivalent fill.

### Expecting a diagonal line wipe

Diagonal fills and erases scale a corner-anchored rectangle in both axes. The
mask edge is not angled.

### Treating `ZoomInEffect` and `ZoomOutEffect` as validation

The two types express intent only. Both accept any positive target and can move
in either direction; the view's min/max range clamps the actual destination.

### Mixing direct viewport zoom with an effect zoom

Direct viewport zoom operations do not occupy the Effects manager's zoom
channel. They can overwrite the same viewport animator, and an effect's final
snap or cancellation can overwrite them in return.

### Removing a target before cancelling its effect

An effect whose target no longer belongs to the host is cancelled without
restoration on the next update. Call `Cancel` or `CancelAll` before clearing
views, removing layers, or rebinding when restoration matters.

### Using visual disappearance as a gameplay switch

Opacity and reveal affect rendering, not activity. Disable input, updates,
collision, or visibility explicitly at the appropriate lifecycle boundary.

### Confusing host effects with per-drawing fades

This subsystem is separate from fade/reveal operations exposed by individual
`DirectDrawing` types. `surface.Effects` targets a complete view or scene layer;
choose a drawing-level API when only one drawable should transition.

### Letting the surface sit outside the engine update loop

Effects do not own a background task. They advance when the registered surface
receives engine foreground updates, using elapsed time since the manager's prior
update. A long gap before the next update can consume most or all of a short
effect in one step, and a stopped engine does not advance effects.

## Quick reference

| Effect | Public constructor | Targets | Channel | Final state |
| --- | --- | --- | --- | --- |
| `FadeInEffect` | `(float durationSeconds, EasingKind easing = EasingKind.Linear)` | `View`, `SceneLayer` | Opacity | Fully opaque |
| `FadeOutEffect` | `(float durationSeconds, EasingKind easing = EasingKind.Linear)` | `View`, `SceneLayer` | Opacity | Fully transparent |
| `SlideInEffect` | `(EffectDirection direction, float durationSeconds, EasingKind easing = EasingKind.EaseOutCubic)` | `View`, `SceneLayer` | Transform | Normal offset |
| `SlideOutEffect` | `(EffectDirection direction, float durationSeconds, EasingKind easing = EasingKind.EaseInCubic)` | `View`, `SceneLayer` | Transform | Directional off-screen offset |
| `FillEffect` | `(EffectDirection direction, float durationSeconds, EasingKind easing = EasingKind.Linear)` | `View`, `SceneLayer` | Reveal | Fully revealed |
| `EraseEffect` | `(EffectDirection direction, float durationSeconds, EasingKind easing = EasingKind.Linear)` | `View`, `SceneLayer` | Reveal | Fully clipped |
| `ZoomInEffect` | `(float targetZoom, float durationSeconds)` | `View` only | Zoom | Clamped target zoom |
| `ZoomOutEffect` | `(float targetZoom, float durationSeconds)` | `View` only | Zoom | Clamped target zoom |
| `EarthquakeEffect` | `(float durationSeconds, float intensityPx = 8f, bool decay = true, int? randomSeed = null)` | `View` only | Transform | Original transform restored |

### Error and boundary behavior

| Condition | Result |
| --- | --- |
| Null target or effect | `ArgumentNullException`. |
| Negative duration | Clamped to zero; effect completes synchronously in `Run`. |
| `EffectDirection.None` for slide/fill/erase | `ArgumentOutOfRangeException`. |
| Zoom target `<= 0` | `ArgumentOutOfRangeException`. |
| Earthquake intensity `< 0` | `ArgumentOutOfRangeException`. |
| `view.MinZoom > view.MaxZoom` when a zoom starts | `ArgumentException` from clamping. |
| Target belongs to another host/scene | `ArgumentException`. |
| Effect does not support target kind | `ArgumentException`. |
| Reusing an effect instance whose start already began | `InvalidOperationException`. |
| Calling `Run` after manager disposal | `ObjectDisposedException`. |

The shortest reliable rule is: obtain the manager and target from the same
surface, construct a fresh effect, attach handlers before `Run`, and decide
explicitly whether completion should retain, reverse, or commit the visual
state.
