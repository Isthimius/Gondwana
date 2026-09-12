using System.Numerics;
using Gondwana.Timers;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Physics.Collisions;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;

namespace Gondwana.Demos.Platformer;

internal sealed class PlatformerGameHost : WinFormsGameHost
{
    private const int WorldColumns = 72;
    private const int WorldRows = 18;
    private const float RunSpeed = 7.5f;
    private const float Gravity = 30f;
    private const float JumpSpeed = 14f;
    private const float MaxFallSpeed = 18f;

    private static readonly Vector2 SpawnPosition = new(2f, 15f);

    private readonly HashSet<Keys> _keysDown = [];
    private readonly List<SceneLayerTile> _hazards = [];
    private readonly List<SceneLayerTile> _relics = [];
    private readonly List<ICollider> _groundProbeResults = [];

    private readonly List<MushroomEnemy> _enemies = [];
    private long _lastEnemyTick;
    private float _spawnElapsed;
    private int _enemyId;
    private Rectangle _previousPlayerArea;

    private Tilesheet _tilesheet = null!;
    private SceneLayer _backgroundLayer = null!;
    private SceneLayer _worldLayer = null!;
    private SceneLayerTile _goal = null!;
    private Sprite _player = null!;
    private TextBlock _hudText = null!;
    private TextBlock _messageText = null!;

    private bool _jumpQueued;
    private bool _grounded;
    private bool _facingLeft;
    private int _relicsCollected;
    private GameState _gameState = GameState.Playing;
    private string _lastHudText = string.Empty;
    private string _statusMessage = string.Empty;
    private DateTime _statusMessageExpiresUtc;

    internal PlatformerGameHost(WinFormBitmapRenderSurfaceControl renderSurface)
        : base(renderSurface)
    {
        ((BitmapBackbuffer)renderSurface.Host.Backbuffer).FilterQuality = SKFilterQuality.None;
    }

    protected override void LoadTilesheets()
    {
        _tilesheet = Engine.Managers.Tilesheets.LoadFromBitmap(
            "platformer",
            PlatformerArt.CreateTilesheetBitmap());

        _tilesheet.DefaultRegion.TileSize = new Size(
            PlatformerArt.TileSize,
            PlatformerArt.TileSize);
    }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();

        _backgroundLayer = scene.AddLayer(
            WorldColumns,
            WorldRows,
            PlatformerArt.TileSize,
            PlatformerArt.TileSize,
            zOrder: 0,
            parallax: 0.35f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);

        _worldLayer = scene.AddLayer(
            WorldColumns,
            WorldRows,
            PlatformerArt.TileSize,
            PlatformerArt.TileSize,
            zOrder: 10,
            parallax: 1f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);

        BuildBackground();
        BuildLevel();

        return scene;
    }

    protected override void OnSceneBound()
    {
        RenderSurface.Host.Backbuffer.ClearColor = new SKColor(110, 190, 235);

        var view = RenderSurface.Host.ViewManager.Views[0];
        view.Camera.WorldBoundsPx = Scene!.GetWorldBoundsPx();
        view.Camera.SnapTo(PointF.Empty);
    }

    protected override void CreateSprites()
    {
        _player = Engine.Managers.Sprites.CreateSprite(
            _worldLayer,
            _tilesheet[PlatformerArt.PlayerRightFrame, 0],
            "player");

        _player.SetPosition(SpawnPosition);
        _player.Visible = true;
        _player.ZOrder = 20;
        _player.AdjustCollisionArea = new CollisionAdjust(
            top: 4,
            bottom: 0,
            left: 7,
            right: 7);

        _player.Collider!.CollisionGroup = Scene!.CollisionGroups.Actors;
        _player.Collider.CollidesWith = Scene.CollisionGroups.WorldStatic;
        _player.Collider.ResponseType = CollisionResponseType.Solid;
        _player.CollisionsEnabled = true;

        _player.Movement.SetAcceleration(new Vector2(0f, Gravity));

        var camera = RenderSurface.Host.ViewManager.Views[0].Camera;
        camera.DeadZonePx = new Rectangle(360, 0, 240, RenderSurface.Height);
        camera.FollowCenteredX(_player, speed: 9f);
        SpawnEnemy();
    }

    protected override void CreateDirectDrawings()
    {
        var view = RenderSurface.Host.ViewManager.Views[0];

        var panel = new DirectRectangle(
                Color.FromArgb(210, 28, 39, 51),
                RenderSurface.Host,
                view,
                new Rectangle(12, 12, 474, 68),
                "hud-panel")
            .SetFilled(true)
            .SetBorderColor(Color.FromArgb(235, 236, 223, 186))
            .SetStrokeWidth(2f)
            .SetCornerRadius(8f);
        panel.ZOrder = 1000;

        _hudText = new TextBlock(
                RenderSurface.Host,
                view,
                new Rectangle(26, 22, 446, 48),
                "hud-text")
            .SetFont(SKTypeface.Default, 17f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false)
            .UseShadow();
        _hudText.ZOrder = 1001;

        _messageText = new TextBlock(
                RenderSurface.Host,
                view,
                new Rectangle(150, 220, 660, 136),
                "status-text")
            .SetFont(SKTypeface.Default, 30f, minSize: 20f)
            .SetColors(SKColors.White, new SKColor(28, 39, 51, 210))
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .SetMaxLines(3)
            .UseShadow()
            .UseOutline();
        _messageText.HorizontalPadding = 18f;
        _messageText.VerticalPadding = 12f;
        _messageText.ZOrder = 1100;
        _messageText.Visible = false;

        UpdateHud(force: true);
        ShowTemporaryMessage("Collect every sun relic, then reach the red flag.", 4d);
    }

    protected override void OnKeyboardAdapterInitialized()
    {
        var keyboard = Engine.Input.KeyboardEventPoller!;
        keyboard.KeyDown += OnKeyDown;

        foreach (var key in MonitoredKeys)
            keyboard.StartMonitoringKey((int)key, key.ToString());
    }

    protected override void OnEngineInitialized()
    {
        _lastEnemyTick = HighResTimer.GetCurrentTick();
        Engine.Configuration.TargetFPS = 60;
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

    private static Keys[] MonitoredKeys =>
    [
        Keys.A,
        Keys.D,
        Keys.Left,
        Keys.Right,
        Keys.W,
        Keys.Up,
        Keys.Space,
        Keys.R
    ];

    private void BuildBackground()
    {
        foreach (var (x, y) in new[]
                 {
                     (3, 3), (11, 5), (21, 2), (32, 4),
                     (43, 2), (54, 5), (63, 3), (70, 1)
                 })
        {
            _backgroundLayer[x, y]!.CurrentFrame = _tilesheet[PlatformerArt.CloudFrame, 0];
        }
    }

    private void BuildLevel()
    {
        var pitColumns = new HashSet<int>
        {
            14, 15, 16,
            36, 37, 38,
            55, 56, 57
        };

        for (var x = 0; x < WorldColumns; x++)
        {
            if (pitColumns.Contains(x))
                continue;

            SetSolidTile(x, 16, PlatformerArt.GrassFrame);
            SetSolidTile(x, 17, PlatformerArt.GrassFrame);
        }

        AddPlatform(5, 9, 13);
        AddPlatform(11, 13, 11);
        AddPlatform(18, 24, 14);
        AddPlatform(26, 31, 12);
        AddPlatform(33, 35, 13);
        AddPlatform(40, 47, 14);
        AddPlatform(49, 54, 12);
        AddPlatform(59, 64, 13);
        AddPlatform(66, 71, 10);

        AddHazard(22, 13);
        AddHazard(29, 11);
        AddHazard(44, 13);
        AddHazard(62, 12);

        AddRelic(7, 12);
        AddRelic(12, 10);
        AddRelic(28, 11);
        AddRelic(51, 11);
        AddRelic(68, 9);

        _goal = _worldLayer[70, 9]!;
        _goal.CurrentFrame = _tilesheet[PlatformerArt.GoalFrame, 0];
    }

    private void AddPlatform(int fromX, int toX, int y)
    {
        for (var x = fromX; x <= toX; x++)
            SetSolidTile(x, y, PlatformerArt.StoneFrame);
    }

    private void SetSolidTile(int x, int y, int frame)
    {
        var tile = _worldLayer[x, y]!;
        tile.CurrentFrame = _tilesheet[frame, 0];
        tile.Collider!.CollisionGroup = _worldLayer.CollisionGroups.WorldStatic;
        tile.Collider.CollidesWith = _worldLayer.CollisionGroups.Actors;
        tile.Collider.ResponseType = CollisionResponseType.Solid;
        tile.CollisionsEnabled = true;
    }

    private void AddHazard(int x, int y)
    {
        var tile = _worldLayer[x, y]!;
        tile.CurrentFrame = _tilesheet[PlatformerArt.SpikeFrame, 0];
        tile.AdjustCollisionArea = new CollisionAdjust(
            top: 14,
            bottom: 1,
            left: 3,
            right: 3);
        _hazards.Add(tile);
    }

    private void AddRelic(int x, int y)
    {
        var tile = _worldLayer[x, y]!;
        tile.CurrentFrame = _tilesheet[PlatformerArt.RelicFrame, 0];
        tile.AdjustCollisionArea = new CollisionAdjust(
            top: 5,
            bottom: 5,
            left: 5,
            right: 5);
        _relics.Add(tile);
    }

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (!Enum.TryParse<Keys>(args.KeyConfig.Key, ignoreCase: true, out var key))
            return;

        switch (args.KeyAction)
        {
            case KeyAction.Pressed:
                _keysDown.Add(key);

                if (key is Keys.Space or Keys.W or Keys.Up)
                    _jumpQueued = true;

                if (key == Keys.R)
                    RestartGame();
                break;

            case KeyAction.Released:
                _keysDown.Remove(key);
                break;
        }
    }

    private void BeforeBackgroundTasksExecute()
    {
        if (_gameState != GameState.Playing)
            return;

        var tick = HighResTimer.GetCurrentTick();
        var elapsed = Math.Max(0f, HighResTimer.GetDuration(_lastEnemyTick, tick));
        _lastEnemyTick = tick;
        UpdateEnemies(elapsed);
        _previousPlayerArea = _player.CollisionArea;

        var velocity = _player.Movement.MovementState.Velocity;
        var moveLeft = _keysDown.Contains(Keys.A) || _keysDown.Contains(Keys.Left);
        var moveRight = _keysDown.Contains(Keys.D) || _keysDown.Contains(Keys.Right);

        var horizontal = moveLeft == moveRight
            ? 0f
            : moveLeft ? -RunSpeed : RunSpeed;

        if (horizontal < 0f && !_facingLeft)
            SetPlayerFacing(left: true);
        else if (horizontal > 0f && _facingLeft)
            SetPlayerFacing(left: false);

        var vertical = Math.Min(velocity.Y, MaxFallSpeed);

        if (_jumpQueued && _grounded)
        {
            vertical = -JumpSpeed;
            _grounded = false;
        }

        _jumpQueued = false;
        _player.Movement.SetVelocity(new Vector2(horizontal, vertical));
        _player.Movement.SetAcceleration(new Vector2(0f, Gravity));
    }

    private void AfterBackgroundTasksExecute()
    {
        if (_gameState != GameState.Playing)
            return;

        _grounded = IsStandingOnSolid();
        CollectRelics();
        if (ResolveEnemyContacts())
            return;

        if (_hazards.Any(hazard =>
                hazard.Visible &&
                _player.CollisionArea.IntersectsWith(hazard.CollisionArea)))
        {
            Respawn("Ouch. Spikes remain undefeated.");
            return;
        }

        if (_player.GetPosition().Y > WorldRows + 2)
        {
            Respawn("Mind the gap.");
            return;
        }

        if (_player.CollisionArea.IntersectsWith(_goal.CollisionArea))
        {
            if (_relicsCollected == _relics.Count)
                WinGame();
            else
                ShowTemporaryMessage(
                    $"The flag is locked: {_relics.Count - _relicsCollected} relic(s) remain.",
                    2d);
        }

        UpdateMessageVisibility();
        UpdateHud();
    }

    private void SpawnEnemy()
    {
        // Choose real ground near the player, keeping clear of pits and the respawn point.
        var playerX = _player.GetPosition().X;
        var column = Enumerable.Range(6, WorldColumns - 7)
            .Where(x => _worldLayer[x, 16]!.CollisionsEnabled && Math.Abs(x - playerX) >= 5f)
            .OrderBy(x => Math.Abs(x - (playerX + 8f)))
            .First();
        var sprite = Engine.Managers.Sprites.CreateSprite(
            _worldLayer, _tilesheet[PlatformerArt.EnemyWalkFrame, 0], $"mushroom-{++_enemyId}");
        sprite.SetPosition(new Vector2(column, 15f));
        sprite.Visible = true;
        sprite.ZOrder = 19;
        sprite.AdjustCollisionArea = new CollisionAdjust(top: 3, bottom: 0, left: 3, right: 3);
        sprite.Collider!.CollisionGroup = Scene!.CollisionGroups.Actors;
        sprite.Collider.CollidesWith = Scene.CollisionGroups.WorldStatic;
        sprite.Collider.ResponseType = CollisionResponseType.Solid;
        sprite.CollisionsEnabled = true;
        sprite.Movement.SetAcceleration(new Vector2(0f, Gravity));
        _enemies.Add(new MushroomEnemy(sprite));
    }

    private void UpdateEnemies(float elapsed)
    {
        _spawnElapsed += elapsed;
        while (_spawnElapsed >= 10f)
        {
            _spawnElapsed -= 10f;
            SpawnEnemy();
        }

        for (var i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            var sprite = enemy.Sprite;
            enemy.PreviousArea = sprite.CollisionArea;
            enemy.Age += elapsed;
            if (enemy.Flattened)
            {
                // Hold the flattened pose briefly, then fade over 0.6 seconds.
                var fade = Math.Clamp((enemy.Age - 0.2f) / 0.6f, 0f, 1f);
                sprite.CurrentFrame = _tilesheet[PlatformerArt.EnemyFlattenedFrame +
                    (int)(fade * (PlatformerArt.EnemyFadeFrames - 1)), 0];
                if (fade < 1f)
                    continue;
            }
            else if (sprite.GetPosition().Y <= WorldRows + 2)
            {
                var dx = _player.GetPosition().X - sprite.GetPosition().X;
                sprite.Movement.SetVelocity(new Vector2(
                    Math.Abs(dx) < 0.1f ? 0f : Math.Sign(dx) * 2f,
                    Math.Min(sprite.Movement.MovementState.Velocity.Y, MaxFallSpeed)));
                sprite.CurrentFrame = _tilesheet[
                    PlatformerArt.EnemyWalkFrame + (int)(enemy.Age / 0.16f) % 2, 0];
                continue;
            }

            sprite.Visible = false;
            sprite.CollisionsEnabled = false;
            sprite.Movement.StopAllMovement();
            sprite.Dispose();
            _enemies.RemoveAt(i);
        }
    }

    private bool ResolveEnemyContacts()
    {
        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (enemy.Flattened)
                continue;

            var area = _player.CollisionArea;
            var target = enemy.Sprite.CollisionArea;
            if (EnemyContact.IsStomp(_previousPlayerArea, area,
                    enemy.PreviousArea, target, _player.Movement.MovementState.Velocity.Y))
            {
                enemy.Flattened = true;
                enemy.Age = 0f;
                enemy.Sprite.CollisionsEnabled = false;
                enemy.Sprite.Movement.StopAllMovement();
                enemy.Sprite.CurrentFrame = _tilesheet[PlatformerArt.EnemyFlattenedFrame, 0];
                var position = _player.GetPosition();
                position.Y += (target.Top - area.Bottom) / (float)PlatformerArt.TileSize;
                _player.SetPosition(position);
                var velocity = _player.Movement.MovementState.Velocity;
                _player.Movement.SetVelocity(new Vector2(velocity.X, -JumpSpeed * 0.65f));
                _grounded = false;
            }
            else if (area.IntersectsWith(target))
            {
                enemy.Sprite.Visible = false;
                enemy.Sprite.CollisionsEnabled = false;
                enemy.Sprite.Movement.StopAllMovement();
                enemy.Sprite.Dispose();
                _enemies.RemoveAt(i);
                Respawn("Mushrooms have a personal-space problem.");
                return true;
            }
        }

        return false;
    }

    private void ClearEnemies()
    {
        foreach (var enemy in _enemies)
        {
            enemy.Sprite.Visible = false;
            enemy.Sprite.CollisionsEnabled = false;
            enemy.Sprite.Movement.StopAllMovement();
            enemy.Sprite.Dispose();
        }
        _enemies.Clear();
    }

    private sealed class MushroomEnemy(Sprite sprite)
    {
        internal Sprite Sprite { get; } = sprite;
        internal Rectangle PreviousArea { get; set; } = sprite.CollisionArea;
        internal float Age { get; set; }
        internal bool Flattened { get; set; }
    }

    private bool IsStandingOnSolid()
    {
        var area = _player.CollisionArea;
        var playerCollider = _player.Collider!;
        var footProbe = new Aabb(
            area.Left + 3,
            area.Bottom,
            area.Right - 3,
            area.Bottom + 2);

        _worldLayer.ColliderRegistry.QueryAabb(
            footProbe,
            playerCollider.CollisionGroup,
            playerCollider.CollidesWith,
            _groundProbeResults,
            ignore: playerCollider);

        return _groundProbeResults.Any(collider =>
            collider.IsStatic &&
            collider.ResponseType == CollisionResponseType.Solid &&
            collider.BoundsWorldPx.MinY >= area.Bottom - 1);
    }

    private void CollectRelics()
    {
        foreach (var relic in _relics)
        {
            if (!relic.Visible || !_player.CollisionArea.IntersectsWith(relic.CollisionArea))
                continue;

            relic.Visible = false;
            _relicsCollected++;
            ShowTemporaryMessage("Sun relic recovered.", 1.25d);
        }
    }

    private void SetPlayerFacing(bool left)
    {
        _facingLeft = left;
        _player.CurrentFrame = _tilesheet[
            left ? PlatformerArt.PlayerLeftFrame : PlatformerArt.PlayerRightFrame,
            0];
    }

    private void Respawn(string message)
    {
        _player.SetPosition(SpawnPosition);
        _previousPlayerArea = _player.CollisionArea;
        _player.Movement.SetVelocity(Vector2.Zero);
        _player.Movement.SetAcceleration(new Vector2(0f, Gravity));
        _grounded = false;
        ShowTemporaryMessage(message, 2d);
    }

    private void WinGame()
    {
        _gameState = GameState.Won;
        _keysDown.Clear();
        ClearEnemies();
        _player.Movement.StopAllMovement();
        _messageText.SetText("YOU FOUND THE OLD ROAD\nPress R to play again");
        _messageText.Visible = true;
        UpdateHud(force: true);
    }

    private void RestartGame()
    {
        _gameState = GameState.Playing;
        _relicsCollected = 0;
        ClearEnemies();
        _spawnElapsed = 0f;
        _lastEnemyTick = HighResTimer.GetCurrentTick();

        foreach (var relic in _relics)
            relic.Visible = true;

        Respawn("The road begins again.");
        SpawnEnemy();
        UpdateHud(force: true);
    }

    private void ShowTemporaryMessage(string message, double seconds)
    {
        if (_gameState != GameState.Playing)
            return;

        _statusMessage = message;
        _statusMessageExpiresUtc = DateTime.UtcNow.AddSeconds(seconds);
        _messageText.SetText(message);
        _messageText.Visible = true;
    }

    private void UpdateMessageVisibility()
    {
        if (string.IsNullOrEmpty(_statusMessage) || DateTime.UtcNow < _statusMessageExpiresUtc)
            return;

        _statusMessage = string.Empty;
        _messageText.Visible = false;
    }

    private void UpdateHud(bool force = false)
    {
        var state = _gameState == GameState.Won ? "Road found" : "Find the old road";
        var hud =
            $"Relics {_relicsCollected}/{_relics.Count}   {state}\n" +
            "A/D or ←/→ move   W/↑/Space jump   R restart   Esc quit";

        if (!force && string.Equals(hud, _lastHudText, StringComparison.Ordinal))
            return;

        _lastHudText = hud;
        _hudText.SetText(hud);
    }

    private enum GameState
    {
        Playing,
        Won
    }
}
