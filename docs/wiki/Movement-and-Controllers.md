Gondwana’s movement system centers on `MovementController`, which manages how a movable object changes position over time.

It supports three movement styles:
- Follow
- Scripted
- Integrated

---

### Follow
Follow mode keeps an object tracking a target, either hard or smoothly. This is useful for camera-like motion, companion motion, or “move toward a live point of interest” behavior.

### Scripted
Scripted movement is for explicit motion plans:
- tweening
- move-toward behaviors
- time-based motion sequences

This is the right fit for cutscenes, UI-like motion, and authored transitions.

### Integrated
Integrated movement is the more physics-like mode. Velocity and acceleration are advanced over time, making it suitable for ordinary gameplay motion.

---

## Priority model
Each frame, the controller resolves movement in this order:
1. follow
2. scripted
3. integrated

That means authored (scripted) or tracking (follow) motion can temporarily “own” the frame before free integration runs.

---

## Why this is nice
The system gives you one controller abstraction instead of making you choose between completely separate movement subsystems.

---

## Where to read next
- `Gondwana/Movement/MovementController.cs`
- `Gondwana/Movement/MovementController.Follow.cs`
- `Gondwana/Movement/MovementController.Scripted.cs`
- `Gondwana/Movement/MovementController.Integrated.cs`
