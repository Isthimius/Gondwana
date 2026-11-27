using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Scenes;
using SkiaSharp;
using SpriteHorizontalAlignment = Gondwana.Drawing.Sprites.HorizontalAlignment;
using SpriteVerticalAlignment = Gondwana.Drawing.Sprites.VerticalAlignment;

namespace Gondwana.Demos.Asteroids;

/// <summary>Shared Gondwana scene, sprites, keyboard actions, and engine update loop.</summary>
internal sealed class AsteroidsRuntime(RenderSurfaceHostBase surface)
{
    private readonly AsteroidsGame _game = new();
    private readonly HashSet<string> _pressed = new(StringComparer.Ordinal);
    private readonly Dictionary<(int Id, int X, int Y), (Sprite Sprite, int Frame)> _sprites = [];
    private readonly HashSet<(int Id, int X, int Y)> _used = [];
    private Tilesheet _tilesheet = null!;
    private SceneLayer _layer = null!;
    private TextBlock _hud = null!;
    private TextBlock _message = null!;
    private KeyboardEventPoller? _keyboard;
    private SKTypeface? _typeface;
    private string _lastHud = "";
    private string _lastMessage = "";

    private static Gondwana.Engine Engine => Gondwana.Engine.Instance;

    internal void LoadTilesheets()
    {
        _tilesheet = Engine.Managers.Tilesheets.LoadFromBitmap("asteroids-procedural", AsteroidsArt.CreateBitmap());
        _tilesheet.DefaultRegion.TileSize = new Size(AsteroidsArt.FrameSize, AsteroidsArt.FrameSize);
    }

    internal Scene CreateScene()
    {
        var scene = new Scene();
        _layer = scene.AddLayer(AsteroidsGame.Width / AsteroidsGame.TileSize, AsteroidsGame.Height / AsteroidsGame.TileSize,
            AsteroidsGame.TileSize, AsteroidsGame.TileSize, 10, 1f, CoordinateSystemTypes.Orthogonal);
        return scene;
    }

    internal void OnSceneBound(Scene scene)
    {
        surface.Backbuffer.ClearColor = SKColors.Black;
        var camera = surface.ViewManager.Views[0].Camera;
        camera.WorldBoundsPx = scene.GetWorldBoundsPx();
        camera.SnapTo(PointF.Empty);
    }

    internal void CreateDirectDrawings()
    {
        // Browser Skia does not reliably expose system fonts. Use the same embedded
        // typeface on every host so all HUD and menu glyphs are available.
        using var fontStream = typeof(AsteroidsRuntime).Assembly.GetManifestResourceStream(
            "Gondwana.Demos.Asteroids.Fonts.ArchitectsDaughter-Regular.ttf")
            ?? throw new InvalidOperationException("Missing embedded Asteroids font.");
        var typeface = SKTypeface.FromStream(fontStream)
            ?? throw new InvalidOperationException("Failed to decode the embedded Asteroids font.");
        _typeface = typeface;
        var view = surface.ViewManager.Views[0];
        _hud = new TextBlock(surface, view, new Rectangle(20, 12, AsteroidsGame.Width - 40, 44), "asteroids-hud")
            .SetFont(typeface, 22f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false);
        _hud.ZOrder = 1000;
        _message = new TextBlock(surface, view, new Rectangle(240, 250, 800, 220), "asteroids-message")
            .SetFont(typeface, 31f, 22f)
            .SetColors(SKColors.White, new SKColor(0, 0, 0, 220))
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .SetMaxLines(5);
        _message.ZOrder = 1001;

        DrawState();
    }

    internal void HookKeyboard(KeyboardEventPoller keyboard, params (int Code, string Name)[] bindings)
    {
        _keyboard = keyboard;
        keyboard.KeyDown += HandleKey;

        foreach (var (code, name) in bindings)
            keyboard.StartMonitoringKey(code, name);
    }

    private void HandleKey(KeyDownEventArgs args)
    {
        string key = args.KeyConfig.Key;

        if (args.KeyAction == KeyAction.Released)
        {
            _pressed.Remove(key);
            return;
        }

        if (args.KeyAction != KeyAction.Pressed || !_pressed.Add(key))
            return;

        switch (key)
        {
            case "space":
                _game.Start();
                break;
            case "p":
                _game.TogglePause();
                break;
            case "r":
                _game.Reset();
                break;
            case "h":
                _game.Hyperspace();
                break;
        }
    }

    internal void Start()
    {
        Engine.SimulationUpdating += Update;
    }

    private void Update(double deltaSeconds)
    {
        float turn = (_pressed.Contains("right") || _pressed.Contains("d") ? 1f : 0f)
            - (_pressed.Contains("left") || _pressed.Contains("a") ? 1f : 0f);

        // Treat excess suspension time as a pause rather than replaying stale input.
        const double maxStepSeconds = 0.05d;
        const int maxSteps = 5;
        double remaining = Math.Min(deltaSeconds, maxStepSeconds * maxSteps);

        for (int i = 0; i < maxSteps && remaining > 0d; i++)
        {
            double step = Math.Min(remaining, maxStepSeconds);
            _game.Update((float)step, turn, _pressed.Contains("up") || _pressed.Contains("w"), _pressed.Contains("space"));
            remaining -= step;
        }

        DrawState();
    }

    private void DrawState()
    {
        _used.Clear();

        if (_game.ShipVisible)
            DrawBody(_game.Ship, _game.Thrusting ? 1 : 0);

        foreach (AsteroidsGame.Body body in _game.Bodies)
        {
            int frame = body.Type switch
            {
                AsteroidsGame.Kind.Rock => 2 + body.Variant,
                AsteroidsGame.Kind.PlayerShot => 5,
                AsteroidsGame.Kind.Saucer => 6,
                AsteroidsGame.Kind.EnemyShot => 7,
                _ => 8
            };
            DrawBody(body, frame);
        }

        foreach (var key in _sprites.Keys.Where(key => !_used.Contains(key)).ToArray())
        {
            Engine.Managers.Sprites.Remove(_sprites[key].Sprite);
            _sprites.Remove(key);
        }

        string hud = $"SCORE {_game.Score:000000}     HIGH {_game.HighScore:000000}     LIVES {_game.Lives}     WAVE {_game.Wave}";

        if (hud != _lastHud)
        {
            _hud.SetText(hud);
            _lastHud = hud;
        }

        string message = _game.State switch
        {
            AsteroidsGame.Phase.Ready => "ASTEROIDS\nSPACE TO START\nA/D or LEFT/RIGHT: ROTATE    W or UP: THRUST\nSPACE: FIRE    H: HYPERSPACE    P: PAUSE",
            AsteroidsGame.Phase.Paused => "PAUSED\nP TO RESUME    R TO RESTART",
            AsteroidsGame.Phase.GameOver => "GAME OVER\nSPACE TO PLAY AGAIN",
            _ => ""
        };

        if (message != _lastMessage)
        {
            _message.SetText(message);
            _message.Visible = message.Length > 0;
            _lastMessage = message;
        }
    }

    private void DrawBody(AsteroidsGame.Body body, int frame)
    {
        // Duplicate only portions crossing a world edge (including corners).
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2 position = body.Position + new Vector2(x * AsteroidsGame.Width, y * AsteroidsGame.Height);
                float radius = body.Radius;

                if (position.X + radius < 0f || position.X - radius > AsteroidsGame.Width ||
                    position.Y + radius < 0f || position.Y - radius > AsteroidsGame.Height)
                    continue;

                var key = (body.Id, x, y);
                _used.Add(key);

                if (!_sprites.TryGetValue(key, out var entry))
                {
                    Sprite sprite = Engine.Managers.Sprites.CreateSprite(_layer, _tilesheet[frame, 0], $"asteroid-{body.Id}-{x}-{y}");
                    int size = (int)MathF.Ceiling(radius * 2f);
                    sprite.RenderSize = new Size(size, size);
                    sprite.HorizAlign = SpriteHorizontalAlignment.Center;
                    sprite.VertAlign = SpriteVerticalAlignment.Middle;
                    sprite.ZOrder = body.Type == AsteroidsGame.Kind.Ship ? 40 : 20;
                    sprite.Visible = true;
                    entry = (sprite, frame);
                    _sprites.Add(key, entry);
                }

                if (entry.Frame != frame)
                {
                    entry.Sprite.CurrentFrame = _tilesheet[frame, 0];
                    _sprites[key] = (entry.Sprite, frame);
                }

                entry.Sprite.SetPosition(ToCenteredSpriteGridPosition(position));
                entry.Sprite.Rotation = body.Angle * (180f / MathF.PI);
            }
        }
    }

    // Center/Middle alignment centers the sprite inside its grid cell, not on the cell anchor.
    internal static Vector2 ToCenteredSpriteGridPosition(Vector2 worldCenter)
        => worldCenter / AsteroidsGame.TileSize - new Vector2(0.5f, 0.5f);

    internal void UnhookEvents()
    {
        Engine.SimulationUpdating -= Update;

        if (_keyboard is not null)
            _keyboard.KeyDown -= HandleKey;

        _keyboard = null;
        _pressed.Clear();

        // GameHostBase stops and waits for rendering before calling this hook.
        _typeface?.Dispose();
        _typeface = null;
    }
}
