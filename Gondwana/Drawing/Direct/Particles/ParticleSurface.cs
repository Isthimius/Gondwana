using Gondwana.Rendering;
using Gondwana.Timers;
using SkiaSharp;
using System.Buffers;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace Gondwana.Drawing.Direct.Particles;

/// <summary>
/// A flexible particle system for effects like smoke, sparks, fire, or snow,
/// implemented as a <see cref="DirectDrawingBase"/>.
/// </summary>
///
/// <remarks>
/// <para>
/// The particle system maintains a pool of particles that are updated and rendered
/// each frame. Particles have a position, velocity, life span, size, and color.
/// The system uses SkiaSharp to draw either simple circles or textured sprites.
/// </para>
/// <para>
/// Emission is controlled through one or more <see cref="ParticleEmitter"/> instances.
/// Each emitter defines its own position, rate, life range, velocity ranges,
/// size range, and base color. This allows you to layer effects (e.g., smoke plus sparks)
/// within a single particle system.
/// </para>
/// <para>
/// Internally, the particle pool is compacted each update to avoid GC churn.
/// Rendering uses Skia’s <see cref="SKBlendMode.Plus"/> for additive blending,
/// making it suitable for “glowy” effects such as fire, sparks, and magical auras.
/// </para>
/// </remarks>
///
/// <example>
/// The following demonstrates how to create a particle system with two emitters:
///
/// <code>
/// // Create a particle system covering the whole viewport
/// var particles = new DirectParticles(renderHost,
///     new Rectangle(0, 0, viewportW, viewportH));
///
/// // Create a sparks emitter
/// var sparks = new ParticleEmitter
/// {
///     Position = new PointF(400, 550),
///     EmitRate = 400,
///     LifeRange = (0.5f, 1.0f),
///     VelocityRangeX = (-150f, 150f),
///     VelocityRangeY = (-300f, -200f),
///     SizeRange = (2f, 4f),
///     Color = SKColors.OrangeRed
/// };
///
/// // Create a smoke emitter
/// var smoke = new ParticleEmitter
/// {
///     Position = new PointF(400, 540),
///     EmitRate = 120,
///     LifeRange = (2.5f, 4.0f),
///     VelocityRangeX = (-40f, 40f),
///     VelocityRangeY = (-120f, -60f),
///     SizeRange = (8f, 16f),
///     Color = new SKColor(80, 80, 80, 200)
/// };
///
/// // Register emitters
/// particles.Emitters.Add(sparks);
/// particles.Emitters.Add(smoke);
///
/// </code>
/// </example>
public sealed partial class ParticleSurface : DirectDrawingBase
{
    private readonly Particle[] _particles;
    private readonly Random _rng = new();
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private int _alive;

    // If you want textured particles, supply a tilesheet frame and draw bitmap quads instead of circles.
    private readonly SKBitmap? _particleSprite;

    /// <summary>
    /// Collection of particle emitters this system updates and renders.
    /// Add multiple emitters (e.g., sparks + smoke) to layer effects.
    /// </summary>
    public readonly List<ParticleEmitter> Emitters = new();

    public int TotalParticles => _particles.Length;

    /// <summary>
    /// Global multiplier applied to all emitter <c>EmitRate</c> values
    /// in this particle system. Acts like a master volume knob for
    /// particle density.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A value of <c>1.0</c> leaves emit rates unchanged.  
    /// Values greater than 1 increase overall particle output,
    /// while values between 0 and 1 reduce it.  
    /// </para>
    /// <para>
    /// Setting this to <c>0</c> effectively pauses emission without
    /// disabling or removing individual emitters.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following demonstrates how to smoothly fade out an effect
    /// by reducing <see cref="GlobalEmitScale"/> over time:
    ///
    /// <code>
    /// // Particle system created and auto-registered by base class
    /// var particles = new DirectParticles(renderHost, viewportBounds);
    ///
    /// // Start at full intensity
    /// particles.GlobalEmitScale = 1.0f;
    ///
    /// // Later, during update (e.g., shutting down effect):
    /// particles.GlobalEmitScale = MathF.Max(0f,
    ///     particles.GlobalEmitScale - 0.5f * deltaTime); // fade out in ~2s
    /// </code>
    /// </example>
    public float GlobalEmitScale { get; set; } = 1f;

    /// <summary>
    /// Global tint color multiplied against every particle’s own color
    /// during rendering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Useful for quickly recoloring or fading an entire particle system
    /// without touching individual emitters.  
    /// </para>
    /// <para>
    /// Defaults to <see cref="SKColors.White"/>, which leaves particles
    /// unchanged. Setting alpha here provides an additional global fade,
    /// stacked with per-particle fading.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following demonstrates how to apply a global tint:
    ///
    /// <code>
    /// var particles = new DirectParticles(renderHost, viewportBounds);
    ///
    /// // Render all particles with a blue tint
    /// particles.GlobalColorTint = new SKColor(128, 160, 255, 255);
    ///
    /// // Fade entire system to transparent over time
    /// particles.GlobalColorTint = particles.GlobalColorTint.WithAlpha(
    ///     (byte)MathF.Max(0, particles.GlobalColorTint.Alpha - 200 * deltaTime));
    /// </code>
    /// </example>
    public SKColor GlobalColorTint { get; set; } = SKColors.White;

    /// <summary>
    /// Gets or sets the horizontal component of gravity, measured in pixels per second squared.
    /// Default is 0 (no horizontal gravity).
    /// </summary>
    public float GravityX { get; set; } = 0f;   // px/s^2

    /// <summary>
    /// Gets or sets the gravitational acceleration along the Y-axis, measured in pixels per second squared.
    /// Default is 400 (downward).
    /// </summary>
    public float GravityY { get; set; } = 400f; // px/s^2

    /// <summary>
    /// Gets or sets the horizontal culling margin, in pixels, beyond the bounds of the viewport to keep elements
    /// active.
    /// </summary>
    public float CullingMarginX { get; set; } = 32f;

    /// <summary>
    /// Gets or sets the vertical culling margin, in pixels, beyond the bounds of the viewport to keep elements active.
    /// </summary>
    public float CullingMarginY { get; set; } = 32f;

    public ParticleSurface(RenderSurfaceHostBase host, Rectangle bounds, int maxParticles = 2000, SKBitmap? particleSprite = null)
        : base(host, bounds)
    {
        _particles = ArrayPool<Particle>.Shared.Rent(maxParticles);
        _particleSprite = particleSprite;
    }

    /// <summary>
    /// Immediately spawns a fixed number of particles from the given emitter.
    /// Useful for explosions, impacts, or click/tap feedback.
    /// </summary>
    /// <param name="emitter">Configured emitter providing ranges and color.</param>
    /// <param name="count">Number of particles to spawn instantly.</param>
    /// <example>
    /// <code>
    /// // Explosion at point P
    /// var boom = new ParticleEmitter {
    ///     Position = P, EmitRate = 0,
    ///     LifeRange = (0.3f, 0.7f),
    ///     VelocityRangeX = (-600f, 600f),
    ///     VelocityRangeY = (-600f, 600f),
    ///     SizeRange = (3f, 6f), Color = SKColors.OrangeRed
    /// };
    /// particles.Burst(boom, 150);
    /// </code>
    /// </example>
    public void Burst(ParticleEmitter emitter, int count) => EmitFrom(emitter, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ArrayPool<Particle>.Shared.Return(_particles, clearArray: true);
            _paint.Dispose();
            _alive = 0;
            _lastTick = null;
        }
        base.Dispose(disposing);
    }

    private long? _lastTick; // null until first update

    /// <summary>
    /// Tick-driven update override. Computes delta internally and advances simulation.
    /// </summary>
    /// <param name="tick">Current tick from <see cref="HighResTimer"/>.</param>
    protected internal override void Update(long tick)
    {
        // Compute dt from ticks (seconds)
        float dt;
        if (_lastTick is { } last)
        {
            long deltaTicks = tick - last;
            if (deltaTicks < 0) deltaTicks = 0; // guard against clock reset
            dt = (float)(deltaTicks / (double)HighResTimer.TicksPerSecond);
        }
        else
        {
            dt = 0f; // first frame
        }
        _lastTick = tick;

        // (A) per-emitter motion
        for (int i = 0; i < Emitters.Count; i++)
            Emitters[i].OnUpdate?.Invoke(Emitters[i], dt);

        // (B) emission
        for (int i = 0; i < Emitters.Count; i++)
        {
            var em = Emitters[i];
            float rate = MathF.Max(0f, em.EmitRate * GlobalEmitScale);
            em._accumulator += rate * dt;

            int toEmit = (int)em._accumulator;
            if (toEmit > 0) { em._accumulator -= toEmit; EmitFrom(em, toEmit); }
        }

        // (C) integrate & compact
        int write = 0;
        for (int i = 0; i < _alive; i++)
        {
            ref var p = ref _particles[i];

            // integrate acceleration (including gravity)
            p.VX += p.AX * dt;
            p.VY += p.AY * dt;

            // clamp max velocity
            if (p.MaxVelocity > 0f)
            {
                // thank you, Pythagoras
                float vx = p.VX, vy = p.VY;
                float velocity2 = (vx * vx) + (vy * vy);
                float max2 = p.MaxVelocity * p.MaxVelocity;

                if (velocity2 > max2)
                {
                    float inv = p.MaxVelocity / MathF.Sqrt(velocity2);
                    p.VX = vx * inv;
                    p.VY = vy * inv;
                }
            }

            // integrate velocity
            p.X += p.VX * dt;
            p.Y += p.VY * dt;

            // to every season...
            p.Rotation += p.AngularVel * dt;
            p.Life -= dt;

            // cull if out of bounds (with margin)
            bool isInView = p.X >= Bounds.Left - CullingMarginX
                         && p.X <= Bounds.Right + CullingMarginX
                         && p.Y >= Bounds.Top - CullingMarginY
                         && p.Y <= Bounds.Bottom + CullingMarginY;

            if (p.Life > 0 && isInView)
                _particles[write++] = p;
        }
        _alive = write;

        ForceRefresh();
    }

    /// <summary>
    /// Renders all live particles to the current backbuffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Draws each particle as either a Skia circle or a textured quad (if a sprite
    /// is provided). Alpha fades with age. Uses additive blending
    /// (<see cref="SkiaSharp.SKBlendMode.Plus"/>) by default for bright/glowy effects.
    /// </para>
    /// <para>
    /// You normally don’t call this directly. Once the system is registered with
    /// <see cref="DirectDrawingManager"/>, the manager invokes <c>Render()</c>
    /// during the host’s render pass.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // One-time setup
    /// directDrawingManager.AddOrReplace(particles);
    ///
    /// // In your render step (host-driven):
    /// renderSurfaceHost.Render();   // manager calls Render() on each drawable
    ///
    /// // If you must force an extra redraw (e.g., after a burst):
    /// particles.Invalidate();       // mark dirty so the manager re-renders
    /// </code>
    /// </example>
    protected internal override void Draw()
    {
        var canvas = RenderSurfaceHost.Backbuffer.Canvas;

        // e.g., default blend; you may switch per-emitter later if you add BlendMode there
        _paint.BlendMode = SKBlendMode.Plus;

        for (int i = 0; i < _alive; i++)
        {
            ref var p = ref _particles[i];

            // life-based fade
            float t = 1f - (p.Life / p.MaxLife);
            byte a = (byte)(255 * (1f - t));

            // choose tint: emitter override if set, otherwise current global
            var tint = p.Tint ?? GlobalColorTint;

            // apply global tint
            _paint.Color = ApplyTint(p.Color, a, tint);

            if (p.ParticleSprite is null)
            {
                // circle primitive
                float size = p.Size * (1f + 0.5f * t);
                canvas.DrawCircle(p.X, p.Y, size, _paint);
            }
            else
            {
                // textured quad
                float s = p.Size;
                var dst = new SKRect(p.X - s * 0.5f, p.Y - s * 0.5f,
                                     p.X + s * 0.5f, p.Y + s * 0.5f);

                canvas.Save();
                canvas.RotateDegrees(p.Rotation, p.X, p.Y);
                canvas.DrawBitmap(p.ParticleSprite, dst, _paint);
                canvas.Restore();
            }
        }
    }

    private void EmitFrom(ParticleEmitter em, int count)
    {
        for (int i = 0; i < count && _alive < _particles.Length; i++)
        {
            ref var p = ref _particles[_alive++];

            // Position
            p.X = em.Position.X + NextRange(-em.JitterX, em.JitterX);
            p.Y = em.Position.Y + NextRange(-em.JitterY, em.JitterY);

            // Velocity
            p.VX = NextRange(em.VelocityRangeX.Min, em.VelocityRangeX.Max);
            p.VY = NextRange(em.VelocityRangeY.Min, em.VelocityRangeY.Max);

            // Acceleration (emitter override or surface default)
            p.AX = em.GravityX ?? this.GravityX;
            p.AY = em.GravityY ?? this.GravityY;

            // Life/Size
            p.MaxLife = p.Life = NextRange(em.LifeRange.Min, em.LifeRange.Max);
            p.Size = NextRange(em.SizeRange.Min, em.SizeRange.Max);

            // Color & spin
            p.Color = em.Color;
            p.Rotation = NextRange(0f, 360f);
            p.AngularVel = NextRange(-180f, 180f);

            // choose sprite once; no emitter lookups in hot loop
            p.ParticleSprite = em.ParticleSprite ?? this._particleSprite;

            // tint override
            p.Tint = em.Tint;                          // null means "use global at render"

            // max speed clamp
            p.MaxVelocity = em.MaxVelocity ?? 0f;      // 0 = no clamp

            // User hook for last-mile per-particle tweaks
            em.OnSpawn?.Invoke(ref p);
        }
    }

    private float NextRange(float min, float max) => (float)(_rng.NextDouble() * (max - min) + min);

    // Fast, branch-free tint (multiplies RGB by tint;
    // alpha = lifeAlpha * globalAlpha). Assumes particle base alpha = 255.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SKColor ApplyTint(SKColor c, byte lifeAlpha, SKColor tint)
    {
        int r = (c.Red * tint.Red) / 255;
        int g = (c.Green * tint.Green) / 255;
        int b = (c.Blue * tint.Blue) / 255;
        int a = (lifeAlpha * tint.Alpha) / 255;

        return new SKColor((byte)r, (byte)g, (byte)b, (byte)a);
    }
}
