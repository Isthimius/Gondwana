Gondwana’s collision system is scene-oriented and AABB-based.

Each Scene owns collision groups, and each `SceneLayer` exposes a collider registry and a collision resolver.

---

## Resolution model
The current resolver is intentionally simple and practical:
- broad-phase query against the registry
- overlap detection in world pixel space
- trigger overlaps reported without push-out
- solid-vs-solid collisions resolved by minimum-axis push-out
- blocked velocity components are canceled so motion can slide on the free axis

---

## Why this fits the engine
Gondwana is a 2.5D rendering engine with strong tile/sprite assumptions. AABB collision is a good match for that style of game logic and keeps the runtime behavior understandable.

---

## Important detail
Collision resolution runs after movement in the engine cycle. That means movers advance first, then get corrected if they overlap.

---

## Mental model
Movement produces a proposed position.

Collision resolution decides whether that position is legal.

---

## Where to read next
- [`Gondwana/Physics/Collisions/CollisionResolver.cs`](https://isthimius.github.io/Gondwana/api/latest/CollisionResolver_8cs_source.html)
- [`Gondwana/Physics/Collisions/`](https://github.com/Isthimius/Gondwana/tree/master/Gondwana/Physics/Collisions)
- [`Gondwana/Scenes/SceneLayer.cs`](https://isthimius.github.io/Gondwana/api/latest/SceneLayer_8cs_source.html)

---

## Periodic collision instances

On wrapped layers, `ColliderRegistry.QueryInstances` returns canonical colliders with translated `BoundsWorldPx`. The resolver uses those bounds at seams and corners while events identify canonical owners. `QueryAabb` returns unique canonical identities. See [[SceneLayer Wrapping]] before using canonical bounds in custom seam collision code.
