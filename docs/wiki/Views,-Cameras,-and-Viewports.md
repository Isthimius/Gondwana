A View is Gondwana’s answer to the question: what part of the world is being shown, and where is it being shown on screen?

A view combines two things:
- a Camera — what world-space region you are looking at
- a Viewport — where that image appears on the render surface

This separation is deliberate. The camera moves through world space. The viewport does not. The viewport only defines a screen-space rectangle and zoom behavior.

---

## Camera
The Camera stores a world-space upper-left position in pixels. It can:
- snap instantly
- center on points or tiles
- smoothly follow a target
- pan over time
- clamp itself to world bounds
- use a dead zone for less twitchy follow behavior

When the camera moves, Gondwana marks the scene for a full refresh. That is expected: changing the camera changes what every visible layer should look like.

---

## Viewport
The Viewport defines:
- target screen rectangle
- zoom level
- optional screen offset
- derived visible world size

Viewport zoom is animated independently from camera movement. This makes “zoom toward cursor” and cinematic pan/zoom behavior possible without mixing concerns.

---

## Why View matters
Because rendering flows through views, multiple cameras are not a bolt-on feature. Split-screen, picture-in-picture, minimaps, and layered overlays are natural outcomes of the model.

---

## Mental model
- Camera = “where am I looking in the world?”
- Viewport = “where does that view appear on screen?”
- View = both, together

---

## Where to read next
- `Gondwana/Rendering/Views/View.cs`
- `Gondwana/Rendering/Views/Camera.cs`
- `Gondwana/Rendering/Views/Viewport.cs`
- `Gondwana/Rendering/Views/ViewManager.cs`
