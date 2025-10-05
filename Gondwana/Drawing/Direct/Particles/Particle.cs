using SkiaSharp;

namespace Gondwana.Drawing.Direct.Particles
{
    /// <summary>
    /// Represents a single particle instance in the particle system.
    /// </summary>
    /// <remarks>
    /// A particle is a lightweight struct that holds its position, velocity,
    /// life span, size, color, and rotation. Particles are typically short-lived
    /// objects that are spawned and updated each frame by one or more
    /// <see cref="ParticleEmitter"/>s inside a <see cref="ParticleSurface"/> system.
    /// </remarks>
    public struct Particle
    {
        /// <summary>
        /// The current X and Y pixel coordinates of the particle in world space.
        /// </summary>
        public float X, Y;

        /// <summary>
        /// The velocity vector (VX, VY) applied to the particle each update step.
        /// (px/s)
        /// </summary>
        public float VX, VY;

        /// <summary>
        /// The acceleration vector (AX, AY) applied to the particle each update step.
        /// (px/s²) Optional, can be zero.
        /// </summary>
        public float AX, AY; // Acceleration components (optional)

        /// <summary>
        /// The remaining lifetime of the particle, in seconds.
        /// Decreases each update until it reaches zero or below.
        /// </summary>
        public float Life;

        /// <summary>
        /// The initial lifetime of the particle, in seconds.
        /// Used together with <see cref="Life"/> to calculate normalized age.
        /// </summary>
        public float MaxLife;

        /// <summary>
        /// The current radius or size of the particle when rendered.
        /// </summary>
        public float Size;

        /// <summary>
        /// The base color of the particle.
        /// The alpha channel is often modified as the particle ages.
        /// </summary>
        public SKColor Color;

        /// <summary>
        /// The current rotation angle of the particle, in degrees.
        /// </summary>
        public float Rotation;

        /// <summary>
        /// The angular velocity of the particle, in degrees per second.
        /// </summary>
        public float AngularVel;

        /// <summary>
        /// The sprite to render for this particle (if any).
        /// Null means render as a primitive (e.g., circle).
        /// </summary>
        public SKBitmap? ParticleSprite;

        /// <summary>
        /// Per-particle tint. If <see langword="null"/>, use the system GlobalColorTint at render time.
        /// </summary>
        public SKColor? Tint;

        /// <summary>
        /// Per-particle speed cap (px/s). MaxVelocity <= 0 means no cap.
        /// </summary>
        public float MaxVelocity;
    }

    /// <summary>
    /// Delegate type for customizing a particle immediately after it is spawned.
    /// </summary>
    /// <param name="particle">
    /// A reference to the newly created <see cref="Particle"/>.
    /// This allows final adjustments to its properties before it begins updating.
    /// </param>
    /// <remarks>
    /// Use <see cref="ParticleSpawnHandler"/> to hook into the creation of each particle.
    /// This is commonly used to randomize rotation, tint colors, or apply
    /// special behaviors on a per-particle basis.
    /// </remarks>
    public delegate void ParticleSpawnHandler(ref Particle particle);
}
