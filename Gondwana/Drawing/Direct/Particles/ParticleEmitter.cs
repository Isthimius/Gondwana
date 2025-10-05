using System.Drawing;
using SkiaSharp;

namespace Gondwana.Drawing.Direct.Particles;

/// <summary>
/// Configures how particles are spawned over time at a given location.
/// </summary>
/// 
/// <remarks>
/// <para>
/// A <see cref="ParticleEmitter"/> defines how particles are spawned over time
/// at a specific position. Each emitter manages its own emission rate, lifetime
/// ranges, velocity ranges, size ranges, and base color.
/// </para>
///
/// <para>
/// You can also attach two optional hooks:
/// <list type="bullet">
///   <item>
///     <description>
///     <see cref="OnSpawn"/> — called once for each newly created particle,
///     giving you a <c>ref</c> to the particle for last-mile initialization
///     (e.g., randomizing rotation, adjusting color, assigning metadata, etc.).
///     </description>
///   </item>
///   <item>
///     <description>
///     <see cref="OnUpdate"/> — called every update tick with the emitter and
///     elapsed time (seconds). This can be used to animate emitter motion
///     (e.g., moving with a torch, oscillating with a sine wave, etc.).
///     </description>
///   </item>
/// </list>
/// </para>
///
/// <para>
/// Emitters are usually created in pairs or groups and added to a
/// <see cref="ParticleSurface"/> system to combine effects. For example,
/// one emitter can generate fast-fading sparks while another generates
/// slow-rising smoke.
/// </para>
/// </remarks>
/// 
/// <example>
/// <para>
/// The following demonstrates how to configure multiple emitters
/// for different particle effects (e.g., fire sparks and campfire smoke).
/// </para>
///
/// <code>
/// // Example: Fire pit with glowing sparks + rising smoke
///
/// var particles = new DirectParticles(renderHost,
///     new Rectangle(0, 0, viewportW, viewportH));
///
/// // Configure base behavior
/// particles.GravityY = 300f;    // pull particles down
///
/// // Define an emitter for sparks
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
/// // Define an emitter for smoke
/// var smoke = new ParticleEmitter
/// {
///     Position = new PointF(400, 540),
///     EmitRate = 120,
///     LifeRange = (2.5f, 4.0f),
///     VelocityRangeX = (-40f, 40f),
///     VelocityRangeY = (-120f, -60f),
///     SizeRange = (8f, 16f),
///     Color = new SKColor(80, 80, 80, 200) // semi-transparent gray
/// };
///
/// // Add emitters to the particle system
/// particles.Emitters.Add(sparks);
/// particles.Emitters.Add(smoke);
///
/// // Register with the manager
/// directDrawingManager.AddOrReplace(particles);
/// </code>
/// </example>
public sealed class ParticleEmitter
{
    /// <summary>RenderHost / adapter pixel position where particles originate.</summary>
    public PointF Position { get; set; }

    /// <summary>Particles per second to spawn (fractional allowed).</summary>
    public float EmitRate { get; set; } = 200f;

    /// <summary>Lifetime range in seconds (min, max).</summary>
    public (float Min, float Max) LifeRange { get; set; } = (0.6f, 1.4f);

    /// <summary>Initial X velocity range in px/s (min, max).</summary>
    public (float Min, float Max) VelocityRangeX { get; set; } = (-120f, 120f);

    /// <summary>Initial Y velocity range in px/s (min, max). Negative shoots upward.</summary>
    public (float Min, float Max) VelocityRangeY { get; set; } = (-300f, -180f);

    /// <summary>Size range in pixels (min, max). Interpreted as diameter for circles, edge for quads.</summary>
    public (float Min, float Max) SizeRange { get; set; } = (3f, 7f);

    /// <summary>
    /// Base color. Alpha will be modulated by lifetime fade. You can randomize in OnSpawn if desired.
    /// </summary>
    public SKColor Color { get; set; } = new SKColor(200, 200, 255, 255);

    /// <summary>
    /// Gets or sets the horizontal jitter value for spawning particles, which represents a random offset applied along the X-axis.
    /// </summary>
    public float JitterX { get; set; } = 0f;

    /// <summary>
    /// Gets or sets the vertical jitter value for spawning particles, which represents a random offset applied along the Y-axis.
    /// </summary>
    public float JitterY { get; set; } = 0f;

    /// <summary>
    /// Gets or sets the horizontal component of the gravity vector in pixels per second squared (px/s²).
    /// Negative values pull left, positive values pull right. If null, the emitter uses the global gravity setting from the <see cref="ParticleSurface">.
    /// </summary>
    public float? GravityX { get; set; } = null;

    /// <summary>
    /// Gets or sets the vertical component of the gravity vector in pixels per second squared (px/s²).
    /// Negative values pull up, positive values pull down. If null, the emitter uses the global gravity setting from the <see cref="ParticleSurface">.
    /// </summary>
    public float? GravityY { get; set; } = null;

    /// <summary>
    /// Optional sprite used for particles spawned by this emitter.
    /// If null, the particle surface’s default sprite is used (or a circle if none).
    /// </summary>
    public SKBitmap? ParticleSprite { get; set; }

    /// <summary>
    /// Optional per-emitter tint. If set, it overrides the particle system's
    /// <c>GlobalColorTint</c> for particles spawned by this emitter.
    /// </summary>
    public SKColor? Tint { get; set; }

    /// <summary>
    /// Optional per-emitter maximum speed in px/s. If set, particle velocity
    /// will be clamped to this magnitude after acceleration is applied each update.
    /// </summary>
    public float? MaxVelocity { get; set; }

    /// <summary>Optional per-particle customization hook (e.g., perlin drift, hue jitter).</summary>
    public ParticleSpawnHandler? OnSpawn { get; set; }

    /// <summary>Optional per-frame callback to tweak emitter (e.g., move with an object).</summary>
    public Action<ParticleEmitter, float>? OnUpdate { get; set; }

    // Internal accumulator to handle fractional emit per frame.
    internal float _accumulator;
}
