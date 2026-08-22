using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes;
using Gondwana.Timers;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;
using SpriteHorizontalAlignment = Gondwana.Drawing.Sprites.HorizontalAlignment;
using SpriteVerticalAlignment = Gondwana.Drawing.Sprites.VerticalAlignment;

namespace Gondwana.Demos.Flappy;

internal sealed class FlappyGameHost : WinFormsGameHost
{
    private const int WorldColumns = 15;
    private const int WorldRows = 10;
    private const int TileSize = 64;
    private const int GroundRow = 9;
    private const int GroundTopPx = GroundRow * TileSize;

    private const float BirdX = 4.1f;
    private const float BirdStartY = 4.25f;
    private const float Gravity = 17.5f;
    private const float FlapVelocity = -6.2f;
    private const float MaxBirdSpeed = 9.5f;

    private const float PipeSpeed = 2.8f;
    private const float PipeSpacing = 4.9f;
    private const float PipeStartX = 11.5f;
    private const int PipeWidthPx = 94;
    private const int PipeGapPx = 174;
    private const int MinimumPipeHeightPx = 96;

    private static readonly Size BirdRenderSize = new(58, 46);

    private readonly List<PipePair> _pipes = [];

    private Tilesheet _tilesheet = null!;
    private SceneLayer _backgroundLayer = null!;
    private SceneLayer _actorLayer = null!;
    private Sprite _bird = null!;
    private TextBlock _scoreText = null!;
    private TextBlock _messageText = null!;

    private Random _random = new(8675309);
    private GameState _state = GameState.Ready;
    private long _lastUpdateTick;
    private float _frameDelta;
    private int _score;

    internal FlappyGameHost(WinFormBitmapRenderSurfaceControl renderSurface)
        : base(renderSurface)
    {
    }

    protected override void LoadTilesheets()
    {
        _tilesheet = Engine.Managers.Tilesheets.LoadFromBitmap(
            "gondwana-flappy-art",
            FlappyArt.CreateBitmap());

        _tilesheet.DefaultRegion.TileSize = new Size(
            FlappyArt.FrameSize,
            FlappyArt.FrameSize);
    }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();

        _backgroundLayer = scene.AddLayer(
            WorldColumns,
            WorldRows,
            TileSize,
            TileSize,
            0,
            1f,
            CoordinateSystemTypes.Orthogonal);

        _actorLayer = scene.AddLayer(
            WorldColumns,
            WorldRows,
            TileSize,
            TileSize,
            10,
            1f,
            CoordinateSystemTypes.Orthogonal);

        PopulateBackground();
        return scene;
    }

    protected override void OnSceneBound()
    {
        RenderSurface.Host.Backbuffer.ClearColor = new SKColor(105, 197, 226);

        var view = RenderSurface.Host.ViewManager.Views[0];
        view.Camera.WorldBoundsPx = Scene!.GetWorldBoundsPx();
        view.Camera.SnapTo(PointF.Empty);
    }

    protected override void CreateSprites()
    {
        _bird = Engine.Managers.Sprites.CreateSprite(
            _actorLayer,
            _tilesheet[FlappyArt.BirdFrame, 0],
            "flappy-bird");

        _bird.RenderSize = BirdRenderSize;
        _bird.HorizAlign = SpriteHorizontalAlignment.Center;
        _bird.VertAlign = SpriteVerticalAlignment.Middle;
        _bird.SetPosition(new Vector2(BirdX, BirdStartY));
        _bird.Visible = true;
        _bird.ZOrder = 40;
        _bird.AdjustCollisionArea = new CollisionAdjust(
            top: 7,
            bottom: 7,
            left: 7,
            right: 7);

        for (int index = 0; index < 3; index++)
        {
            Sprite top = CreatePipeSprite($"pipe-{index}-top", rotation: 0f);
            Sprite bottom = CreatePipeSprite($"pipe-{index}-bottom", rotation: 180f);
            _pipes.Add(new PipePair(top, bottom));
        }

        ResetWorld();
    }

    protected override void CreateDirectDrawings()
    {
        var view = RenderSurface.Host.ViewManager.Views[0];

        _scoreText = new TextBlock(
                RenderSurface.Host,
                view,
                new Rectangle(330, 24, 300, 90),
                "flappy-score")
            .SetFont(SKTypeface.Default, 44f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false)
            .UseShadow()
            .UseOutline();

        _scoreText.ZOrder = 1000;

        _messageText = new TextBlock(
                RenderSurface.Host,
                view,
                new Rectangle(190, 190, 580, 190),
                "flappy-message")
            .SetFont(SKTypeface.Default, 32f, minSize: 20f)
            .SetColors(SKColors.White, new SKColor(37, 107, 131, 225))
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .SetMaxLines(4)
            .UseShadow()
            .UseOutline();

        _messageText.HorizontalPadding = 24f;
        _messageText.VerticalPadding = 16f;
        _messageText.ZOrder = 1001;

        RefreshHud();
    }

    protected override void OnKeyboardAdapterInitialized()
    {
        var keyboard = Engine.Input.KeyboardEventPoller!;
        keyboard.KeyDown += OnKeyDown;
        keyboard.StartMonitoringKey((int)Keys.Space, Keys.Space.ToString());
        keyboard.StartMonitoringKey((int)Keys.R, Keys.R.ToString());
    }

    protected override void OnEngineInitialized()
    {
        Engine.Configuration.TargetFPS = 60;
        _lastUpdateTick = HighResTimer.GetCurrentTick();
        Engine.BeforeBackgroundTasksExecute += BeforeBackgroundTasksExecute;
        Engine.AfterBackgroundTasksExecute += AfterBackgroundTasksExecute;
    }

    protected override void UnhookEvents()
    {
        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;

        Engine.BeforeBackgroundTasksExecute -= BeforeBackgroundTasksExecute;
        Engine.AfterBackgroundTasksExecute -= AfterBackgroundTasksExecute;
    }

    private void PopulateBackground()
    {
        for (int column = 0; column < WorldColumns; column++)
            _backgroundLayer[column, GroundRow]!.CurrentFrame = _tilesheet[FlappyArt.GroundFrame, 0];

        var cloudPositions = new (int X, int Y)[]
        {
            (1, 1),
            (6, 2),
            (11, 1),
            (13, 4),
            (3, 5),
            (8, 6)
        };

        foreach ((int x, int y) in cloudPositions)
            _backgroundLayer[x, y]!.CurrentFrame = _tilesheet[FlappyArt.CloudFrame, 0];
    }

    private Sprite CreatePipeSprite(string nickname, float rotation)
    {
        Sprite sprite = Engine.Managers.Sprites.CreateSprite(
            _actorLayer,
            _tilesheet[FlappyArt.PipeFrame, 0],
            nickname);

        sprite.HorizAlign = SpriteHorizontalAlignment.Center;
        sprite.VertAlign = SpriteVerticalAlignment.Middle;
        sprite.Rotation = rotation;
        sprite.Visible = true;
        sprite.ZOrder = 25;
        sprite.AdjustCollisionArea = new CollisionAdjust(
            top: 2,
            bottom: 2,
            left: 5,
            right: 5);

        return sprite;
    }

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (args.KeyAction != KeyAction.Pressed ||
            !Enum.TryParse(args.KeyConfig.Key, ignoreCase: true, out Keys key))
        {
            return;
        }

        if (key == Keys.R)
        {
            ResetGame();
            return;
        }

        if (key != Keys.Space)
            return;

        if (_state == GameState.GameOver)
            ResetGame();

        if (_state == GameState.Ready)
            StartGame();

        Flap();
    }

    private void BeforeBackgroundTasksExecute()
    {
        long tick = HighResTimer.GetCurrentTick();
        _frameDelta = Math.Clamp(
            HighResTimer.GetDuration(_lastUpdateTick, tick),
            0f,
            0.05f);
        _lastUpdateTick = tick;

        if (_state != GameState.Playing || _frameDelta <= 0f)
            return;

        UpdatePipes(_frameDelta);
    }

    private void AfterBackgroundTasksExecute()
    {
        if (_state != GameState.Playing)
            return;

        UpdateBirdRotation();
        CheckScore();
        CheckCollisions();
    }

    private void StartGame()
    {
        _state = GameState.Playing;
        _bird.Movement.SetAcceleration(new Vector2(0f, Gravity));
        _bird.Movement.SetMaxSpeed(MaxBirdSpeed);
        _bird.Movement.SetLinearDamping(0f);
        _messageText.Visible = false;
    }

    private void Flap()
    {
        if (_state != GameState.Playing)
            return;

        Vector2 velocity = _bird.Movement.MovementState.Velocity;
        _bird.Movement.SetVelocity(new Vector2(velocity.X, FlapVelocity));
        _bird.Rotation = -22f;
    }

    private void UpdateBirdRotation()
    {
        float velocityY = _bird.Movement.MovementState.Velocity.Y;
        float targetRotation = Math.Clamp(velocityY * 6.5f, -24f, 72f);
        _bird.Rotation += (targetRotation - _bird.Rotation) * 0.18f;
    }

    private void UpdatePipes(float dt)
    {
        foreach (PipePair pipe in _pipes)
        {
            pipe.X -= PipeSpeed * dt;
            PositionPipePair(pipe);
        }

        foreach (PipePair pipe in _pipes.Where(pipe => pipe.X < -1.5f).ToArray())
        {
            float farthestX = _pipes.Max(candidate => candidate.X);
            ConfigurePipePair(pipe, farthestX + PipeSpacing);
        }
    }

    private void CheckScore()
    {
        foreach (PipePair pipe in _pipes)
        {
            if (pipe.Scored || pipe.X > BirdX)
                continue;

            pipe.Scored = true;
            _score++;
            RefreshHud();
        }
    }

    private void CheckCollisions()
    {
        Rectangle birdBounds = _bird.CollisionArea;

        if (birdBounds.Top <= 0 || birdBounds.Bottom >= GroundTopPx)
        {
            EndGame();
            return;
        }

        foreach (PipePair pipe in _pipes)
        {
            if (birdBounds.IntersectsWith(pipe.Top.CollisionArea) ||
                birdBounds.IntersectsWith(pipe.Bottom.CollisionArea))
            {
                EndGame();
                return;
            }
        }
    }

    private void EndGame()
    {
        _state = GameState.GameOver;
        _bird.Movement.StopAllMovement();
        _messageText.SetText($"GAME OVER\nScore: {_score}\nSPACE to try again   •   R to reset");
        _messageText.Visible = true;
    }

    private void ResetGame()
    {
        _random = new Random(8675309);
        _score = 0;
        _state = GameState.Ready;
        _bird.Movement.StopAllMovement();
        _bird.SetPosition(new Vector2(BirdX, BirdStartY));
        _bird.Rotation = 0f;
        ResetWorld();
        _lastUpdateTick = HighResTimer.GetCurrentTick();
        RefreshHud();
    }

    private void ResetWorld()
    {
        for (int index = 0; index < _pipes.Count; index++)
            ConfigurePipePair(_pipes[index], PipeStartX + index * PipeSpacing);
    }

    private void ConfigurePipePair(PipePair pipe, float x)
    {
        int gapHalf = PipeGapPx / 2;
        int minimumCenter = MinimumPipeHeightPx + gapHalf;
        int maximumCenter = GroundTopPx - MinimumPipeHeightPx - gapHalf;
        int gapCenterPx = _random.Next(minimumCenter, maximumCenter + 1);

        pipe.X = x;
        pipe.GapCenterPx = gapCenterPx;
        pipe.Scored = false;
        PositionPipePair(pipe);
    }

    private static void PositionPipePair(PipePair pipe)
    {
        int gapTopPx = pipe.GapCenterPx - PipeGapPx / 2;
        int gapBottomPx = pipe.GapCenterPx + PipeGapPx / 2;
        int topHeightPx = gapTopPx;
        int bottomHeightPx = GroundTopPx - gapBottomPx;

        pipe.Top.RenderSize = new Size(PipeWidthPx, topHeightPx);
        pipe.Bottom.RenderSize = new Size(PipeWidthPx, bottomHeightPx);

        pipe.Top.SetPosition(new Vector2(
            pipe.X,
            topHeightPx / (2f * TileSize)));

        pipe.Bottom.SetPosition(new Vector2(
            pipe.X,
            (gapBottomPx + bottomHeightPx / 2f) / TileSize));
    }

    private void RefreshHud()
    {
        _scoreText?.SetText(_score.ToString());

        if (_messageText is null)
            return;

        if (_state == GameState.Ready)
        {
            _messageText.SetText("GONDWANA FLAPPY\nSPACE to flap\nPass the pipes. Avoid becoming a statistic.");
            _messageText.Visible = true;
        }
    }

    private sealed class PipePair
    {
        internal PipePair(Sprite top, Sprite bottom)
        {
            Top = top;
            Bottom = bottom;
        }

        internal Sprite Top { get; }
        internal Sprite Bottom { get; }
        internal float X { get; set; }
        internal int GapCenterPx { get; set; }
        internal bool Scored { get; set; }
    }

    private enum GameState
    {
        Ready,
        Playing,
        GameOver
    }
}
