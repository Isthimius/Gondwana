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
public sealed partial class DirectParticles : DirectDrawingBase
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

    // Emit controls
    public float GravityY { get; set; } = 400f; // px/s^2

    public DirectParticles(RenderSurfaceHostBase host, Rectangle bounds, int maxParticles = 2000, SKBitmap? particleSprite = null)
        : base(host, bounds)
    {
        _particles = ArrayPool<Particle>.Shared.Rent(maxParticles);
        _particleSprite = particleSprite;
        ZOrder = 10; // draw above backgrounds by default
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

            p.VY += GravityY * dt;
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
            p.Rotation += p.AngularVel * dt;
            p.Life -= dt;

            if (p.Life > 0 && Bounds.Contains((int)p.X, (int)p.Y))
                _particles[write++] = p;
        }
        _alive = write;

        _dirty = true;
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
    protected internal override void Render()
    {
        var canvas = RenderSurfaceHost.Backbuffer.Canvas;

        // OPTIONAL: additive blending for glowy effects
        _paint.BlendMode = SKBlendMode.Plus;

        if (_particleSprite is null)
        {
            // Cheap circles
            for (int i = 0; i < _alive; i++)
            {
                ref var p = ref _particles[i];
                float t = 1f - (p.Life / p.MaxLife);                // 0..1
                byte a = (byte)(255 * (1f - t));                   // fade out
                _paint.Color = ApplyGlobalTint(p.Color, a);
                float size = p.Size * (1f + 0.5f * t);              // slight growth
                canvas.DrawCircle(p.X, p.Y, size, _paint);
            }
        }
        else
        {
            // Sprite quads
            var half = 0.5f;
            for (int i = 0; i < _alive; i++)
            {
                ref var p = ref _particles[i];
                float t = 1f - (p.Life / p.MaxLife);
                byte a = (byte)(255 * (1f - t));
                _paint.Color = ApplyGlobalTint(p.Color, a);

                float s = p.Size;
                var dst = new SKRect(p.X - s * half, p.Y - s * half, p.X + s * half, p.Y + s * half);
                canvas.Save();
                canvas.RotateDegrees(p.Rotation, p.X, p.Y);
                canvas.DrawBitmap(_particleSprite, dst, _paint);
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
            p.X = em.Position.X + NextRange(-0.0f, 0.0f); // jitter here if desired
            p.Y = em.Position.Y + NextRange(-0.0f, 0.0f);

            // Velocity
            p.VX = NextRange(em.VelocityRangeX.Min, em.VelocityRangeX.Max);
            p.VY = NextRange(em.VelocityRangeY.Min, em.VelocityRangeY.Max);

            // Life/Size
            p.MaxLife = p.Life = NextRange(em.LifeRange.Min, em.LifeRange.Max);
            p.Size = NextRange(em.SizeRange.Min, em.SizeRange.Max);

            // Color & spin
            p.Color = em.Color;
            p.Rotation = NextRange(0f, 360f);
            p.AngularVel = NextRange(-180f, 180f);

            // User hook for last-mile per-particle tweaks
            em.OnSpawn?.Invoke(ref p);
        }
    }

    private float NextRange(float min, float max) => (float)(_rng.NextDouble() * (max - min) + min);

    // Fast, branch-free tint (multiplies RGB by global tint;
    // alpha = lifeAlpha * globalAlpha). Assumes particle base alpha = 255.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SKColor ApplyGlobalTint(SKColor c, byte lifeAlpha)
    {
        // Cache globals locally (JIT can keep these in regs)
        var gt = GlobalColorTint;

        int r = (c.Red * gt.Red) / 255;
        int g = (c.Green * gt.Green) / 255;
        int b = (c.Blue * gt.Blue) / 255;
        int a = (lifeAlpha * gt.Alpha) / 255;

        return new SKColor((byte)r, (byte)g, (byte)b, (byte)a);
    }
}
