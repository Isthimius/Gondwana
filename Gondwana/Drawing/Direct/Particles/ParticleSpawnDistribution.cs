namespace Gondwana.Drawing.Direct.Particles;

public enum ParticleSpawnDistribution
{
    Rectangle = 0, // default behavior
    Ellipse = 1,   // uniform disk/ellipse
    Ring = 2,      // uniform annulus
    Gaussian = 3   // center-weighted, clamped to ellipse bounds
}
