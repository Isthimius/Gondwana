using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Direct.Particles;
using Gondwana.Scenes;
using Gondwana.Drawing.Coordinates;
using SkiaSharp;
using System.Text;
using static Gondwana.Drawing.Direct.TextBlock;
using Gondwana.Movement.Easing;
using System.Numerics;

namespace Gondwana.ParticleTest;

public partial class Form1 : Form
{
    private ParticleSurface? _particleSurface;
    private TextBlock? _textBlock;

    public Form1()
    {
        InitializeComponent();

        this.Shown += Form1_Shown;
    }

    private void Form1_Shown(object? sender, EventArgs e)
    {
        InitializeEngine();
        Engine.Instance.Configuration.TargetFPS = 90;
    }

    private void InitializeEngine()
    {
        var renderSurface = winFormBitmapRenderSurfaceControl1.RenderSurfaceHost;
        var adapter = renderSurface.RenderSurfaceAdapter;

        var sceneLayer = new SceneLayer(1, 1, adapter!.Width, adapter.Height);
        sceneLayer.CoordinateSystem = new SquareIsoCoordinates();
        var scene = new Scene(sceneLayer);

        renderSurface.Bind(scene);

        Engine.Instance.CPSCalculated += (cps) =>
        {
            var sb = new StringBuilder()
                .Append("Oh no!!! The wizard doth spray purple slime!")
                .AppendLine($" There are {_particleSurface?.ActiveParticleCount ?? 0} active particles!!!")
                .AppendLine(cps.ToString());

            _textBlock?.SetText(sb.ToString()).StartWordReveal(5);
        };

        Engine.Instance.Start();
        Engine.Instance.Configuration.TargetFPS = 60;

        _particleSurface = new ParticleSurface(renderSurface, new Rectangle(0, 0, adapter.Width, adapter.Height), 10000);
        _particleSurface.Emitters.Add(GetSparks(adapter.Width, adapter.Height));
        _particleSurface.Emitters.Add(GetColorfulSparks(adapter.Width, adapter.Height));
        _particleSurface.Emitters.Add(GetRain(adapter.Width));
        _particleSurface.Emitters.Add(GetSnow(adapter.Width));
        _particleSurface.Emitters.Add(GetSmoke(adapter.Width, adapter.Height));

        //_particleSurface.FadeOut(15f);
        //_particleSurface.FadeToCompleted += (s, e) => _particleSurface.Dispose();

        var glowBox = new DirectRectangle(renderSurface, new Rectangle(20, adapter.Height * 7 / 10, adapter.Width - 40, 160), Color.Blue)
            .SetAlpha(128)
            .SetCornerRadius(6f)
            .SetBorderColor(Color.White)
            .SetFilled(true)
            .SetStrokeWidth(6f)
            .SetStrokeAlign(DirectRectangle.StrokeAlign.Outside)
            .PulseBorder(Color.Lime, Color.Red, 2.0f)
            .SetBlendMode(SKBlendMode.Screen);
            //.PulseFill(Color.Blue, Color.Purple, 1.25f);

        glowBox.ZOrder = 1;

        _textBlock = new TextBlock(renderSurface, new Rectangle(20, adapter.Height * 7 / 10, adapter.Width - 40, 160))
            .SetFont(SKTypeface.FromFamilyName("Papyrus"), 14f, minSize: 14f)
            .SetColors(Color.White, Color.Transparent)
            .SetAlignment(SKTextAlign.Center, VerticalAlign.Center)
            .EnableWrapping()
            .SetMaxLines(6)
            //.PulseColor(Color.Red, Color.White, 1.75f)
            .UseShadow()
            .SetShadow(6, 6, 200, 3.0f)
            .UseOutline()
            .StartTypewriter(5);

        _textBlock.ZOrder = 10;

        var composite = new DirectComposite(renderSurface);
        composite.Add(glowBox)
                 .Add(_textBlock);
        //.FadeOut(10f);
        //glowBox.Movement.MoveTo(new Vector2(glowBox.Bounds.Left, glowBox.Bounds.Top - 300), 10f, EasingFunctions.EaseInOutQuad, 1f);

        //composite.Movement.MoveTo(new Vector2(composite.Bounds.Left, composite.Bounds.Top - 300), 10f, EasingFunctions.EaseInOutQuad, 1f);
        composite.Movement.MoveBy(new Vector2(0, -500), 10f, EasingFunctions.EaseInOutQuad);
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

    private ParticleEmitter GetColorfulSparks(float width, float height)
    {
        var rng = new Random();
        var sparks = new ParticleEmitter
        {
            Position = new PointF(width / 2, height),
            EmitRate = 400,
            LifeRange = (0.5f, 5.0f),
            VelocityRangeX = (-150f, 150f),
            VelocityRangeY = (-800f, -600f),
            SizeRange = (0.1f, 3f),
            Color = SKColors.White,
            //GravityY = 100f,

            OnSpawn = (ref Particle p) =>
            {
                // pick a vivid random hue around the violet–blue–cyan range
                float hue = (float)(rng.NextDouble() * 60f + 220f); // 220–280 range
                float sat = (float)(rng.NextDouble() * 0.3f + 0.7f); // 0.7–1.0
                float val = (float)(rng.NextDouble() * 0.4f + 0.6f); // 0.6–1.0

                // convert HSV → RGB
                p.Color = HsvToColor(hue, sat, val);
            }
        };

        return sparks;
    }

    // helper for hue variation
    private static SKColor HsvToColor(float h, float s, float v)
    {
        h %= 360f;
        float c = v * s;
        float x = c * (1 - Math.Abs((h / 60f) % 2 - 1));
        float m = v - c;

        float r = 0, g = 0, b = 0;
        if (h < 60) (r, g, b) = (c, x, 0);
        else if (h < 120) (r, g, b) = (x, c, 0);
        else if (h < 180) (r, g, b) = (0, c, x);
        else if (h < 240) (r, g, b) = (0, x, c);
        else if (h < 300) (r, g, b) = (x, 0, c);
        else (r, g, b) = (c, 0, x);

        return new SKColor(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255),
            255);
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
                p.Y = -8f; // just above the top so they fall in

                // optional: tiny horizontal gust
                p.VX += (float)(rng.NextDouble() * 40f - 20f);
            }
        };
        return snow;
    }

    private ParticleEmitter GetSmoke(float width, float height)
    {
        return new ParticleEmitter
        {
            Position = new PointF(width / 2, height),
            EmitRate = 120,
            LifeRange = (2.5f, 4.0f),
            VelocityRangeX = (-40f, 40f),
            VelocityRangeY = (-120f, -60f),
            SizeRange = (8f, 16f),
            Color = new SKColor(80, 80, 80, 200),
            GravityY = -20f // slight upward drift
        };
    }
}
