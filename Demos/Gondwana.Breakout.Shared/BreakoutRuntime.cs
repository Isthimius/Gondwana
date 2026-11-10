using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Gondwana.Timers;
using SkiaSharp;
using SpriteHorizontalAlignment = Gondwana.Drawing.Sprites.HorizontalAlignment;
using SpriteVerticalAlignment = Gondwana.Drawing.Sprites.VerticalAlignment;

namespace Gondwana.Demos.Breakout;

/// <summary>One game/scene/input implementation reused by all three GPU platform hosts.</summary>
internal sealed class BreakoutRuntime
{
    private readonly RenderSurfaceHostBase _surface;
    private readonly BreakoutGame _game = new();
    private readonly HashSet<string> _pressed = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Sprite> _bricks = [];
    private Tilesheet _tilesheet = null!;
    private SceneLayer _layer = null!;
    private Sprite _paddle = null!;
    private Sprite _ball = null!;
    private TextBlock _hud = null!;
    private TextBlock _message = null!;
    private KeyboardEventPoller? _keyboard;
    private long _lastTick;
    private string _lastHud = "";
    private string _lastMessage = "";

    private static Engine Engine => Engine.Instance;

    internal BreakoutRuntime(RenderSurfaceHostBase surface) => _surface = surface;

    internal void LoadTilesheets()
    {
        _tilesheet = Engine.Managers.Tilesheets.LoadFromBitmap("breakout-procedural", BreakoutArt.CreateBitmap());
        _tilesheet.DefaultRegion.TileSize = new Size(BreakoutArt.FrameSize, BreakoutArt.FrameSize);
    }

    internal Scene CreateScene()
    {
        var scene = new Scene();
        _layer = scene.AddLayer(BreakoutGame.Width / BreakoutGame.TileSize,
            BreakoutGame.Height / BreakoutGame.TileSize,
            BreakoutGame.TileSize, BreakoutGame.TileSize, 10, 1f, CoordinateSystemTypes.Orthogonal);
        return scene;
    }

    internal void OnSceneBound(Scene scene)
    {
        _surface.Backbuffer.ClearColor = new SKColor(9, 16, 33);
        var camera = _surface.ViewManager.Views[0].Camera;
        camera.WorldBoundsPx = scene.GetWorldBoundsPx();
        camera.SnapTo(PointF.Empty);
    }

    internal void CreateSprites()
    {
        _paddle = CreateSprite("breakout-paddle", BreakoutArt.Paddle,
            new Size((int)BreakoutGame.PaddleWidth, (int)BreakoutGame.PaddleHeight),
            new Vector2(_game.PaddleX, BreakoutGame.PaddleY));
        _paddle.ZOrder = 40;

        _ball = CreateSprite("breakout-ball", BreakoutArt.Ball, new Size(26, 26), _game.Ball);
        _ball.ZOrder = 50;

        int i = 0;
        foreach (var brick in _game.Bricks)
        {
            var sprite = CreateSprite($"breakout-brick-{i++}", BreakoutArt.FirstBrick + brick.Row,
                new Size((int)brick.Width, (int)brick.Height), brick.Center);
            _bricks.Add(sprite);
        }
    }

    internal void CreateDirectDrawings()
    {
        var view = _surface.ViewManager.Views[0];
        DrawRail("left", new Rectangle(35, 116, 6, 964));
        DrawRail("right", new Rectangle(1879, 116, 6, 964));
        DrawRail("top", new Rectangle(35, 115, 1850, 6));

        _hud = new TextBlock(_surface, view, new Rectangle(72, 20, 1776, 75), "breakout-hud")
            .SetFont(SKTypeface.Default, 27f)
            .SetColors(SKColors.White, new SKColor(13, 26, 49, 215))
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false).UseShadow();
        _hud.ZOrder = 1000;

        _message = new TextBlock(_surface, view, new Rectangle(485, 555, 950, 140), "breakout-message")
            .SetFont(SKTypeface.Default, 35f, 25f)
            .SetColors(SKColors.White, new SKColor(16, 32, 60, 230))
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .SetMaxLines(3).UseShadow();
        _message.ZOrder = 1001;

        DrawState();
    }

    private void DrawRail(string name, Rectangle bounds)
    {
        new DirectRectangle(Color.FromArgb(140, 96, 185, 223), _surface,
            _surface.ViewManager.Views[0], bounds, $"breakout-{name}")
            .SetFilled(true).ZOrder = 2;
    }

    private Sprite CreateSprite(string name, int frame, Size size, Vector2 pixelCenter)
    {
        var sprite = Engine.Managers.Sprites.CreateSprite(_layer, _tilesheet[frame, 0], name);
        sprite.RenderSize = size;
        sprite.HorizAlign = SpriteHorizontalAlignment.Center;
        sprite.VertAlign = SpriteVerticalAlignment.Middle;
        sprite.SetPosition(ToGrid(pixelCenter));
        sprite.Visible = true;
        sprite.ZOrder = 20;
        return sprite;
    }

    private static Vector2 ToGrid(Vector2 px) => px / BreakoutGame.TileSize;

    internal void HookKeyboard(KeyboardEventPoller keyboard, params (int Code, string Name)[] bindings)
    {
        _keyboard = keyboard;
        _keyboard.KeyDown += HandleKey;
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
        if (args.KeyAction != KeyAction.Pressed) return;
        _pressed.Add(key);
        switch (key)
        {
            case "space":
                _game.Launch();
                DrawState();
                break;
            case "r":
                _game.Reset();
                _pressed.Clear();
                _lastTick = HighResTimer.GetCurrentTick();
                DrawState();
                break;
            case "p":
                _game.TogglePause();
                DrawState();
                break;
        }
    }

    internal void Start()
    {
        _lastTick = HighResTimer.GetCurrentTick();
        Engine.BeforeBackgroundTasksExecute += Update;
    }

    private void Update()
    {
        long tick = HighResTimer.GetCurrentTick();
        float dt = Math.Clamp(HighResTimer.GetDuration(_lastTick, tick), 0f, 0.05f);
        _lastTick = tick;
        float axis = (_pressed.Contains("right") || _pressed.Contains("d") ? 1f : 0f)
            - (_pressed.Contains("left") || _pressed.Contains("a") ? 1f : 0f);
        _game.Update(dt, axis);
        DrawState();
    }

    private void DrawState()
    {
        if (_paddle is null || _ball is null || _hud is null) return;

        _paddle.SetPosition(ToGrid(new Vector2(_game.PaddleX, BreakoutGame.PaddleY)));
        _ball.SetPosition(ToGrid(_game.Ball));
        _ball.Visible = _game.Phase != BreakoutGame.GamePhase.GameOver;
        for (int i = 0; i < _bricks.Count; i++)
            _bricks[i].Visible = _game.Bricks[i].Alive;

        string hud = $"BREAKOUT   SCORE {_game.Score:000000}   LIVES {_game.Lives}     MOVE  A/D or \u2190/\u2192     SPACE  LAUNCH     P  PAUSE     R  RESTART";
        if (hud != _lastHud)
        {
            _hud.SetText(hud);
            _lastHud = hud;
        }

        string message = _game.Phase switch
        {
            BreakoutGame.GamePhase.Ready => "SPACE TO LAUNCH\nClear all the bricks!",
            BreakoutGame.GamePhase.Paused => "PAUSED\nPress P to resume",
            BreakoutGame.GamePhase.Victory => "BOARD CLEARED!\nPress R to play again",
            BreakoutGame.GamePhase.GameOver => "GAME OVER\nPress R to try again",
            _ => ""
        };
        if (message != _lastMessage)
        {
            _message.SetText(message);
            _message.Visible = message.Length != 0;
            _lastMessage = message;
        }
    }

    internal void UnhookEvents()
    {
        Engine.BeforeBackgroundTasksExecute -= Update;
        if (_keyboard is not null)
            _keyboard.KeyDown -= HandleKey;
        _keyboard = null;
        _pressed.Clear();
    }
}
