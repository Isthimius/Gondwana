using Gondwana.Drawing.Direct.Particles;
using Gondwana.Scenes;
using Gondwana.Scenes.Coordinates;
using SkiaSharp;

namespace Gondwana.ParticleTest;

public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();

        this.Shown += Form1_Shown;
    }

    private void Form1_Shown(object? sender, EventArgs e)
    {
        InitializeEngine();
    }

    private void InitializeEngine()
    {
        var renderSurface = winFormBitmapRenderSurfaceControl1.RenderSurfaceHost;
        var adapter = renderSurface.RenderSurfaceAdapter;

        var sceneLayer = new SceneLayer(1, 1, adapter.Width, adapter.Height);
        sceneLayer.CoordinateSystem = new SquareIsoCoordinates();
        var scene = new Scene(sceneLayer);

        renderSurface.Bind(scene);

        Engine.Instance.Start();

        var particles = new ParticleSurface(renderSurface, new Rectangle(0, 0, adapter.Width, adapter.Height));
        particles.Emitters.Add(GetSparks(adapter.Width, adapter.Height));
        particles.Emitters.Add(GetRain(adapter.Width));
        particles.Emitters.Add(GetSnow(adapter.Width));
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
        Engine.Instance.Stop();
    }

    private ParticleEmitter GetSparks(float width, float height)
    {
        var sparks = new ParticleEmitter
        {
            Position = new PointF(width / 2, height),
            EmitRate = 400,
            LifeRange = (0.5f, 2.0f),
            VelocityRangeX = (-150f, 150f),
            VelocityRangeY = (-300f, -200f),
            SizeRange = (0.1f, 3f),
            Color = SKColors.BlueViolet
        };
        return sparks;
    }

    private ParticleEmitter GetRain(float w)
    {
        var rng = new Random();

        var rain = new ParticleEmitter
        {
            // we’ll override X/Y per-particle in OnSpawn
            Position = new PointF(0f, 0f),

            EmitRate = 800f,                 // density
            LifeRange = (1.0f, 1.5f),        // long enough to fall through view
            VelocityRangeX = (-10f, 10f),    // slight horizontal drift
            VelocityRangeY = (500f, 700f),   // falling fast
            SizeRange = (1f, 2f),            // thin drops
            Color = new SKColor(120, 160, 255, 180),

            OnSpawn = (ref Particle p) =>
            {
                // spawn anywhere across the top edge
                p.X = (float)(rng.NextDouble() * w);
                p.Y = -4f; // just above the top so they fall in

                // optional: tiny horizontal gust
                p.VX += (float)(rng.NextDouble() * 20f - 10f);
            }
        };

        return rain;
    }

    private ParticleEmitter GetSnow(float w)
    {
        var rng = new Random();
        var snow = new ParticleEmitter
        {
            // we’ll override X/Y per-particle in OnSpawn
            Position = new PointF(0f, 0f),
            EmitRate = 200f,                 // density
            LifeRange = (5.0f, 10.0f),       // long enough to fall through view
            VelocityRangeX = (-20f, 20f),    // slight horizontal drift
            VelocityRangeY = (50f, 100f),    // falling slowly
            SizeRange = (2f, 5f),            // fluffy flakes
            GravityY = 50f,
            Color = SKColors.White,
            OnSpawn = (ref Particle p) =>
            {
                // spawn anywhere across the top edge
                p.X = (float)(rng.NextDouble() * w);
                p.Y = -1f; // just above the top so they fall in
                // optional: tiny horizontal gust
                p.VX += (float)(rng.NextDouble() * 40f - 20f);
            }
        };
        return snow;
    }
}
