---
title: "feat: HealthComponent and DamageSource entity lifecycle system"
---
## Summary
FlatRedBall ships a standardized damage and health system. Gondwana has no such concept. This issue tracks adding optional, composable `HealthComponent` and `DamageSource` types that plug into collision callbacks.

## Scope of Work

### `Gondwana.HealthComponent`
```csharp
public class HealthComponent
{
    public float MaxHealth { get; set; }
    public float CurrentHealth { get; private set; }
    public bool IsAlive { get; private set; }
    public bool IsInvincible { get; set; }
    public TimeSpan InvincibilityWindow { get; set; }

    public void TakeDamage(float amount, DamageSource source);
    public void Heal(float amount);
    public void Kill();

    public event EventHandler<DamageEventArgs> Damaged;
    public event EventHandler<HealEventArgs> Healed;
    public event EventHandler Died;
}
```

### `Gondwana.DamageSource`
```csharp
public record DamageSource(float Amount, DamageType Type, object? Owner = null);
public enum DamageType { Physical, Environmental, Poison, Fire }
```

### Collision Convenience Extension
Add `result.ApplyDamage(source)` on `CollisionResult` — looks up a `HealthComponent` on the colliding entity and calls `TakeDamage`.

## Design Goals
- Opt-in and additive — no existing types change signatures
- Zero allocations in the hot path (no LINQ, no boxing in `TakeDamage`)
- `InvincibilityWindow` uses the engine's existing `HighResTimer`
- `HealthComponent` is not tied to `Sprite`; attach via composition

## Acceptance Criteria
- [ ] `TakeDamage` / `Heal` correctly adjusts `CurrentHealth` with min/max clamping
- [ ] `Died` event fires exactly once when health reaches zero
- [ ] Invincibility window blocks damage for its configured duration
- [ ] Works in the existing Spot demo (enemy damages player on collision)

## Key Files / References
- `Gondwana/Collisions/CollisionResult.cs`
- `Gondwana/Timers/HighResTimer.cs`
- FlatRedBall damage system: https://docs.flatredball.com/flatredball/tutorials/damage-dealing
