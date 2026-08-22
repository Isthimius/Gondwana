using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Scenes;
using Gondwana.Timers;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;
using SpriteHorizontalAlignment = Gondwana.Drawing.Sprites.HorizontalAlignment;
using SpriteVerticalAlignment = Gondwana.Drawing.Sprites.VerticalAlignment;

namespace Gondwana.Demos.Pong;

internal sealed class PongGameHost : WinFormsGameHost
{
    private const int WorldColumns = 15;
    private const int WorldRows = 10;
    private const int TileSize = 64;
    private const float PaddleHalfWidth = 12f / TileSize;
    private const float PaddleHalfHeight = 64f / TileSize;
    private const float BallRadius = 14f / TileSize;
    private const float PaddleSpeed = 7.25f;
    private const float AiSpeed = 5.7f;
    private const float InitialBallSpeed = 6.3f;
    private const float MaximumBallSpeed = 11f;
    private const int WinningScore = 7;

    private static readonly Keys[] MonitoredKeys =
        [Keys.W, Keys.S, Keys.Up, Keys.Down, Keys.Space, Keys.R, Keys.Tab];
    private readonly HashSet<Keys> _keysDown = [];
    private Tilesheet _tilesheet = null!;
    private SceneLayer _actorLayer = null!;
    private Sprite _leftPaddle = null!;
    private Sprite _rightPaddle = null!;
    private Sprite _ball = null!;
    private TextBlock _scoreText = null!;
    private TextBlock _statusText = null!;
    private Vector2 _ballVelocity;
    private long _lastUpdateTick;
    private int _leftScore;
    private int _rightScore;
    private int _serveDirection = 1;
    private bool _rightPaddleAi = true;
    private GameState _state = GameState.Ready;

    internal PongGameHost(WinFormBitmapRenderSurfaceControl renderSurface) : base(renderSurface) { }

    protected override void LoadTilesheets()
    {
        _tilesheet = Engine.Managers.Tilesheets.LoadFromBitmap("gondwana-pong-art", PongArt.CreateBitmap());
        _tilesheet.DefaultRegion.TileSize = new Size(PongArt.FrameSize, PongArt.FrameSize);
    }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();
        _actorLayer = scene.AddLayer(WorldColumns, WorldRows, TileSize, TileSize, 10, 1f,
            CoordinateSystemTypes.Orthogonal);
        return scene;
    }

    protected override void OnSceneBound()
    {
        RenderSurface.Host.Backbuffer.ClearColor = new SKColor(4, 10, 22);
        var view = RenderSurface.Host.ViewManager.Views[0];
        view.Camera.WorldBoundsPx = Scene!.GetWorldBoundsPx();
        view.Camera.SnapTo(PointF.Empty);
    }

    protected override void CreateSprites()
    {
        _leftPaddle = CreateSprite("left-paddle", PongArt.PaddleFrame, new Size(24, 128),
            new Vector2(0.65f, WorldRows / 2f));
        _rightPaddle = CreateSprite("right-paddle", PongArt.PaddleFrame, new Size(24, 128),
            new Vector2(WorldColumns - 0.65f, WorldRows / 2f));
        _ball = CreateSprite("ball", PongArt.BallFrame, new Size(28, 28),
            new Vector2(WorldColumns / 2f, WorldRows / 2f));
        _ball.ZOrder = 30;
    }

    protected override void CreateDirectDrawings()
    {
        var view = RenderSurface.Host.ViewManager.Views[0];
        for (int y = 16; y < WorldRows * TileSize; y += 40)
        {
            var dash = new DirectRectangle(Color.FromArgb(90, 180, 225, 240), RenderSurface.Host, view,
                new Rectangle(WorldColumns * TileSize / 2 - 2, y, 4, 22), $"center-line-{y}").SetFilled(true);
            dash.ZOrder = 1;
        }
        _scoreText = new TextBlock(RenderSurface.Host, view, new Rectangle(330, 20, 300, 92), "pong-score")
            .SetFont(SKTypeface.Default, 56f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false)
            .UseShadow();
        _scoreText.ZOrder = 1000;
        _statusText = new TextBlock(RenderSurface.Host, view, new Rectangle(210, 505, 540, 100), "pong-status")
            .SetFont(SKTypeface.Default, 20f, minSize: 16f)
            .SetColors(new SKColor(208, 240, 250), new SKColor(5, 15, 31, 215))
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .SetMaxLines(3)
            .UseShadow();
        _statusText.HorizontalPadding = 18f;
        _statusText.VerticalPadding = 10f;
        _statusText.ZOrder = 1001;
        RefreshHud();
    }

    protected override void OnKeyboardAdapterInitialized()
    {
        var keyboard = Engine.Input.KeyboardEventPoller!;
        keyboard.KeyDown += OnKeyDown;
        foreach (Keys key in MonitoredKeys)
            keyboard.StartMonitoringKey((int)key, key.ToString());
    }

    protected override void OnEngineInitialized()
    {
        Engine.Configuration.TargetFPS = 60;
        _lastUpdateTick = HighResTimer.GetCurrentTick();
        Engine.BeforeBackgroundTasksExecute += BeforeBackgroundTasksExecute;
    }

    protected override void UnhookEvents()
    {
        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;
        Engine.BeforeBackgroundTasksExecute -= BeforeBackgroundTasksExecute;
    }

    private Sprite CreateSprite(string nickname, int frame, Size renderSize, Vector2 position)
    {
        Sprite sprite = Engine.Managers.Sprites.CreateSprite(_actorLayer, _tilesheet[frame, 0], nickname);
        sprite.RenderSize = renderSize;
        sprite.HorizAlign = SpriteHorizontalAlignment.Center;
        sprite.VertAlign = SpriteVerticalAlignment.Middle;
        sprite.SetPosition(position);
        sprite.Visible = true;
        sprite.ZOrder = 20;
        return sprite;
    }

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (!Enum.TryParse(args.KeyConfig.Key, ignoreCase: true, out Keys key)) return;
        if (args.KeyAction == KeyAction.Released)
        {
            _keysDown.Remove(key);
            return;
        }
        _keysDown.Add(key);
        if (key == Keys.Space && _state == GameState.Ready) Serve();
        else if (key == Keys.R) ResetMatch();
        else if (key == Keys.Tab)
        {
            _rightPaddleAi = !_rightPaddleAi;
            _keysDown.Remove(Keys.Up);
            _keysDown.Remove(Keys.Down);
            RefreshHud();
        }
    }

    private void BeforeBackgroundTasksExecute()
    {
        long tick = HighResTimer.GetCurrentTick();
        float dt = Math.Clamp(HighResTimer.GetDuration(_lastUpdateTick, tick), 0f, 0.05f);
        _lastUpdateTick = tick;
        if (dt <= 0f) return;
        MovePaddles(dt);
        if (_state == GameState.Playing) MoveBall(dt);
    }

    private void MovePaddles(float dt)
    {
        MovePaddle(_leftPaddle, GetDirection(Keys.W, Keys.S) * PaddleSpeed * dt);
        float rightDirection;
        if (_rightPaddleAi)
        {
            float delta = _ball.GetPosition().Y - _rightPaddle.GetPosition().Y;
            rightDirection = MathF.Abs(delta) < 0.12f ? 0f : MathF.Sign(delta);
        }
        else rightDirection = GetDirection(Keys.Up, Keys.Down);
        MovePaddle(_rightPaddle, rightDirection * (_rightPaddleAi ? AiSpeed : PaddleSpeed) * dt);
    }

    private float GetDirection(Keys negative, Keys positive)
    {
        bool moveNegative = _keysDown.Contains(negative);
        bool movePositive = _keysDown.Contains(positive);
        return moveNegative == movePositive ? 0f : moveNegative ? -1f : 1f;
    }

    private static void MovePaddle(Sprite paddle, float deltaY)
    {
        Vector2 position = paddle.GetPosition();
        position.Y = Math.Clamp(position.Y + deltaY, PaddleHalfHeight, WorldRows - PaddleHalfHeight);
        paddle.SetPosition(position);
    }

    private void MoveBall(float dt)
    {
        Vector2 previous = _ball.GetPosition();
        Vector2 next = previous + _ballVelocity * dt;
        if (next.Y - BallRadius <= 0f && _ballVelocity.Y < 0f)
        {
            next.Y = BallRadius;
            _ballVelocity.Y *= -1f;
        }
        else if (next.Y + BallRadius >= WorldRows && _ballVelocity.Y > 0f)
        {
            next.Y = WorldRows - BallRadius;
            _ballVelocity.Y *= -1f;
        }
        BounceFromPaddle(_leftPaddle, previous, ref next, true);
        BounceFromPaddle(_rightPaddle, previous, ref next, false);
        _ball.SetPosition(next);
        if (next.X + BallRadius < 0f) AwardPoint(false);
        else if (next.X - BallRadius > WorldColumns) AwardPoint(true);
    }

    private void BounceFromPaddle(Sprite paddle, Vector2 previous, ref Vector2 next, bool isLeft)
    {
        Vector2 paddlePosition = paddle.GetPosition();
        float paddleFace = paddlePosition.X + (isLeft ? PaddleHalfWidth : -PaddleHalfWidth);
        bool crossedFace = isLeft
            ? previous.X - BallRadius >= paddleFace && next.X - BallRadius <= paddleFace
            : previous.X + BallRadius <= paddleFace && next.X + BallRadius >= paddleFace;
        if (!crossedFace || isLeft != (_ballVelocity.X < 0f)) return;
        float offset = (next.Y - paddlePosition.Y) / PaddleHalfHeight;
        if (MathF.Abs(offset) > 1f + BallRadius) return;
        offset = Math.Clamp(offset, -1f, 1f);
        float speed = Math.Min(_ballVelocity.Length() * 1.055f, MaximumBallSpeed);
        float horizontal = MathF.Cos(offset * 0.95f) * speed;
        float vertical = MathF.Sin(offset * 0.95f) * speed;
        _ballVelocity = new Vector2(isLeft ? horizontal : -horizontal, vertical);
        next.X = paddleFace + (isLeft ? BallRadius : -BallRadius);
        _ball.JiggleOnce(2f, 2f, 18f, 0.12f, true, 0.12f);
    }

    private void AwardPoint(bool leftPlayerScored)
    {
        if (leftPlayerScored) _leftScore++; else _rightScore++;
        if (_leftScore >= WinningScore || _rightScore >= WinningScore)
        {
            _state = GameState.MatchOver;
            CenterBall();
            RefreshHud();
            return;
        }
        _serveDirection = leftPlayerScored ? 1 : -1;
        _state = GameState.Ready;
        CenterBall();
        RefreshHud();
    }

    private void Serve()
    {
        float vertical = (_leftScore + _rightScore) % 2 == 0 ? -2.35f : 2.35f;
        _ballVelocity = new Vector2(_serveDirection * InitialBallSpeed, vertical);
        _state = GameState.Playing;
        RefreshHud();
    }

    private void ResetMatch()
    {
        _leftScore = 0;
        _rightScore = 0;
        _serveDirection = 1;
        _state = GameState.Ready;
        _keysDown.Clear();
        _leftPaddle.SetPosition(new Vector2(0.65f, WorldRows / 2f));
        _rightPaddle.SetPosition(new Vector2(WorldColumns - 0.65f, WorldRows / 2f));
        CenterBall();
        _lastUpdateTick = HighResTimer.GetCurrentTick();
        RefreshHud();
    }

    private void CenterBall()
    {
        _ballVelocity = Vector2.Zero;
        _ball.SetPosition(new Vector2(WorldColumns / 2f, WorldRows / 2f));
    }

    private void RefreshHud()
    {
        _scoreText?.SetText($"{_leftScore}  :  {_rightScore}");
        if (_statusText is null) return;
        string opponent = _rightPaddleAi ? "CPU" : "PLAYER 2";
        string prompt = _state switch
        {
            GameState.Ready => "SPACE to serve",
            GameState.MatchOver => $"{(_leftScore > _rightScore ? "PLAYER 1" : opponent)} WINS — R to restart",
            _ => $"First to {WinningScore}"
        };
        _statusText.SetText($"{prompt}\nW/S: Player 1   ↑/↓: Player 2   TAB: {opponent}   R: restart");
    }

    private enum GameState { Ready, Playing, MatchOver }
}
