---
title: "feat: Rendering pipeline extensibility — IRenderPass abstraction"
---
## Summary
This is listed in the README roadmap as "Enhancing rendering pipeline extensibility." FlatRedBall and GameMaker both allow developers to inject custom draw code at defined stages. Currently `ViewRenderer` iterates scene layers in a fixed order with no insertion points for custom draw callbacks.

## Problem
There is no way to:
- Draw a custom parallax background before scene layers
- Insert a lighting pass between two tile layers
- Draw screen-space UI on top of everything without reimplementing the render loop

## Scope of Work

### `IRenderPass` Interface
```csharp
public interface IRenderPass
{
    int Order { get; }   // lower = earlier; ties are stable-sorted by registration order
    void Draw(RenderContext context, SKCanvas canvas);
}
```

### Wiring
- Add `View.RenderPasses` (or `Scene.GlobalRenderPasses`) collection
- Wrap existing draw logic into built-in pass implementations:
  - `SceneLayerRenderPass` (wraps current SceneLayer draw loop)
  - `DirectDrawingRenderPass` (wraps current DirectDrawingManager.Draw call)
- Named insertion points as constants: `RenderOrder.BeforeScene = 0`, `RenderOrder.AfterScene = 1000`, `RenderOrder.Overlay = 2000`

### Custom Pass Registration
```csharp
myView.RenderPasses.Add(new LightingRenderPass { Order = RenderOrder.AfterScene + 1 });
myView.RenderPasses.Add(new HudRenderPass    { Order = RenderOrder.Overlay });
```

## Acceptance Criteria
- [ ] A custom `IRenderPass` added to a `View` draws at the correct stage relative to scene layers
- [ ] Removing a pass at runtime takes effect on the next frame (no stale references)
- [ ] Existing rendering in the Spot demo is pixel-identical before/after this change
- [ ] At least two of the built-in systems (SceneLayer, DirectDrawing) are migrated to use `IRenderPass` internally

## Key Files / References
- `Gondwana/Rendering/Views/`
- `Gondwana/Rendering/RenderContext.cs`
- README roadmap entry: _"Enhancing rendering pipeline extensibility"_
