This page explains Gondwana's lightweight direct-drawing lighting helpers:

- `DirectRadialLight`
- `DirectLightLayer`
- `DirectDarknessOverlay`
- `DirectSceneLayerDarknessOverlay`

These classes provide a modular way to add torch glows, player vision, view-specific darkness, and SceneLayer-bound fog/darkness without replacing Gondwana's renderer or introducing a full lighting engine.

---

## Table of contents

- [What this feature is](#what-this-feature-is)
- [What this feature is not](#what-this-feature-is-not)
- [Mental model](#mental-model)
- [When to use each class](#when-to-use-each-class)
- [View darkness vs SceneLayer darkness](#view-darkness-vs-scenelayer-darkness)
- [Coordinate spaces](#coordinate-spaces)
- [Minimal setup checklist](#minimal-setup-checklist)
- [Recipe 1: draw one standalone torch glow](#recipe-1-draw-one-standalone-torch-glow)
- [Recipe 2: manage several torch lights](#recipe-2-manage-several-torch-lights)
- [Recipe 3: darken the view with player vision](#recipe-3-darken-the-view-with-player-vision)
- [Recipe 4: torch glow plus matching darkness reveal](#recipe-4-torch-glow-plus-matching-darkness-reveal)
- [Recipe 5: track an entire light layer](#recipe-5-track-an-entire-light-layer)
- [Recipe 6: player-held torch](#recipe-6-player-held-torch)
- [Recipe 7: manual reveal with no visible glow](#recipe-7-manual-reveal-with-no-visible-glow)
- [Recipe 8: temporary spell or pickup glow](#recipe-8-temporary-spell-or-pickup-glow)
- [Recipe 9: SceneLayer-bound fog or room darkness](#recipe-9-scenelayer-bound-fog-or-room-darkness)
- [Recipe 10: SceneLayer darkness tracking SceneLayer lights](#recipe-10-scenelayer-darkness-tracking-scenelayer-lights)
- [Recipe 11: moving fog cloud or poison haze](#recipe-11-moving-fog-cloud-or-poison-haze)
- [Tuning guide](#tuning-guide)
- [Dirty rectangles and performance](#dirty-rectangles-and-performance)
- [Layering and Z-order](#layering-and-z-order)
- [Cleanup and scene changes](#cleanup-and-scene-changes)
- [Troubleshooting](#troubleshooting)
- [API quick reference](#api-quick-reference)
- [Summary](#summary)

---

## What this feature is

Direct lighting and darkness are **DirectDrawing-based visual effects**.

They are designed for common 2D/2.5D game visuals such as:

- torch glows
- lamp glows
- player vision circles
- dungeon darkness
- fog-of-war style darkness overlays
- actual fog, smoke, haze, or poison clouds
- magic sight
- temporary spell auras
- small localized visual lighting effects

The system is intentionally small. It is meant to be something a game can opt into when needed.

The helpers are useful when you want visuals like:

```text
The player is carrying a torch.
The dungeon is dark.
The torch creates a soft visible circle.
The torch glow flickers gently.
```

They are also useful for less literal effects:

```text
A magic item glows.
A stealth enemy has a vision radius.
A cutscene spotlights one part of the map.
A hazard gives off a pulsing aura.
A swamp layer has drifting mist over part of the world.
A room contains magical darkness that every camera should see.
```

---

## What this feature is not

This is not a full lighting engine.

It does not provide:

- shadows
- wall occlusion
- ray casting
- normal maps
- physically based lighting
- light bouncing
- tile-aware visibility blocking
- automatic line-of-sight logic

The helpers draw visual effects. Game logic still decides where lights are, which objects emit light, and whether a reveal source should exist.

For example, this system can draw a torch glow around a player. It does not automatically know that a stone wall should block the torch.

If your game needs wall-aware visibility, you can build that separately and use the result to decide which reveal sources should exist, where they should be, or how large they should be.

---

## Mental model

Think of the system as three related pieces:

```text
DirectRadialLight
  draws a warm, bounded glow on a SceneLayer

DirectDarknessOverlay
  draws darkness over a View
  then cuts soft reveal holes through it

DirectSceneLayerDarknessOverlay
  draws darkness/fog over a bounded world region on one SceneLayer
  then cuts soft reveal holes through that layer-local overlay
```

Used together, a light and a View darkness overlay create the classic player-vision or torch-in-a-dark-room effect:

```text
scene tiles and sprites
+ view darkness overlay
- reveal hole around torch/player
+ warm radial torch glow
```

Used together, a light and a SceneLayer darkness overlay create a world-local fog/darkness effect:

```text
scene layer content
+ bounded layer fog/darkness region
- reveal hole around layer-local torch
+ warm radial torch glow on that same layer
```

The darkness reveal and the visible light glow are separate on purpose.

That gives you several useful combinations:

| Combination | Result | Typical use |
|---|---|---|
| Light only | Warm glow over normal scene | lamps, spell glows, engine exhaust |
| View darkness only | Whole view darkened | cutscene fade, night vision baseline |
| View darkness + manual reveal | Player vision / stealth vision / fog-of-war reveal | per-player visibility |
| View darkness + tracked light | Torch glow and View reveal hole move together | dungeon exploration |
| SceneLayer darkness only | Bounded fog/darkness exists in the world | swamp mist, room darkness |
| SceneLayer darkness + tracked light | Layer-local fog/darkness reveals around layer-local lights | smoke/fog affected by torches on that layer |

---

## When to use each class

### Use `DirectRadialLight` when you want a visible glow

Use this when the effect itself should be visible:

- torch glow
- lantern glow
- spell aura
- explosion flash
- glowing crystal
- engine exhaust glow
- warm window light

You can use `DirectRadialLight` directly. You do **not** need `DirectLightLayer`.

### Use `DirectLightLayer` when you want to manage multiple lights

`DirectLightLayer` is a convenience owner/factory.

It is **not** a `SceneLayer`.

Use it when you want:

- a list of lights
- quick torch defaults
- easier cleanup
- `TrackLightLayer(...)` support from the darkness overlay

For one or two lights, direct `DirectRadialLight` construction is fine.

For many lights, `DirectLightLayer` keeps code cleaner.

### Use `DirectDarknessOverlay` when you want the view darkened

Use this when you want:

- a dark dungeon
- fog-like screen darkening
- player vision circles
- visibility holes around torches
- stealth / sight radius effects

`DirectDarknessOverlay` is view-space. It covers the target `View`.

Reveal sources are defined in world-space and projected through a scene layer.

### Use `DirectSceneLayerDarknessOverlay` when darkness/fog belongs to the world

Use this when the effect is something that physically exists on a specific `SceneLayer`:

- swamp mist
- smoke clouds
- poison gas
- dust haze
- magical darkness in one room
- localized fog over terrain
- low cloud layer over a graveyard

`DirectSceneLayerDarknessOverlay` is SceneLayer-space. It has `WorldBounds`, participates in that layer's draw order, and is visible to any View that sees that part of the layer.

It can also track `DirectRadialLight` instances, but only lights on the same `SceneLayer`. That is intentional. This helper does **not** implement cross-layer light spill.

---

## View darkness vs SceneLayer darkness

The main design choice is whether the darkness/fog is about **what a viewer can see** or **what exists in the world**.

Use `DirectDarknessOverlay` when the question is:

```text
What does this View/player/camera get to see?
```

Good fits:

- player vision
- fog-of-war reveal
- stealth visibility
- split-screen player-specific darkness
- minimap reveal rules
- per-camera night vision

Use `DirectSceneLayerDarknessOverlay` when the question is:

```text
What fog/darkness exists at this place on this SceneLayer?
```

Good fits:

- actual fog
- smoke
- poison clouds
- room-local magical darkness
- layer-specific haze
- environmental mist that all players should see

The quick test is:

> If two Views look at the same world area, should both Views see the same effect?

If yes, use SceneLayer-bound darkness.

If no, use View-bound darkness.

Examples:

| Effect | Better fit | Why |
|---|---|---|
| Player torch visibility | `DirectDarknessOverlay` | Each player/View may have different visibility |
| Fog-of-war | `DirectDarknessOverlay` | It represents player knowledge or exploration |
| Split-screen PvP darkness | `DirectDarknessOverlay` | Each player can have separate reveal rules |
| Swamp mist | `DirectSceneLayerDarknessOverlay` | It physically exists on the map |
| Poison gas cloud | `DirectSceneLayerDarknessOverlay` | It has a world position and should be visible to any camera |
| Dark room region | `DirectSceneLayerDarknessOverlay` | The room itself is dark, independent of the viewer |

---

## Coordinate spaces

The lighting helpers intentionally use Gondwana's existing coordinate model.

### `DirectRadialLight` uses world pixels

A light created at:

```csharp
new PointF(520, 320)
```

is centered at world pixel `(520, 320)` on its `SceneLayer`.

If the camera moves, the light remains attached to that world location.

If the view zooms, the light scales with the world.

### `DirectDarknessOverlay` covers a view

The darkness overlay is attached to a `View`, so it covers the view's viewport.

```csharp
var darkness = new DirectDarknessOverlay(
    renderSurfaceHost,
    mainView,
    dungeonLayer,
    "dungeon-darkness");
```

The `mainView` argument says:

```text
Cover this view with darkness.
```

The `dungeonLayer` argument says:

```text
Use this scene layer when converting reveal source world positions into screen positions.
```

For most games, use your main gameplay layer as the projection layer.

### `DirectSceneLayerDarknessOverlay` uses world bounds

SceneLayer-bound darkness is attached to a `SceneLayer` and has `WorldBounds`:

```csharp
var layerFog = new DirectSceneLayerDarknessOverlay(
    renderSurfaceHost,
    dungeonLayer,
    new Rectangle(0, 0, 1024, 768),
    "dungeon-fog");
```

The rectangle is in world pixels on `dungeonLayer`.

If the camera moves, the fog/darkness remains attached to that world region.

If two Views can see that same world region, both Views see the same fog/darkness.

Reveal sources for `DirectSceneLayerDarknessOverlay` are also world-space:

```csharp
layerFog.AddRevealSource(
    centerWorldPx: new PointF(320, 240),
    radiusWorldPx: 120f);
```

Unlike `DirectDarknessOverlay`, there is no `View` argument and no `projectionLayer` argument. The overlay already belongs to one `SceneLayer`.

---

## Minimal setup checklist

To add a basic torch-in-darkness effect, you need these existing game objects:

```text
renderSurfaceHost
mainView
main gameplay SceneLayer, e.g. dungeonLayer
```

Then add:

```csharp
using System.Drawing;
using Gondwana.Drawing.Direct;
```

Then create:

1. a light
2. a darkness overlay
3. a tracking link between them

```csharp
var torch = new DirectRadialLight(
    Color.FromArgb(180, 255, 190, 80),
    renderSurfaceHost,
    dungeonLayer,
    new PointF(520, 320),
    120f,
    "torch-01");

var darkness = new DirectDarknessOverlay(
    renderSurfaceHost,
    mainView,
    dungeonLayer,
    "dungeon-darkness")
    .SetDarknessOpacity(190);

darkness.TrackLight(torch);
```

That is the smallest complete version of the paired effect.

For actual fog or world-local darkness, the checklist is slightly different:

```text
renderSurfaceHost
SceneLayer where the fog/darkness lives
world-space Rectangle for the affected region
optional lights or reveal sources on that same SceneLayer
```

Then create a SceneLayer-bound overlay:

```csharp
var layerFog = new DirectSceneLayerDarknessOverlay(
    renderSurfaceHost,
    dungeonLayer,
    new Rectangle(0, 0, 1024, 768),
    "dungeon-layer-fog");
```

---

## Recipe 1: draw one standalone torch glow

Use this when you only want a visible glow and do not want to darken the screen.

```csharp
using System.Drawing;
using Gondwana.Drawing.Direct;

var torch = new DirectRadialLight(
    lightColor: Color.FromArgb(180, 255, 190, 80),
    renderSurfaceHost: renderSurfaceHost,
    sceneLayer: dungeonLayer,
    centerWorldPx: new PointF(520, 320),
    radiusWorldPx: 120f,
    nickname: "torch-01");

torch.Intensity = 0.85f;
torch.FlickerEnabled = true;
torch.FlickerAmount = 0.08f;
torch.FlickerRefreshHz = 12;
torch.ZOrder = 10_000;
```

What you should see:

- a warm amber glow centered at world pixel `(520, 320)`
- a bright center
- a soft fade toward the edge
- subtle flicker if `FlickerEnabled` is true

This does not darken the rest of the scene. It only draws the glow.

On a bright scene, this may look subtle. On a dark scene, it should be obvious.

---

## Recipe 2: manage several torch lights

Use `DirectLightLayer` when you want a simple owner for multiple lights.

```csharp
using System.Drawing;
using Gondwana.Drawing.Direct;

var lights = new DirectLightLayer(renderSurfaceHost, dungeonLayer);

var torchA = lights.AddTorchLight(
    centerWorldPx: new PointF(256, 256),
    radiusWorldPx: 120f,
    nickname: "torch-a");

var torchB = lights.AddTorchLight(
    centerWorldPx: new PointF(768, 256),
    radiusWorldPx: 120f,
    nickname: "torch-b");

var torchC = lights.AddTorchLight(
    centerWorldPx: new PointF(512, 640),
    radiusWorldPx: 160f,
    nickname: "torch-c");
```

Then configure the lights:

```csharp
torchA.FlickerEnabled = true;
torchB.FlickerEnabled = true;
torchC.FlickerEnabled = true;

torchA.FlickerRefreshHz = 10;
torchB.FlickerRefreshHz = 12;
torchC.FlickerRefreshHz = 9;
```

What you should see:

- three separate torch glows
- each has its own radius
- each can flicker independently

`DirectLightLayer` does not draw anything by itself. It owns the `DirectRadialLight` instances.

---

## Recipe 3: darken the view with player vision

Use this when you want darkness and a player-visible area, but no warm torch glow.

```csharp
using System.Drawing;
using Gondwana.Drawing.Direct;

var darkness = new DirectDarknessOverlay(
    renderSurfaceHost,
    mainView,
    dungeonLayer,
    "dungeon-darkness")
    .SetDarknessColor(Color.Black)
    .SetDarknessOpacity(190)
    .SetInnerClearRadiusRatio(0.25f)
    .SetMidpointRadiusRatio(0.65f)
    .SetMidpointStrength(0.45f);

var playerVision = darkness.AddRevealSource(
    centerWorldPx: playerWorldCenterPx,
    radiusWorldPx: 180f,
    nickname: "player-vision");
```

During gameplay, move the reveal source as the player moves:

```csharp
playerVision.MoveTo(playerWorldCenterPx);
```

What you should see:

- the whole view is darkened
- the player has a soft circular visible area
- the visible area follows the player
- there is no warm glow, only a visibility hole

This is useful for player vision, stealth vision, fog reveal, or line-of-sight prototypes.

---

## Recipe 4: torch glow plus matching darkness reveal

This is the common dungeon torch setup.

```csharp
using System.Drawing;
using Gondwana.Drawing.Direct;

var torch = new DirectRadialLight(
    Color.FromArgb(180, 255, 190, 80),
    renderSurfaceHost,
    dungeonLayer,
    new PointF(520, 320),
    120f,
    "torch-01");

torch.FlickerEnabled = true;
torch.FlickerAmount = 0.08f;
torch.FlickerRefreshHz = 12;
torch.Intensity = 0.85f;
torch.ZOrder = 10_000;

var darkness = new DirectDarknessOverlay(
    renderSurfaceHost,
    mainView,
    dungeonLayer,
    "dungeon-darkness")
    .SetDarknessColor(Color.Black)
    .SetDarknessOpacity(190)
    .SetInnerClearRadiusRatio(0.20f)
    .SetMidpointRadiusRatio(0.62f)
    .SetMidpointStrength(0.45f);

darkness.TrackLight(torch);
```

`TrackLight(torch)` creates a reveal source that follows the light's:

- center
- radius
- intensity, unless intensity tracking is disabled

Now this:

```csharp
torch.MoveTo(new PointF(600, 360));
```

moves both:

```text
the visible torch glow
the darkness reveal hole
```

`TrackLight(...)` is idempotent. Calling it again for the same light returns the existing reveal source and keeps the original tracking options. To change `radiusScale`, `intensityScale`, or `trackIntensity`, untrack and then track again.

What you should see:

- the whole viewport becomes dark
- the area around the torch is visible
- the reveal has a soft falloff
- the torch glow appears inside/around the revealed area
- moving the torch moves both the glow and the visibility hole

---

## Recipe 5: track an entire light layer

Use `TrackLightLayer(...)` when you want the darkness overlay to automatically reveal around every light in a `DirectLightLayer`.

```csharp
using System.Drawing;
using Gondwana.Drawing.Direct;

var lights = new DirectLightLayer(renderSurfaceHost, dungeonLayer);

var darkness = new DirectDarknessOverlay(
    renderSurfaceHost,
    mainView,
    dungeonLayer,
    "dungeon-darkness")
    .SetDarknessOpacity(200);

darkness.TrackLightLayer(lights);

var torchA = lights.AddTorchLight(new PointF(300, 240), 100f, nickname: "torch-a");
var torchB = lights.AddTorchLight(new PointF(700, 420), 140f, nickname: "torch-b");
```

Because `TrackLightLayer` is active, both torches automatically get reveal sources.

Lights added later are also tracked:

```csharp
var torchC = lights.AddTorchLight(new PointF(900, 500), 120f, nickname: "torch-c");
```

Removing a light removes its tracked reveal source:

```csharp
lights.Remove(torchB);
```

What you should see:

- the view is darkened
- each torch creates a soft reveal hole
- each torch also draws its warm glow
- adding/removing lights updates the darkness overlay tracking

This is the cleanest setup for rooms with multiple torches or lamps.

---

## Recipe 6: player-held torch

A player-held torch is usually just a light that moves to the player's world center each update.

Setup:

```csharp
using System.Drawing;
using Gondwana.Drawing.Direct;

var playerTorch = new DirectRadialLight(
    Color.FromArgb(190, 255, 190, 80),
    renderSurfaceHost,
    dungeonLayer,
    playerWorldCenterPx,
    150f,
    "player-torch");

playerTorch.FlickerEnabled = true;
playerTorch.FlickerAmount = 0.08f;
playerTorch.FlickerRefreshHz = 12;
playerTorch.Intensity = 0.90f;
playerTorch.ZOrder = 10_000;

var darkness = new DirectDarknessOverlay(renderSurfaceHost, mainView, dungeonLayer)
    .SetDarknessOpacity(200);

darkness.TrackLight(
    playerTorch,
    radiusScale: 1.15f,
    intensityScale: 1.0f);
```

During gameplay:

```csharp
playerTorch.MoveTo(playerWorldCenterPx);
```

What you should see:

- the player carries a warm flickering light
- the visible area follows the player
- the reveal is slightly larger than the glow because `radiusScale` is `1.15f`

This pattern is useful for top-down dungeon games, cave scenes, night levels, and stealth prototypes.

---

## Recipe 7: manual reveal with no visible glow

Manual reveal sources are useful when visibility should exist without a visible light effect.

Examples:

- player vision
- enemy vision
- stealth detection radius
- magic detection
- scripted cutscene spotlight
- minimap reveal logic

```csharp
var darkness = new DirectDarknessOverlay(renderSurfaceHost, mainView, dungeonLayer)
    .SetDarknessOpacity(185);

var vision = darkness.AddRevealSource(
    centerWorldPx: playerWorldCenterPx,
    radiusWorldPx: 180f,
    nickname: "player-vision");
```

During gameplay:

```csharp
vision.MoveTo(playerWorldCenterPx);
```

Change the radius when a power-up is active:

```csharp
vision.SetRadius(260f);
```

Make the reveal weaker:

```csharp
vision.Intensity = 0.50f;
```

What you should see:

- the scene is dark
- the player has a visibility circle
- no warm glow is drawn
- lower intensity leaves some darkness inside the reveal

---

## Recipe 8: temporary spell or pickup glow

A temporary glow does not need a darkness overlay.

```csharp
var pickupGlow = new DirectRadialLight(
    Color.FromArgb(150, 120, 180, 255),
    renderSurfaceHost,
    dungeonLayer,
    pickupWorldCenterPx,
    80f,
    "mana-pickup-glow");

pickupGlow.Intensity = 0.70f;
pickupGlow.FlickerEnabled = true;
pickupGlow.FlickerAmount = 0.05f;
pickupGlow.FlickerRefreshHz = 8;
pickupGlow.ZOrder = 9_000;
```

When the pickup is collected:

```csharp
pickupGlow.Dispose();
```

What you should see:

- a small colored aura around the pickup
- optional subtle shimmer/flicker
- no darkness behavior unless you also use `DirectDarknessOverlay`

---

## Recipe 9: SceneLayer-bound fog or room darkness

Use `DirectSceneLayerDarknessOverlay` when the darkness or fog is a world effect that belongs to one `SceneLayer`.

This example creates a dark/fogged rectangular area on `dungeonLayer`:

```csharp
using System.Drawing;
using Gondwana.Drawing.Direct;

var roomDarkness = new DirectSceneLayerDarknessOverlay(
    renderSurfaceHost,
    dungeonLayer,
    new Rectangle(256, 128, 512, 384),
    "north-room-darkness")
    .SetDarknessColor(Color.Black)
    .SetDarknessOpacity(180)
    .SetInnerClearRadiusRatio(0.20f)
    .SetMidpointRadiusRatio(0.65f)
    .SetMidpointStrength(0.45f);
```

What you should see:

- only the world rectangle `(256, 128, 512, 384)` is darkened
- the effect moves with `dungeonLayer` as the camera moves
- any View looking at that part of the layer sees the same darkness
- content outside the rectangle is not affected

This is a good fit for an actual dark room or a fog bank that lives on the map.

You can add a manual reveal source to it:

```csharp
var doorwayReveal = roomDarkness.AddRevealSource(
    centerWorldPx: new PointF(320, 240),
    radiusWorldPx: 96f,
    nickname: "doorway-light");

doorwayReveal.Intensity = 0.75f;
```

Manual reveal sources only cut through the SceneLayer darkness. They do not create warm glow by themselves.

---

## Recipe 10: SceneLayer darkness tracking SceneLayer lights

SceneLayer-bound darkness can track `DirectRadialLight` instances on the same `SceneLayer`.

```csharp
using System.Drawing;
using Gondwana.Drawing.Direct;

var lights = new DirectLightLayer(renderSurfaceHost, dungeonLayer);

var torch = lights.AddTorchLight(
    centerWorldPx: new PointF(320, 240),
    radiusWorldPx: 140f,
    nickname: "room-torch");

torch.FlickerEnabled = true;
torch.FlickerAmount = 0.08f;
torch.FlickerRefreshHz = 12;

var roomDarkness = new DirectSceneLayerDarknessOverlay(
    renderSurfaceHost,
    dungeonLayer,
    new Rectangle(256, 128, 512, 384),
    "north-room-darkness")
    .SetDarknessOpacity(190);

roomDarkness.TrackLight(torch, radiusScale: 1.15f);
```

Now the torch and the SceneLayer darkness reveal stay synchronized.

If the torch moves:

```csharp
torch.MoveTo(new PointF(420, 260));
```

the reveal hole moves with it.

What you should see:

```text
dungeon layer content
+ bounded room darkness/fog
- soft reveal around room torch
+ warm radial torch glow
```

### Tracking a whole light owner

If the fog/darkness should reveal around every light in a `DirectLightLayer`, track the owner:

```csharp
var lights = new DirectLightLayer(renderSurfaceHost, dungeonLayer);

var roomDarkness = new DirectSceneLayerDarknessOverlay(
    renderSurfaceHost,
    dungeonLayer,
    new Rectangle(0, 0, 1024, 768),
    "layer-fog")
    .SetDarknessOpacity(170);

roomDarkness.TrackLightLayer(lights, radiusScale: 1.10f);

lights.AddTorchLight(new PointF(256, 256), 120f, nickname: "torch-a");
lights.AddTorchLight(new PointF(640, 360), 160f, nickname: "torch-b");
```

Lights added later are tracked automatically.

Lights removed from the `DirectLightLayer` remove their tracked reveal source.

### Same-layer rule

SceneLayer-bound darkness only tracks lights from the same `SceneLayer`:

```csharp
var backgroundFog = new DirectSceneLayerDarknessOverlay(
    renderSurfaceHost,
    backgroundLayer,
    new Rectangle(0, 0, 1024, 768));

// Throws: playerTorch belongs to dungeonLayer, not backgroundLayer.
backgroundFog.TrackLight(playerTorch);
```

That is intentional. This helper does not implement cross-layer light spill.

---

## Recipe 11: moving fog cloud or poison haze

Because `DirectSceneLayerDarknessOverlay` is a normal scene-layer direct drawing, you can move its world bounds.

```csharp
var poisonCloud = new DirectSceneLayerDarknessOverlay(
    renderSurfaceHost,
    dungeonLayer,
    new Rectangle(400, 300, 240, 180),
    "poison-cloud")
    .SetDarknessColor(Color.FromArgb(80, 160, 90))
    .SetDarknessOpacity(150)
    .SetMidpointStrength(0.35f);
```

Move the whole cloud by changing its world bounds:

```csharp
var bounds = poisonCloud.DarknessWorldBounds;
bounds.Offset(1, 0);
poisonCloud.DarknessWorldBounds = bounds;
```

Add a weak reveal source if something pushes the haze away:

```csharp
var windGap = poisonCloud.AddRevealSource(
    centerWorldPx: new PointF(480, 360),
    radiusWorldPx: 80f,
    nickname: "wind-gap");

windGap.Intensity = 0.35f;
```

What you should see:

- a bounded colored haze/fog region on the SceneLayer
- it moves with the world and camera
- its dirty region follows the old and new world bounds
- any View looking at the cloud sees the same cloud

---

## Tuning guide

### Darkness opacity

Controls how dark the overlay is.

```csharp
darkness.SetDarknessOpacity(190);
```

Suggested values:

| Value | Result |
|---:|---|
| `60` | very light haze |
| `80` | light haze |
| `120` | dim scene |
| `160` | moody darkness |
| `190` | dungeon darkness |
| `220` | very dark |
| `240` | nearly black |

Start around `180` to `200` for a dungeon.

### Reveal radius

Controls how far the player or light can see.

```csharp
playerVision.SetRadius(180f);
torch.SetRadius(120f);
```

Suggested values:

| Radius | Result |
|---:|---|
| `60f` | small candle / item glow |
| `100f` | small torch |
| `150f` | player-held torch |
| `220f` | strong lantern / magic sight |
| `300f+` | large beacon / scripted reveal |

### Inner clear radius

Controls how much of the reveal circle is fully clear before falloff begins.

```csharp
darkness.SetInnerClearRadiusRatio(0.20f);
```

Suggested values:

| Value | Result |
|---:|---|
| `0.00f` | fade starts immediately |
| `0.15f` | small clear center |
| `0.25f` | comfortable player vision |
| `0.40f` | large clear center |
| `0.65f` | mostly clear with soft edge |

### Midpoint radius

Controls where the middle falloff stop occurs.

```csharp
darkness.SetMidpointRadiusRatio(0.62f);
```

Higher values make the reveal stay stronger farther from the center.

Suggested values:

| Value | Result |
|---:|---|
| `0.40f` | aggressive falloff |
| `0.60f` | balanced falloff |
| `0.75f` | wide, soft falloff |
| `0.90f` | reveal stays strong almost to edge |

### Midpoint strength

Controls how strong the reveal is at the midpoint.

```csharp
darkness.SetMidpointStrength(0.45f);
```

Higher values create a softer, wider reveal. Lower values make the edge fade more aggressively.

Suggested values:

| Value | Result |
|---:|---|
| `0.20f` | sharp/dim falloff |
| `0.45f` | balanced torch falloff |
| `0.70f` | soft broad reveal |
| `1.00f` | very strong until edge |

### Light intensity

Controls how strong the visible glow is.

```csharp
torch.Intensity = 0.85f;
```

Suggested values:

| Value | Result |
|---:|---|
| `0.25f` | faint glow |
| `0.50f` | mild glow |
| `0.85f` | strong torch |
| `1.00f` | maximum glow |

### Flicker

Flicker should usually be subtle.

```csharp
torch.FlickerEnabled = true;
torch.FlickerAmount = 0.08f;
torch.FlickerRefreshHz = 12;
```

Suggested values:

| Property | Suggested range |
|---|---:|
| `FlickerAmount` | `0.04f` to `0.12f` |
| `FlickerRefreshHz` | `8` to `15` |

Avoid very high flicker values unless the effect is intentionally unstable, magical, or hazardous.

### Light radius versus reveal radius

When tracking a light, you can scale the reveal independently:

```csharp
darkness.TrackLight(
    torch,
    radiusScale: 1.25f,
    intensityScale: 1.0f);
```

This is useful because visible glow radius and gameplay visibility radius are not always the same thing.

Common choices:

| Setup | Meaning |
|---|---|
| `radiusScale: 1.0f` | reveal matches glow radius |
| `radiusScale: 1.15f` | reveal slightly larger than glow |
| `radiusScale: 0.80f` | glow spills beyond clear vision |
| `intensityScale: 0.75f` | reveal remains partially dark |

### Disable intensity tracking

If a flickering light makes the darkness reveal pulse too much, disable intensity tracking:

```csharp
darkness.TrackLight(
    torch,
    radiusScale: 1.0f,
    intensityScale: 1.0f,
    trackIntensity: false);
```

The reveal will still follow position and radius, but it will not pulse with the torch's flicker.

### SceneLayer darkness bounds

`DirectSceneLayerDarknessOverlay` is bounded by `DarknessWorldBounds` / `WorldBounds`.

```csharp
var layerFog = new DirectSceneLayerDarknessOverlay(
    renderSurfaceHost,
    dungeonLayer,
    new Rectangle(256, 128, 512, 384));
```

A larger rectangle covers more of the world and costs more to redraw.

A smaller rectangle is better for localized room darkness, smoke clouds, and fog banks.

Suggested starting sizes:

| Use case | Bounds guidance |
|---|---|
| One room | room world rectangle |
| Smoke cloud | tight rectangle around smoke |
| Poison gas | hazard area plus soft edge padding |
| Whole layer haze | full visible gameplay area or full map region |
| Background mist | broad but low-opacity region |

If the effect is really meant to cover the whole screen regardless of camera position, prefer `DirectDarknessOverlay`.

---

## Dirty rectangles and performance

`DirectRadialLight` is bounded in world space.

That means its dirty area is the light's world bounds:

```text
center +/- radius
```

This works well with Gondwana's dirty-rectangle renderer because the light only affects a finite region.

When a light moves, the old and new bounds need to be redrawn. The helper handles that through its direct-drawing bounds.

`DirectDarknessOverlay` is different. It is view-sized.

When the View darkness overlay or one of its reveal sources changes, it generally refreshes the full target viewport. This is intentional. The overlay covers the whole view, so the simplest correct behavior is to redraw the whole view overlay.

`DirectSceneLayerDarknessOverlay` is in between those two. It is not full-view, but it can still be large.

Its dirty area is its `WorldBounds`:

```text
DirectSceneLayerDarknessOverlay.WorldBounds
```

When a manual reveal source changes, or when a tracked light moves, the overlay refreshes its bounded world region. That is much better than refreshing the whole View when the fog/darkness is local to a room or cloud, but it is still more expensive than a small `DirectRadialLight` if the bounds are large.

Performance guidance:

- A few torch lights are fine.
- Flicker at `8` to `15` Hz usually looks good.
- Avoid making every light flicker at full frame rate.
- A full-view darkness overlay is more expensive than a small bounded light.
- A large SceneLayer darkness overlay is cheaper than full-view darkness only if its bounds are meaningfully smaller than the View or affected map area.
- Use one View darkness overlay per View unless you have a specific reason to do otherwise.
- Use SceneLayer darkness for bounded world fog, smoke, or room darkness.
- GPU-backed rendering is the natural long-term home for large animated overlays.
- Bitmap rendering can still handle this, but full-view and large-world effects should be used thoughtfully.

Practical starting point for player-view darkness:

```text
1 darkness overlay per View
1 player-held torch
0 to 10 static torches
flicker at 8-12 Hz
moderate radii
```

Practical starting point for SceneLayer fog/darkness:

```text
1 bounded SceneLayer overlay per fog/dark region
bounds no larger than necessary
manual reveal sources for static clear spots
tracked lights only when the fog should open around lights
```

---

## Layering and Z-order

These helpers are drawn through the DirectDrawing system, so `ZOrder` matters.

For View darkness, suggested ordering is:

```text
tiles and sprites
lower-Z direct drawings
DirectRadialLight glow
DirectDarknessOverlay
HUD/widgets/debug overlays
```

Typical values:

```csharp
torch.ZOrder = 10_000;
darkness.ZOrder = 20_000;
```

For SceneLayer darkness, the ordering is local to the same `SceneLayer`:

```text
tiles and sprites on this SceneLayer
DirectRadialLight glow on this SceneLayer
DirectSceneLayerDarknessOverlay on this SceneLayer
later SceneLayers / View overlays / HUD
```

Typical values:

```csharp
torch.ZOrder = 10_000;
layerFog.ZOrder = 20_000;
```

If the darkness appears above everything and hides the glow, either:

- the glow needs to be drawn after the darkness, or
- the reveal hole needs to expose the glow beneath, depending on the look you want.

For the common torch-in-darkness look, the practical intent is:

```text
darkness overlay creates visibility
light glow provides warm color inside/around that visibility
```

If the result looks wrong, first check `ZOrder`.

Also check whether you used the right darkness type:

```text
DirectDarknessOverlay
  View-level; good for player visibility

DirectSceneLayerDarknessOverlay
  SceneLayer-level; good for actual fog/darkness in the world
```

---

## Cleanup and scene changes

Direct drawings should be disposed when they are no longer needed.

For a standalone light:

```csharp
torch.Dispose();
```

For a temporary pickup glow:

```csharp
pickupGlow.Dispose();
```

For a View darkness overlay:

```csharp
darkness.Dispose();
```

For a SceneLayer darkness overlay:

```csharp
layerFog.Dispose();
```

For a light owner, use whatever cleanup helper is provided by `DirectLightLayer`, or dispose/remove the lights it owns.

Good times to clean up:

- leaving a level
- changing scenes
- removing a torch object
- collecting a glowing pickup
- ending a spell effect
- closing a cutscene overlay
- removing a fog bank, smoke cloud, or poison haze

If a light remains visible after it should be gone, make sure the corresponding `DirectRadialLight` was disposed or removed from its owner.

If a reveal hole remains visible after a light is gone, make sure the darkness overlay is no longer tracking that light, or remove the manual reveal source.

If SceneLayer fog remains visible after leaving a room or area, make sure the corresponding `DirectSceneLayerDarknessOverlay` was disposed or had its `DarknessWorldBounds` changed.

---

## Troubleshooting

### I added a torch but the scene is not dark

`DirectRadialLight` only draws the glow.

Add `DirectDarknessOverlay` if you want the rest of the scene darkened.

```csharp
var darkness = new DirectDarknessOverlay(renderSurfaceHost, mainView, dungeonLayer)
    .SetDarknessOpacity(190);

darkness.TrackLight(torch);
```

### I added darkness but cannot see anything

Add at least one reveal source:

```csharp
darkness.AddRevealSource(playerWorldCenterPx, 180f);
```

or track a light:

```csharp
darkness.TrackLight(torch);
```

Also check that the reveal source is using the correct world position and that the overlay's projection layer is the layer where that world position makes sense.

### The torch glow is visible but the reveal does not move

Make sure you are moving the tracked `DirectRadialLight`, not just a separate game object.

```csharp
torch.MoveTo(playerWorldCenterPx);
```

If you are using manual reveal sources, move the reveal source directly.

```csharp
playerVision.MoveTo(playerWorldCenterPx);
```

### The reveal is offset from the torch

Check the `projectionLayer` used by `DirectDarknessOverlay`.

```csharp
var darkness = new DirectDarknessOverlay(
    renderSurfaceHost,
    mainView,
    dungeonLayer,
    "dungeon-darkness");
```

The `dungeonLayer` should usually be the same layer that the light's world coordinates are based on.

### The effect is too subtle

Try increasing the light intensity:

```csharp
torch.Intensity = 1.0f;
```

Try increasing darkness opacity:

```csharp
darkness.SetDarknessOpacity(210);
```

Try increasing the reveal radius:

```csharp
torch.SetRadius(160f);
```

### The effect is too harsh

Try lowering darkness opacity:

```csharp
darkness.SetDarknessOpacity(150);
```

Try increasing midpoint strength:

```csharp
darkness.SetMidpointStrength(0.70f);
```

Try increasing midpoint radius:

```csharp
darkness.SetMidpointRadiusRatio(0.75f);
```

### Flicker looks annoying

Reduce the amount:

```csharp
torch.FlickerAmount = 0.04f;
```

Lower the refresh rate:

```csharp
torch.FlickerRefreshHz = 8;
```

Or disable reveal intensity tracking:

```csharp
darkness.TrackLight(
    torch,
    radiusScale: 1.0f,
    intensityScale: 1.0f,
    trackIntensity: false);
```

### I expected actual fog, but split-screen players see different darkness

You probably used `DirectDarknessOverlay`.

That overlay is View-specific and is usually correct for player vision or fog-of-war.

For actual fog that exists in the world and should be the same for every View, use `DirectSceneLayerDarknessOverlay` instead:

```csharp
var layerFog = new DirectSceneLayerDarknessOverlay(
    renderSurfaceHost,
    dungeonLayer,
    new Rectangle(0, 0, 1024, 768));
```

### SceneLayer darkness will not track my light

`DirectSceneLayerDarknessOverlay` only tracks lights on the same `SceneLayer`.

This is intentional.

```csharp
// Works only when torch.SceneLayer == layerFog.SceneLayer.
layerFog.TrackLight(torch);
```

If you need cross-layer spill later, that should be a larger lighting-system feature, not an accidental behavior in this helper.

### SceneLayer fog/darkness covers too much of the world

Shrink its world bounds:

```csharp
layerFog.DarknessWorldBounds = new Rectangle(256, 128, 512, 384);
```

For room darkness, use the room's world rectangle.

For a smoke cloud, use a rectangle tightly around the cloud plus a little padding for soft edges.

### The effect is too expensive

Try:

- reducing darkness usage to one overlay per view
- lowering `FlickerRefreshHz`
- disabling reveal intensity tracking for flickering lights
- reducing the number of animated lights
- using smaller radii
- using fewer full-view overlays
- testing with GPU-backed rendering

---

## API quick reference

### `DirectRadialLight`

Create directly:

```csharp
var light = new DirectRadialLight(
    Color.FromArgb(180, 255, 190, 80),
    renderSurfaceHost,
    dungeonLayer,
    new PointF(520, 320),
    120f,
    "torch-01");
```

Common members:

```csharp
light.CenterWorldPx
light.RadiusWorldPx
light.LightColor
light.Intensity
light.EffectiveIntensity
light.BlendMode
light.FlickerEnabled
light.FlickerAmount
light.FlickerRefreshHz
light.MoveTo(new PointF(600, 360));
light.SetRadius(160f);
light.Dispose();
```

### `DirectLightLayer`

Create an owner:

```csharp
var lights = new DirectLightLayer(renderSurfaceHost, dungeonLayer);
```

Add torch-style lights:

```csharp
var torch = lights.AddTorchLight(
    new PointF(520, 320),
    120f,
    nickname: "torch-01");
```

Remove a light:

```csharp
lights.Remove(torch);
```

### `DirectDarknessOverlay`

Create an overlay:

```csharp
var darkness = new DirectDarknessOverlay(
    renderSurfaceHost,
    mainView,
    dungeonLayer,
    "dungeon-darkness");
```

Tune it:

```csharp
darkness
    .SetDarknessColor(Color.Black)
    .SetDarknessOpacity(190)
    .SetInnerClearRadiusRatio(0.20f)
    .SetMidpointRadiusRatio(0.62f)
    .SetMidpointStrength(0.45f);
```

Add manual reveal:

```csharp
var vision = darkness.AddRevealSource(playerWorldCenterPx, 180f, "player-vision");
vision.MoveTo(playerWorldCenterPx);
vision.SetRadius(220f);
vision.Intensity = 0.75f;
```

Track one light:

```csharp
darkness.TrackLight(torch);
```

Track one light with tuning:

```csharp
darkness.TrackLight(
    torch,
    radiusScale: 1.15f,
    intensityScale: 1.0f,
    trackIntensity: false);
```

Track a light owner:

```csharp
darkness.TrackLightLayer(lights);
```

### `DirectSceneLayerDarknessOverlay`

Create a bounded SceneLayer darkness/fog overlay:

```csharp
var layerFog = new DirectSceneLayerDarknessOverlay(
    renderSurfaceHost,
    dungeonLayer,
    new Rectangle(0, 0, 1024, 768),
    "layer-fog");
```

Tune it:

```csharp
layerFog
    .SetDarknessColor(Color.Black)
    .SetDarknessOpacity(180)
    .SetInnerClearRadiusRatio(0.20f)
    .SetMidpointRadiusRatio(0.65f)
    .SetMidpointStrength(0.45f);
```

Move or resize the affected world region:

```csharp
layerFog.DarknessWorldBounds = new Rectangle(256, 128, 512, 384);
```

Add manual reveal:

```csharp
var reveal = layerFog.AddRevealSource(
    new PointF(320, 240),
    120f,
    "room-reveal");

reveal.Intensity = 0.75f;
```

Track one same-layer light:

```csharp
layerFog.TrackLight(torch, radiusScale: 1.15f);
```

Track a same-layer light owner:

```csharp
layerFog.TrackLightLayer(lights, radiusScale: 1.10f);
```

Dispose when done:

```csharp
layerFog.Dispose();
```

---

## Summary

Use `DirectRadialLight` when you want a visible glow.

Use `DirectLightLayer` when you want to manage several glow lights conveniently.

Use `DirectDarknessOverlay` when you want a View darkened with soft visibility holes. This is the right fit for player vision, fog-of-war, split-screen visibility, and per-camera darkness.

Use `DirectSceneLayerDarknessOverlay` when you want bounded fog/darkness that physically belongs to one `SceneLayer`. This is the right fit for actual fog, smoke, poison haze, room darkness, and other world-local atmosphere.

Use `TrackLight` or `TrackLightLayer` when you want the glow and reveal hole to stay synchronized automatically.

The system is deliberately DirectDrawing-based, modular, and renderer-friendly. It gives Gondwana a practical first lighting/darkness workflow without turning the engine into a full lighting engine before it needs to be one. Cross-layer light spill, shadows, wall occlusion, and light bouncing remain intentionally out of scope.
