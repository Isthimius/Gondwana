using System;
using System.Drawing;
using Gondwana.Drawing.Direct.Particles;
using SkiaSharp;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameHost
{
    private static readonly Random _rng = new();
    private ParticleSurface? _particleSurface;

    private ParticleEmitter GetSpots(float width, float height)
    {
        SKColor[] colors =
        {
            SKColors.Red,
            SKColors.Blue,
            SKColors.Yellow,
            SKColors.Green,
            SKColors.Violet
        };

        return new ParticleEmitter
        {
            Position = new PointF(width * 1.1f, height * 0.5f),
            JitterY = height * 0.5f,

            EmitRate = 0.65f,
            LifeRange = (1000f, 2000f),

            VelocityRangeX = (-100f, -50f),
            VelocityRangeY = (-1f, 1f),

            SizeRange = (40f, 80f),

            GravityY = 0f,
            BlendMode = SKBlendMode.SrcOver,

            OnSpawn = (ref Particle p) =>
            {
                var baseColor = colors[_rng.Next(colors.Length)];
                p.Color = baseColor.WithAlpha(255);
            }
        };
    }

    private void DisposeParticleSurface()
    {
        _particleSurface?.Dispose();
        _particleSurface = null;
    }

    private void AddClouds()
    {
        if (SpotGame.Players.Length == 0)
            return;

        DisposeParticleSurface();
        _particleSurface = new ParticleSurface(
            SurfaceHost,
            SpotGame.BackgroundGameField,
            new Rectangle(0, 0, 769, 769),
            "cloudSurface",
            4);

        _particleSurface.CullingMarginX = 1300f;
        _particleSurface.ZOrder = 50;
        _particleSurface.Emitters.Add(GetClouds(769, 769));
    }

    private ParticleEmitter GetClouds(float width, float height)
    {
        return new ParticleEmitter
        {
            Position = new PointF(width * 1.4f, height * 0.5f),
            JitterY = height * 0.5f,

            EmitRate = 0.075f,
            LifeRange = (2000f, 2000f),

            VelocityRangeX = (-50f, -25f),
            VelocityRangeY = (-1f, 1f),

            SizeRange = (200f, 500f),

            GravityY = 0f,
            BlendMode = SKBlendMode.SrcOver,

            ParticleSprite = _clouds.SkBitmap,

            OnSpawn = (ref Particle p) =>
            {
                p.AngularVel = 0;
                p.Rotation = 0;

                byte alpha = (byte)Random.Shared.Next(100, 180);
                p.Tint = new SKColor(255, 255, 255, alpha);
            }
        };
    }
}
