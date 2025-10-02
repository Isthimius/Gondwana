using SkiaSharp;

namespace Gondwana.Drawing.Direct.Particles;

public struct Particle
{
    public float X, Y;
    public float VX, VY;
    public float Life, MaxLife;
    public float Size;
    public SKColor Color;
    public float Rotation, AngularVel;
}

public delegate void ParticleSpawnHandler(ref Particle particle);
