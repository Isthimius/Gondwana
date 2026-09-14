Gondwana's particle system is built on top of [[DirectDrawing]].

A `ParticleSurface` represents one engine-managed drawing containing a pool of lightweight `Particle` values. One or more `ParticleEmitter` objects control how particles are created.

Typical uses include:

- sparks
- fire
- smoke
- snow
- rain
- magical effects
- explosions
- impact effects
- ambient dust
- clouds
- embers
- screen-space feedback

The basic relationship is:

```text
ParticleSurface
    |
    +-- ParticleEmitter
    |       |
    |       +-- creates Particle
    |       +-- creates Particle
    |       +-- creates Particle
    |
    +-- ParticleEmitter
            |
            +-- creates Particle
            +-- creates Particle
```

The `ParticleSurface` owns simulation and rendering.

Emitters are configuration objects describing **how new particles enter that simulation**.

---

## The important types

The particle system primarily revolves around:

- `ParticleSurface`
- `ParticleEmitter`
- `Particle`
- `ParticleSpawnDistribution`

---

# ParticleSurface

`ParticleSurface` is the actual DirectDrawing.

It derives from `DirectDrawingMovableBase`, so it participates in the same engine systems as other direct drawings:

- automatic registration
- visibility
- Z-order
- opacity
- fading
- world-space or screen-space rendering
- dirty-region handling
- movement
- disposal

It also owns the particle pool and performs particle simulation every engine update.

A typical screen-space particle surface looks like this:

```csharp
var view = renderSurfaceHost.ViewManager.Views[0];

var bounds = new Rectangle(
    0,
    0,
    renderSurfaceHost.RenderSurfaceAdapter!.Width,
    renderSurfaceHost.RenderSurfaceAdapter.Height);

var particles = new ParticleSurface(
    renderSurfaceHost,
    view,
    bounds,
    "weather-particles",
    maxParticles: 10_000);
```

No explicit registration is required. Like other DirectDrawing types, constructing the `ParticleSurface` automatically registers it with the engine.

---

## World-space vs screen-space particles

A `ParticleSurface` can use either DirectDrawing coordinate mode.

### View mode

```csharp
var particles = new ParticleSurface(
    renderSurfaceHost,
    view,
    screenBounds);
```

Particle positions are interpreted as **screen pixels**.

This is useful for:

- rain covering the display
- snow
- screen effects
- menu effects
- click explosions
- HUD effects

Camera movement does not move the particle system.

### SceneLayer mode

```csharp
var particles = new ParticleSurface(
    renderSurfaceHost,
    sceneLayer,
    worldBounds);
```

Particle positions are interpreted as **world pixels**.

This is useful for:

- campfires
- torches
- smoke stacks
- waterfalls
- explosions in the game world
- environmental effects

The surface then participates in the normal camera, zoom, viewport, and parallax transformations for that layer.

---

# ParticleEmitter

A `ParticleEmitter` describes how particles are created.

A minimal emitter might look like:

```csharp
var sparks = new ParticleEmitter
{
    Position = new PointF(width / 2f, height),
    EmitRate = 400f,
    LifeRange = (0.5f, 2.0f),
    VelocityRangeX = (-150f, 150f),
    VelocityRangeY = (-300f, -200f),
    SizeRange = (1f, 3f),
    Color = SKColors.BlueViolet
};

particles.Emitters.Add(sparks);
```

Once added, that emitter continuously creates particles.

---

## EmitRate

`EmitRate` is measured in **particles per second**:

```csharp
EmitRate = 400f;
```

Emission is not tied to frame rate. Internally, each emitter maintains a fractional accumulator.

Conceptually:

```text
particlesToAdd = EmitRate × deltaTime
```

Whole particles are emitted immediately while the fractional remainder carries into the next update.

This allows rates such as:

```csharp
EmitRate = 2.5f;
```

without requiring an emitter to produce exactly the same integer number of particles every frame.

---

## LifeRange

```csharp
LifeRange = (0.5f, 2.0f);
```

Each particle receives a random lifetime between the two values.

Lifetime is measured in seconds. The particle stores both:

```text
Life
MaxLife
```

`Life` decreases every update. Once it reaches zero, the particle is removed from the active pool.

Remaining life is also used during rendering to fade particles out naturally.

---

## Velocity

Initial velocity is independently randomized along each axis:

```csharp
VelocityRangeX = (-150f, 150f);
VelocityRangeY = (-300f, -200f);
```

Velocity uses pixels per second.

Screen/world Y increases downward, so:

```text
negative Y velocity = upward
positive Y velocity = downward
```

Thus:

```csharp
VelocityRangeY = (-300f, -200f);
```

launches particles upward, while:

```csharp
VelocityRangeY = (500f, 700f);
```

makes particles fall rapidly downward.

---

## Gravity and acceleration

`ParticleSurface` provides default gravity:

```csharp
particles.GravityX = 0f;
particles.GravityY = 400f;
```

These values are acceleration in pixels per second squared.

An individual emitter can override either value:

```csharp
var smoke = new ParticleEmitter
{
    GravityY = -20f
};
```

That value is copied into each new particle's acceleration when it is spawned.

Different emitters on the same `ParticleSurface` can therefore behave differently:

```text
fire       -> GravityY = -60
smoke      -> GravityY = -15
sparks     -> GravityY = -20
rain       -> default downward gravity
```

---

## SizeRange

```csharp
SizeRange = (2f, 5f);
```

A random size is chosen when each particle is spawned.

For primitive particles, Gondwana renders a circle and uses `Size` as its radius. Primitive particles also grow slightly as they age.

For bitmap-backed particles, `Size` controls the dimensions of the rendered sprite.

---

# The Particle struct

`Particle` is intentionally lightweight.

It contains the runtime state of one particle:

```text
X, Y
VX, VY
AX, AY

Life
MaxLife

Size
Color

Rotation
AngularVel

ParticleSprite
Tint
BlendMode
MaxVelocity
```

Particles are structs rather than individual managed objects. That matters because particle systems may create and destroy thousands of particles over short periods of time.

Gondwana avoids allocating a separate heap object for every particle.

---

# Pooling and performance

`ParticleSurface` rents its particle array from:

```csharp
ArrayPool<Particle>.Shared
```

The requested `maxParticles` controls the pool capacity:

```csharp
var particles = new ParticleSurface(
    renderSurfaceHost,
    view,
    bounds,
    "effects",
    maxParticles: 10_000);
```

When the surface is disposed, the array is returned to the pool.

This reduces garbage-collector pressure compared with continuously allocating short-lived particle objects.

---

## Dead particles are compacted in place

Particle removal also avoids allocating replacement collections.

During an update, Gondwana walks the active particle array. Particles that are still alive are copied toward the beginning of the array:

```text
before:

[A][dead][B][dead][C][D]

after:

[A][B][C][D]
```

The active count is then reduced.

This keeps the active particle region contiguous without repeatedly creating lists or deleting items from the middle of a collection.

---

# Maximum particle count

A `ParticleSurface` has a finite pool.

For example:

```csharp
maxParticles: 10_000
```

If every slot is occupied, additional emission stops until existing particles die or are culled.

That makes the maximum particle count both a capacity setting and a performance safety limit.

---

# Culling

Particles are removed when they leave the bounds of the `ParticleSurface`.

A margin can keep particles alive slightly beyond those bounds:

```csharp
particles.CullingMarginX = 64f;
particles.CullingMarginY = 64f;
```

This is useful for effects that originate just outside the visible area.

Culling is checked against:

- `WorldBounds` in `SceneLayer` mode
- `ScreenBounds` in `View` mode

plus the configured margins.

---

# Spawn position and jitter

Every emitter has an origin:

```csharp
Position = new PointF(400, 300);
```

By default, particles spawn exactly at that point.

`JitterX` and `JitterY` spread particle creation around it:

```csharp
JitterX = 20f;
JitterY = 10f;
```

The shape of that region is controlled by `SpawnDistribution`.

---

# Spawn distributions

Gondwana supports four built-in spawn distributions:

```csharp
ParticleSpawnDistribution.Rectangle
ParticleSpawnDistribution.Ellipse
ParticleSpawnDistribution.Ring
ParticleSpawnDistribution.Gaussian
```

## Rectangle

The default:

```csharp
SpawnDistribution = ParticleSpawnDistribution.Rectangle;
```

Particles are distributed uniformly through the rectangular jitter area.

Useful for:

- rain
- snow
- wide smoke sources
- dust
- area effects

## Ellipse

```csharp
SpawnDistribution = ParticleSpawnDistribution.Ellipse;
```

Particles are uniformly distributed through an ellipse whose radii are defined by `JitterX` and `JitterY`.

Useful for:

- explosions
- magical auras
- soft circular effects
- impact bursts

## Ring

```csharp
SpawnDistribution = ParticleSpawnDistribution.Ring;
RingInnerRadius01 = 0.90f;
```

Particles spawn inside an annulus rather than filling its center.

Typical values:

```text
0.0   ~= filled disk
0.5   = broad ring
0.9   = thin ring
0.98  = very thin ring
```

Useful for:

- shockwaves
- spell rings
- teleport effects
- impact halos

## Gaussian

```csharp
SpawnDistribution = ParticleSpawnDistribution.Gaussian;
GaussianStdDev01 = 0.45f;
```

Particles are concentrated around the center rather than uniformly distributed. The resulting distribution is clamped to the emitter's elliptical jitter bounds.

Useful for:

- smoke
- magical puffs
- dust clouds
- soft explosions

---

# OnSpawn

`ParticleEmitter.OnSpawn` provides per-particle customization.

It receives the new particle by `ref`:

```csharp
OnSpawn = (ref Particle p) =>
{
    p.Size *= 0.8f;
    p.VX += 15f;
};
```

It can modify almost anything:

- position
- velocity
- acceleration
- lifetime
- size
- color
- rotation
- angular velocity
- sprite
- tint
- blend mode
- maximum velocity

This is one of the most useful extension points in the particle system.

---

# OnUpdate

Emitters also support `OnUpdate`.

This runs once per update before emission:

```csharp
emitter.OnUpdate = (em, dt) =>
{
    em.Position = new PointF(
        em.Position.X + 40f * dt,
        em.Position.Y);
};
```

An emitter attached to another game object can use this to keep its origin synchronized with that object.

Particles already emitted remain independent; this only changes where subsequent particles originate.

---

# GlobalEmitScale

`ParticleSurface.GlobalEmitScale` acts as a master emission multiplier:

```csharp
particles.GlobalEmitScale = 1f;   // normal
particles.GlobalEmitScale = 0.5f; // half emission
particles.GlobalEmitScale = 2f;   // double emission
particles.GlobalEmitScale = 0f;   // stop new emission
```

Existing particles continue living normally when emission is reduced to zero.

This is useful for shutting down an effect naturally.

---

# Color, alpha, and tinting

Every emitter has a base color:

```csharp
Color = SKColors.OrangeRed;
```

The color's **alpha channel is significant**.

For example:

```csharp
Color = new SKColor(80, 80, 80, 120);
```

creates smoke whose base opacity is lower than:

```csharp
Color = new SKColor(80, 80, 80, 220);
```

A particle may also receive an emitter-specific tint:

```csharp
Tint = new SKColor(180, 220, 255, 200);
```

The entire surface also has:

```csharp
particles.GlobalColorTint
```

which defaults to `SKColors.White`.

During rendering, Gondwana combines:

```text
particle RGB × tint RGB

particle alpha
    × lifetime fade
    × tint alpha
```

Conceptually:

```text
finalAlpha =
    Particle.Color.Alpha
    × lifeAlpha
    × tintAlpha
```

with each component normalized to the 0-255 alpha range.

This allows alpha to be controlled at three useful levels:

- `Particle.Color.Alpha` — base opacity of that particle
- lifetime fade — automatic fade as the particle ages
- emitter/global tint alpha — whole-effect opacity control

Because `OnSpawn` receives the particle by reference, individual particles can also vary their base alpha:

```csharp
OnSpawn = (ref Particle p) =>
{
    p.Color = p.Color.WithAlpha(
        (byte)Random.Shared.Next(120, 256));
};
```

---

# Blend modes

Each emitter can select a Skia blend mode:

```csharp
BlendMode = SKBlendMode.Plus;
```

`Plus` is the default and works especially well for bright effects because overlapping particles accumulate light.

It is a natural choice for:

- sparks
- fire
- magic
- glowing projectiles

For conventional alpha compositing:

```csharp
BlendMode = SKBlendMode.SrcOver;
```

This is often preferable for:

- smoke
- clouds
- snow
- textured debris

---

# Maximum velocity

Emitters can optionally impose a speed cap:

```csharp
MaxVelocity = 400f;
```

After acceleration is applied, Gondwana checks the velocity magnitude. If it exceeds the limit, the velocity vector is normalized and scaled back to the configured maximum.

---

# Primitive vs sprite particles

By default, particles render as Skia circles.

A `ParticleSurface` can instead be given a default bitmap:

```csharp
var particles = new ParticleSurface(
    renderSurfaceHost,
    view,
    bounds,
    "leaves",
    maxParticles: 2000,
    particleSprite: leafBitmap);
```

Or a particular emitter can specify one:

```csharp
var leaves = new ParticleEmitter
{
    ParticleSprite = leafBitmap
};
```

The emitter-specific bitmap takes precedence over the surface default.

Sprite particles also support rotation. Each particle starts with a random rotation and angular velocity unless changed through `OnSpawn`:

```csharp
OnSpawn = (ref Particle p) =>
{
    p.AngularVel = 45f;
};
```

---

# Continuous emission vs Burst

There are two ways to create particles.

## Continuous emission

Add an emitter to the surface:

```csharp
particles.Emitters.Add(emitter);
```

Its `EmitRate` continuously generates particles.

## Burst emission

For explosions and impacts:

```csharp
particles.Burst(emitter, 100);
```

`Burst()` immediately creates the requested number of particles.

An emitter used solely for bursts normally has:

```csharp
EmitRate = 0f;
```

and does not need to be placed in `Emitters`.

---

# Effect recipe: simple sparks

```csharp
var sparks = new ParticleEmitter
{
    Position = new PointF(width / 2f, height),
    EmitRate = 400f,
    LifeRange = (0.5f, 2.0f),
    VelocityRangeX = (-150f, 150f),
    VelocityRangeY = (-300f, -200f),
    SizeRange = (0.1f, 3f),
    Color = SKColors.BlueViolet
};

particles.Emitters.Add(sparks);
```

The important ingredients are:

```text
high emission
short lifetime
wide horizontal velocity
strong negative Y velocity
small particle size
```

---

# Effect recipe: colorful magical sparks

`OnSpawn` can vary particle colors individually:

```csharp
var rng = new Random();

var sparks = new ParticleEmitter
{
    Position = new PointF(width / 2f, height),
    EmitRate = 400f,
    LifeRange = (0.5f, 5f),
    VelocityRangeX = (-150f, 150f),
    VelocityRangeY = (-800f, -600f),
    SizeRange = (0.1f, 3f),
    Color = SKColors.White,

    OnSpawn = (ref Particle p) =>
    {
        float hue = (float)(rng.NextDouble() * 60f + 220f);
        float saturation = (float)(rng.NextDouble() * 0.3f + 0.7f);
        float value = (float)(rng.NextDouble() * 0.4f + 0.6f);

        p.Color = HsvToColor(hue, saturation, value);
    }
};
```

---

# Effect recipe: rain

Rain is a good example of overriding the spawn location yourself:

```csharp
var rng = new Random();

var rain = new ParticleEmitter
{
    Position = new PointF(0f, 0f),
    EmitRate = 800f,
    LifeRange = (1f, 1.5f),
    VelocityRangeX = (-10f, 10f),
    VelocityRangeY = (500f, 700f),
    SizeRange = (1f, 2f),
    Color = new SKColor(120, 160, 255, 180),

    OnSpawn = (ref Particle p) =>
    {
        p.X = (float)(rng.NextDouble() * width);
        p.Y = -4f;
        p.VX += (float)(rng.NextDouble() * 20f - 10f);
    }
};

particles.Emitters.Add(rain);
```

Each particle begins just above the viewport at a random X position.

The semi-transparent base color is preserved and then further reduced by the automatic lifetime fade.

---

# Effect recipe: snow

```csharp
var rng = new Random();

var snow = new ParticleEmitter
{
    Position = new PointF(0f, 0f),
    EmitRate = 200f,
    LifeRange = (5f, 10f),
    VelocityRangeX = (-20f, 20f),
    VelocityRangeY = (50f, 100f),
    SizeRange = (2f, 5f),
    GravityY = 50f,
    Color = SKColors.White,

    OnSpawn = (ref Particle p) =>
    {
        p.X = (float)(rng.NextDouble() * width);
        p.Y = -8f;
        p.VX += (float)(rng.NextDouble() * 40f - 20f);
    }
};

particles.Emitters.Add(snow);
```

Compared with rain:

```text
lower emission rate
much longer lifetime
much slower fall
larger particles
more horizontal drift
```

---

# Effect recipe: smoke

```csharp
var smoke = new ParticleEmitter
{
    Position = new PointF(width / 2f, height),
    EmitRate = 120f,
    LifeRange = (2.5f, 4f),
    VelocityRangeX = (-40f, 40f),
    VelocityRangeY = (-120f, -60f),
    SizeRange = (8f, 16f),
    Color = new SKColor(80, 80, 80, 200),
    GravityY = -20f,
    BlendMode = SKBlendMode.SrcOver
};

particles.Emitters.Add(smoke);
```

The important ingredients are:

- slow upward velocity
- long lifetime
- relatively large particles
- weak upward acceleration
- semi-transparent base color
- normal alpha compositing

Because primitive particles expand slightly over their lives, the built-in rendering behavior already works reasonably well for smoke.

---

# Effect recipe: drifting clouds

Particles do not have to be tiny or short-lived:

```csharp
var clouds = new ParticleEmitter
{
    Position = new PointF(
        width * 1.1f,
        height * 0.5f),

    JitterY = height * 0.5f,
    EmitRate = 2f,
    LifeRange = (100f, 200f),
    VelocityRangeX = (-20f, -10f),
    VelocityRangeY = (-1f, 1f),
    SizeRange = (40f, 80f),
    Color = new SKColor(80, 80, 80, 30),
    GravityY = 0f,
    BlendMode = SKBlendMode.SrcOver
};

particles.Emitters.Add(clouds);
```

In a game, this becomes more convincing when paired with a cloud bitmap:

```csharp
clouds.ParticleSprite = cloudBitmap;
```

For a small number of persistent, individually managed images, also consider `ImageInstanceLayer`.

---

# Effect recipe: campfire

More sophisticated effects are usually created by combining multiple emitters.

A campfire can be composed from:

```text
flame
sparks
embers
smoke
```

all inside the same `ParticleSurface`.

## Flame emitter

```csharp
var rng = new Random();

var fire = new ParticleEmitter
{
    Position = new PointF(width / 2f, height),

    JitterX = 18f,
    JitterY = 6f,

    EmitRate = 90f,

    LifeRange = (0.35f, 0.8f),

    VelocityRangeX = (-20f, 20f),
    VelocityRangeY = (-140f, -70f),

    SizeRange = (6f, 14f),

    GravityY = -60f,

    Color = new SKColor(255, 150, 60, 240),

    OnSpawn = (ref Particle p) =>
    {
        p.VX += (float)(rng.NextDouble() * 30f - 15f);

        switch (rng.Next(4))
        {
            case 0:
                p.Color = new SKColor(255, 90, 20, 240);
                break;

            case 1:
                p.Color = new SKColor(255, 140, 40, 240);
                break;

            case 2:
                p.Color = new SKColor(255, 190, 60, 240);
                break;

            default:
                p.Color = new SKColor(255, 230, 120, 240);
                p.Size *= 0.7f;
                break;
        }

        byte alpha = (byte)(200 + rng.Next(55));
        p.Color = p.Color.WithAlpha(alpha);
    }
};
```

Here the randomized alpha in `OnSpawn` directly affects the rendered opacity of each flame particle.

## Campfire sparks

```csharp
var sparks = new ParticleEmitter
{
    Position = new PointF(width / 2f, height),

    JitterX = 12f,
    JitterY = 6f,

    EmitRate = 12f,

    LifeRange = (0.6f, 1.4f),

    VelocityRangeX = (-35f, 35f),
    VelocityRangeY = (-180f, -120f),

    SizeRange = (2f, 4f),

    Color = new SKColor(255, 210, 120, 255),

    GravityY = -20f
};
```

## Campfire embers

```csharp
var rng = new Random();

var embers = new ParticleEmitter
{
    Position = new PointF(width / 2f, height - 4f),

    JitterX = 14f,
    JitterY = 4f,

    EmitRate = 45f,

    LifeRange = (0.25f, 0.7f),

    VelocityRangeX = (-12f, 12f),
    VelocityRangeY = (-35f, -10f),

    SizeRange = (3f, 7f),

    GravityY = -10f,

    Color = new SKColor(255, 120, 40, 180),

    OnSpawn = (ref Particle p) =>
    {
        switch (rng.Next(5))
        {
            case 0:
                p.Color = new SKColor(
                    255, 80, 20,
                    (byte)(160 + rng.Next(60)));
                break;

            case 1:
            case 2:
                p.Color = new SKColor(
                    255, 110, 30,
                    (byte)(170 + rng.Next(60)));
                break;

            case 3:
                p.Color = new SKColor(
                    255, 150, 50,
                    (byte)(180 + rng.Next(50)));
                break;

            default:
                p.Color = new SKColor(
                    255, 200, 80,
                    (byte)(140 + rng.Next(40)));
                p.Size *= 0.8f;
                break;
        }

        p.VX += (float)(rng.NextDouble() * 10f - 5f);
    }
};
```

The different ember alpha values now participate directly in final rendering.

## Campfire smoke

```csharp
var rng = new Random();

var smoke = new ParticleEmitter
{
    Position = new PointF(width / 2f, height - 50f),

    JitterX = 10f,
    JitterY = 6f,

    EmitRate = 10f,

    LifeRange = (2f, 3.5f),

    VelocityRangeX = (-10f, 10f),
    VelocityRangeY = (-50f, -25f),

    SizeRange = (14f, 30f),

    GravityY = -15f,

    Color = new SKColor(70, 70, 70, 140),

    BlendMode = SKBlendMode.SrcOver,

    OnSpawn = (ref Particle p) =>
    {
        p.VX += (float)(rng.NextDouble() * 20f - 10f);
    }
};
```

Then combine them:

```csharp
particles.Emitters.Add(fire);
particles.Emitters.Add(sparks);
particles.Emitters.Add(embers);
particles.Emitters.Add(smoke);
```

No special "campfire particle system" is required. The effect emerges from layering several simple emitters.

---

# Effect recipe: one-time explosion

For an impact or explosion, use `Burst()` instead of continuous emission:

```csharp
var explosion = new ParticleEmitter
{
    EmitRate = 0f,

    Position = new PointF(500, 300),

    LifeRange = (0.3f, 0.8f),

    VelocityRangeX = (-500f, 500f),
    VelocityRangeY = (-500f, 500f),

    SizeRange = (2f, 6f),

    JitterX = 12f,
    JitterY = 12f,

    SpawnDistribution =
        ParticleSpawnDistribution.Ellipse,

    Color = SKColors.OrangeRed,

    MaxVelocity = 600f
};

particles.Burst(explosion, 150);
```

The emitter is only configuration in this case. It does not need to be added to `particles.Emitters`.

---

# Effect recipe: click or impact burst

A single emitter can be reused repeatedly:

```csharp
var impactEmitter = new ParticleEmitter
{
    EmitRate = 0f,

    LifeRange = (0.35f, 0.7f),

    VelocityRangeX = (-280f, 280f),
    VelocityRangeY = (-280f, 280f),

    SizeRange = (2f, 5f),

    Color = SKColors.OrangeRed,

    MaxVelocity = 400f,

    JitterX = 40f,
    JitterY = 40f,

    SpawnDistribution =
        ParticleSpawnDistribution.Gaussian,

    GaussianStdDev01 = 0.45f
};
```

When something happens:

```csharp
impactEmitter.Position =
    new PointF(screenX, screenY);

particles.Burst(impactEmitter, 80);
```

Useful for:

- mouse clicks
- hits
- bullets
- spell impacts
- UI feedback

---

# Effect recipe: shockwave ring

Change the same burst emitter to:

```csharp
impactEmitter.SpawnDistribution =
    ParticleSpawnDistribution.Ring;

impactEmitter.RingInnerRadius01 = 0.92f;

impactEmitter.JitterX = 60f;
impactEmitter.JitterY = 60f;
```

Then:

```csharp
particles.Burst(impactEmitter, 100);
```

Particles initially appear around a thin ring centered on the emitter.

---

# Moving an emitter

An emitter can follow a moving game object:

```csharp
var smoke = new ParticleEmitter
{
    EmitRate = 50f,

    LifeRange = (1f, 2f),

    VelocityRangeX = (-15f, 15f),
    VelocityRangeY = (-60f, -30f),

    SizeRange = (6f, 12f),

    GravityY = -10f,

    Color = new SKColor(90, 90, 90, 160),

    BlendMode = SKBlendMode.SrcOver
};

smoke.OnUpdate = (emitter, dt) =>
{
    Vector2 p = vehicle.GetPosition();

    emitter.Position =
        new PointF(
            p.X - 20f,
            p.Y + 10f);
};

particles.Emitters.Add(smoke);
```

Existing smoke continues moving independently. Only newly emitted smoke follows the updated emitter position.

This is the normal model for:

- engine exhaust
- torch flames
- character auras
- projectile trails

---

# Controlling the whole effect

Because `ParticleSurface` is itself a DirectDrawing, the entire effect can be controlled using normal DirectDrawing operations:

```csharp
particles.ZOrder = 100;
particles.FadeOut(2f);
particles.Visible = false;
```

You can also stop new emission while allowing current particles to finish naturally:

```csharp
particles.GlobalEmitScale = 0f;
```

The two techniques have different meanings:

```text
GlobalEmitScale = 0
    stop creating particles
    existing particles continue

FadeOut(...)
    entire rendered surface fades
    simulation may continue
```

---

# Update pipeline

Each `ParticleSurface` update follows roughly this sequence:

```text
1. determine elapsed time
2. call each emitter's OnUpdate
3. calculate continuous emission
4. spawn new particles
5. apply acceleration
6. clamp maximum velocity
7. apply velocity
8. update rotation
9. reduce lifetime
10. cull dead/out-of-bounds particles
11. compact survivors
12. mark the drawing for refresh
```

`OnUpdate` happens **before emission**, so moving an emitter there affects particles created during that update.

`OnSpawn` happens after Gondwana initializes the new particle, making it ideal for final customization.

---

# Render pipeline

During rendering, every active particle is projected from the `ParticleSurface` coordinate space into the destination screen rectangle.

For each particle Gondwana then:

```text
selects its blend mode
calculates life-based opacity
combines particle alpha with life and tint alpha
applies RGB tint
maps its position to screen space

draws either:
    a circle
or
    a bitmap sprite
```

Sprite particles also apply their current rotation.

---

# Choosing between ParticleSurface and ImageInstanceLayer

Both can render repeated moving images, but they solve somewhat different problems.

Use `ParticleSurface` when you have:

- many objects
- frequent creation/destruction
- short lifetimes
- randomized motion
- emission
- explosions
- sparks
- weather
- smoke
- fire

Use `ImageInstanceLayer` when you have:

- relatively few objects
- longer-lived instances
- individually meaningful objects
- persistent clouds
- large fog patches
- decorative background objects

There is deliberate overlap. The difference is primarily one of lifecycle and scale.

---

# Practical tuning advice

Particle effects are usually built by adjusting a few families of values together.

### For sharper, energetic effects

Use:

```text
short lifetime
high velocity
small size
high emission
additive blending
```

Typical examples:

- sparks
- magic
- explosions

### For soft atmospheric effects

Use:

```text
long lifetime
low velocity
large size
low emission
partial base alpha
SrcOver blending
```

Typical examples:

- smoke
- clouds
- mist

### For weather

Use:

```text
wide spawn area
long enough lifetime to cross the view
directional velocity
moderate/high particle count
```

Typical examples:

- rain
- snow
- ash

### For complex effects

Prefer multiple simple emitters over one extremely complicated emitter.

A campfire is easier to reason about as:

```text
fire emitter
+ spark emitter
+ ember emitter
+ smoke emitter
```

than as one emitter attempting to produce all four behaviors.

---

# Disposal

When the effect is finished:

```csharp
particles.Dispose();
```

Disposal:

- unregisters the DirectDrawing
- releases its drawing resources
- returns the particle array to `ArrayPool<Particle>`

Long-lived applications should dispose particle surfaces when they are no longer needed.

---

# Mental model

The easiest way to think about Gondwana particles is:

```text
ParticleSurface
    = simulation + renderer + particle pool

ParticleEmitter
    = recipe for creating particles

Particle
    = one lightweight runtime instance

Burst()
    = create particles right now

EmitRate
    = create particles continuously
```

And for larger effects:

```text
one visual effect
    != necessarily one emitter

one visual effect
    = often several emitters sharing one ParticleSurface
```

---

# Where to read next

Core implementation:

- `Gondwana/Drawing/Direct/Particles/ParticleSurface.cs`
- `Gondwana/Drawing/Direct/Particles/ParticleEmitter.cs`
- `Gondwana/Drawing/Direct/Particles/Particle.cs`
- `Gondwana/Drawing/Direct/Particles/ParticleSpawnDistribution.cs`

Working examples:

- `Demos/Gondwana.ParticleTest/Form1.cs`
- `Demos/Gondwana.CoordinateTest/Game.cs`

Related topics:

- [[DirectDrawing]]
- [[Using Views and Cameras]]
- [[Dirty Rectangles]]
- [[Refresh Queues]]
