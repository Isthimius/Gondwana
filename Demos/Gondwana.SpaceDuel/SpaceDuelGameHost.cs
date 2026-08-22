using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Direct.Particles;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes;
using Gondwana.Timers;
using Gondwana.Widgets.Hud;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;
using SpriteHorizontalAlignment = Gondwana.Drawing.Sprites.HorizontalAlignment;
using SpriteVerticalAlignment = Gondwana.Drawing.Sprites.VerticalAlignment;

namespace Gondwana.Demos.SpaceDuel;

internal sealed class SpaceDuelGameHost : WinFormsGpuGameHost
{
    private const int WorldColumns = 90;
    private const int WorldRows = 54;
    private const int WorldTileSize = 64;
    private const float PlayerTurnSpeed = 190f;
    private const float EnemyTurnSpeed = 115f;
    private const float PlayerAcceleration = 5.5f;
    private const float EnemyAcceleration = 3.4f;
    private const float PlayerMaxSpeed = 5.8f;
    private const float EnemyMaxSpeed = 4.1f;
    private const float CoastingDamping = 1.15f;
    private const float ThrustDamping = 0.12f;
    private const float LaserSpeed = 10.5f;
    private const float LaserLifetime = 2.4f;
    private const float PlayerFireDelay = 0.24f;
    private const float EnemyFireDelay = 1.4f;
    private const float PlayerStartingHealth = 500f;
    private const float EnemyStartingHealth = 100f;
    private const float LaserDamage = 20f;
    private const float ShipCollisionBounce = 0.7f;

    private static readonly Size ShipRenderSize = new(108, 108);

    private readonly HashSet<Keys> _keysDown = [];
    private readonly List<ShipState> _ships = [];
    private readonly List<ShipState> _enemies = [];
    private readonly List<Projectile> _projectiles = [];

    private Tilesheet _shipTilesheet = null!;
    private Tilesheet _effectsTilesheet = null!;
    private SceneLayer _farStarLayer = null!;
    private SceneLayer _nearStarLayer = null!;
    private SceneLayer _shipLayer = null!;
    private ShipState _player = null!;
    private TextBlock _hudText = null!;
    private TextBlock _messageText = null!;
    private ParticleSurface _particleSurface = null!;
    private TextBlock _performanceText = null!;

    private long _lastUpdateTick;
    private float _frameDelta;
    private GameState _gameState = GameState.Starting;
    private string _lastHud = string.Empty;

    internal SpaceDuelGameHost(WinFormGpuRenderSurfaceControl renderSurface)
        : base(renderSurface)
    {
    }

    internal void BeginPostSplashStartup()
    {
        if (_gameState != GameState.Starting)
            return;

        _gameState = GameState.Playing;
        _lastUpdateTick = HighResTimer.GetCurrentTick();
        UpdateHud(force: true);
    }

    protected override void LoadTilesheets()
    {
        _shipTilesheet = Engine.Managers.Tilesheets.LoadFromBitmap(
            "space-duel-ships",
            SpaceDuelArt.LoadShipBitmap());

        _shipTilesheet.DefaultRegion.TileSize = new Size(
            SpaceDuelArt.ShipFrameSize,
            SpaceDuelArt.ShipFrameSize);

        _effectsTilesheet = Engine.Managers.Tilesheets.LoadFromBitmap(
            "space-duel-effects",
            SpaceDuelArt.CreateEffectsBitmap());

        _effectsTilesheet.DefaultRegion.TileSize = new Size(
            SpaceDuelArt.EffectsFrameSize,
            SpaceDuelArt.EffectsFrameSize);
    }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();

        _farStarLayer = AddLayer(scene, zOrder: 0, parallax: 0.16f);
        _nearStarLayer = AddLayer(scene, zOrder: 2, parallax: 0.42f);
        _shipLayer = AddLayer(scene, zOrder: 10, parallax: 1f);

        PopulateStars(
            _farStarLayer,
            SpaceDuelArt.FarStarFrame,
            count: 315,
            seed: 7319);

        PopulateStars(
            _nearStarLayer,
            SpaceDuelArt.NearStarFrame,
            count: 162,
            seed: 1907);

        return scene;
    }

    protected override void OnSceneBound()
    {
        RenderSurface.Host.Backbuffer.ClearColor = new SKColor(2, 7, 19);

        var view = RenderSurface.Host.ViewManager.Views[0];
        view.Camera.WorldBoundsPx = Scene!.GetWorldBoundsPx();
        view.Camera.SnapTo(PointF.Empty);
    }

    protected override void CreateSprites()
    {
        _player = CreateShip(
            "player",
            frameColumn: 0,
            frameRow: 0,
            position: new Vector2(15f, 9f),
            rotation: 0f,
            isPlayer: true);

        _enemies.Add(CreateShip(
            "raider-one",
            frameColumn: 1,
            frameRow: 0,
            position: new Vector2(8f, 5f),
            rotation: 135f,
            isPlayer: false));

        _enemies.Add(CreateShip(
            "raider-two",
            frameColumn: 0,
            frameRow: 1,
            position: new Vector2(23f, 6f),
            rotation: 225f,
            isPlayer: false));

        _enemies.Add(CreateShip(
            "raider-three",
            frameColumn: 1,
            frameRow: 1,
            position: new Vector2(20f, 14f),
            rotation: 315f,
            isPlayer: false));

        _ships.Add(_player);
        _ships.AddRange(_enemies);

        RenderSurface.Host.ViewManager.Views[0].Camera
            .FollowCentered(_player.Sprite, speed: 8f);
    }

    protected override void CreateDirectDrawings()
    {
        foreach (ShipState ship in _ships)
        {
            var bar = new HealthBarWidget(
                RenderSurface.Host,
                ship.Sprite,
                maximum: ship.MaxHealth,
                size: new Size(72, 9),
                nickname: $"{ship.Sprite.Nickname}-health");

            if (!ship.IsPlayer)
                bar.SetFillColor(Color.FromArgb(245, 244, 98, 78));

            bar.SetZOrder(200);
            bar.Show();
            ship.HealthBar = bar;
        }

        var worldBounds = Scene!.GetWorldBoundsPx();

        _particleSurface = new ParticleSurface(
            RenderSurface.Host,
            _shipLayer,
            Rectangle.FromLTRB(
                (int)MathF.Floor(worldBounds.Left),
                (int)MathF.Floor(worldBounds.Top),
                (int)MathF.Ceiling(worldBounds.Right),
                (int)MathF.Ceiling(worldBounds.Bottom)),
            "space-duel-explosions",
            maxParticles: 1400)
        {
            GravityX = 0f,
            GravityY = 0f,
            ZOrder = 60
        };

        var view = RenderSurface.Host.ViewManager.Views[0];

        var hudPanel = new DirectRectangle(
                Color.FromArgb(210, 9, 18, 37),
                RenderSurface.Host,
                view,
                new Rectangle(12, 12, 535, 69),
                "space-duel-hud-panel")
            .SetFilled(true)
            .SetBorderColor(Color.FromArgb(230, 82, 207, 236))
            .SetStrokeWidth(2f)
            .SetCornerRadius(8f);

        hudPanel.ZOrder = 1000;

        _hudText = new TextBlock(
                RenderSurface.Host,
                view,
                new Rectangle(26, 21, 507, 51),
                "space-duel-hud")
            .SetFont(SKTypeface.Default, 17f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false)
            .UseShadow();

        _hudText.ZOrder = 1001;

        _messageText = new TextBlock(
                RenderSurface.Host,
                view,
                new Rectangle(170, 236, 620, 150),
                "space-duel-message")
            .SetFont(SKTypeface.Default, 34f, minSize: 22f)
            .SetColors(SKColors.White, new SKColor(8, 18, 38, 225))
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .SetMaxLines(3)
            .UseShadow()
            .UseOutline();

        _messageText.HorizontalPadding = 20f;
        _messageText.VerticalPadding = 14f;
        _messageText.ZOrder = 1100;
        _messageText.Visible = false;

        UpdateHud(force: true);

        var viewport = view.Viewport.TargetRectPx;

        const int performanceWidth = 300;
        const int performanceHeight = 42;
        const int performanceMargin = 16;

        _performanceText = new TextBlock(
                RenderSurface.Host,
                view,
                new Rectangle(
                    viewport.Right - performanceWidth - performanceMargin,
                    viewport.Top + performanceMargin,
                    performanceWidth,
                    performanceHeight),
                "space-duel-performance")
            .SetFont(SKTypeface.Default, 18f)
            .SetColors(SKColors.White, new SKColor(8, 18, 38, 180))
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false)
            .UseShadow();

        _performanceText.ZOrder = 1200;

        Engine.CPSCalculated += OnCpsCalculated;
    }

    private void OnCpsCalculated(CyclesPerSecondCalculatedEventArgs e)
    {
        double fps = e.GpuFps ?? e.NetCPS;

        _performanceText.SetText(
            $"CPS {e.GrossCPS:0.0}   FPS {fps:0.0}");
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
        Engine.CPSCalculated -= OnCpsCalculated;
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

    private static SceneLayer AddLayer(Scene scene, int zOrder, float parallax)
    {
        return scene.AddLayer(
            WorldColumns,
            WorldRows,
            WorldTileSize,
            WorldTileSize,
            zOrder,
            parallax,
            CoordinateSystemTypes.Orthogonal);
    }

    private void PopulateStars(
        SceneLayer layer,
        int frame,
        int count,
        int seed)
    {
        var random = new Random(seed);
        var occupied = new HashSet<(int X, int Y)>();

        while (occupied.Count < count)
        {
            var position = (
                random.Next(WorldColumns),
                random.Next(WorldRows));

            if (!occupied.Add(position))
                continue;

            layer[position.Item1, position.Item2]!.CurrentFrame =
                _effectsTilesheet[frame, 0];
        }
    }

    private ShipState CreateShip(
        string nickname,
        int frameColumn,
        int frameRow,
        Vector2 position,
        float rotation,
        bool isPlayer)
    {
        Sprite sprite = Engine.Managers.Sprites.CreateSprite(
            _shipLayer,
            _shipTilesheet[frameColumn, frameRow],
            nickname);

        sprite.RenderSize = ShipRenderSize;
        sprite.HorizAlign = SpriteHorizontalAlignment.Center;
        sprite.VertAlign = SpriteVerticalAlignment.Middle;
        sprite.SetPosition(position);
        sprite.Rotation = rotation;
        sprite.Visible = true;
        sprite.ZOrder = 30;
        sprite.AdjustCollisionArea = new CollisionAdjust(
            top: 18,
            bottom: 18,
            left: 18,
            right: 18);

        sprite.Movement.WrapX = true;
        sprite.Movement.WrapY = true;

        return new ShipState(
            sprite,
            position,
            rotation,
            isPlayer);
    }

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (!Enum.TryParse(args.KeyConfig.Key, ignoreCase: true, out Keys key))
            return;

        switch (args.KeyAction)
        {
            case KeyAction.Pressed:
                _keysDown.Add(key);

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
        long tick = HighResTimer.GetCurrentTick();
        _frameDelta = Math.Clamp(
            HighResTimer.GetDuration(_lastUpdateTick, tick),
            0f,
            0.05f);
        _lastUpdateTick = tick;

        if (_gameState != GameState.Playing || _frameDelta <= 0f)
            return;

        foreach (ShipState ship in _ships)
            ship.FireCooldown = Math.Max(0f, ship.FireCooldown - _frameDelta);

        UpdatePlayerFlight(_frameDelta);

        foreach (ShipState enemy in _enemies)
            UpdateEnemyFlight(enemy, _frameDelta);
    }

    private void AfterBackgroundTasksExecute()
    {
        if (_gameState != GameState.Playing)
            return;

        UpdateProjectiles(_frameDelta);
        ResolveShipCollisions();
        ResolveProjectileHits();
        UpdateHud();
    }

    private void ResolveShipCollisions()
    {
        for (int firstIndex = 0; firstIndex < _ships.Count - 1; firstIndex++)
        {
            ShipState first = _ships[firstIndex];

            if (!first.IsAlive)
                continue;

            for (int secondIndex = firstIndex + 1;
                 secondIndex < _ships.Count;
                 secondIndex++)
            {
                ShipState second = _ships[secondIndex];

                if (!second.IsAlive)
                    continue;

                Rectangle firstBounds = first.Sprite.CollisionArea;
                Rectangle secondBounds = second.Sprite.CollisionArea;

                if (!firstBounds.IntersectsWith(secondBounds))
                    continue;

                SeparateAabbOverlap(
                    first.Sprite,
                    second.Sprite,
                    firstBounds,
                    secondBounds);

                Vector2 firstVelocity =
                    first.Sprite.Movement.MovementState.Velocity;
                Vector2 secondVelocity =
                    second.Sprite.Movement.MovementState.Velocity;

                first.Sprite.Movement.SetVelocity(
                    secondVelocity * ShipCollisionBounce);
                second.Sprite.Movement.SetVelocity(
                    firstVelocity * ShipCollisionBounce);
            }
        }
    }

    private static void SeparateAabbOverlap(
        Sprite first,
        Sprite second,
        Rectangle firstBounds,
        Rectangle secondBounds)
    {
        Rectangle overlap = Rectangle.Intersect(firstBounds, secondBounds);

        if (overlap.Width <= 0 || overlap.Height <= 0)
            return;

        if (overlap.Width <= overlap.Height)
        {
            int totalSeparation = overlap.Width + 1;
            int firstMove = totalSeparation / 2;
            int secondMove = totalSeparation - firstMove;
            bool firstIsLeft =
                firstBounds.Left + firstBounds.Width * 0.5f <=
                secondBounds.Left + secondBounds.Width * 0.5f;

            first.TranslateWorldPx(firstIsLeft ? -firstMove : firstMove, 0);
            second.TranslateWorldPx(firstIsLeft ? secondMove : -secondMove, 0);
        }
        else
        {
            int totalSeparation = overlap.Height + 1;
            int firstMove = totalSeparation / 2;
            int secondMove = totalSeparation - firstMove;
            bool firstIsAbove =
                firstBounds.Top + firstBounds.Height * 0.5f <=
                secondBounds.Top + secondBounds.Height * 0.5f;

            first.TranslateWorldPx(0, firstIsAbove ? -firstMove : firstMove);
            second.TranslateWorldPx(0, firstIsAbove ? secondMove : -secondMove);
        }
    }

    private void UpdatePlayerFlight(float dt)
    {
        bool turnLeft =
            _keysDown.Contains(Keys.A) ||
            _keysDown.Contains(Keys.Left);

        bool turnRight =
            _keysDown.Contains(Keys.D) ||
            _keysDown.Contains(Keys.Right);

        float turn = turnLeft == turnRight
            ? 0f
            : turnLeft ? -1f : 1f;

        _player.Sprite.Rotation = NormalizeDegrees(
            _player.Sprite.Rotation + turn * PlayerTurnSpeed * dt);

        bool thrust =
            _keysDown.Contains(Keys.W) ||
            _keysDown.Contains(Keys.Up);

        ConfigureFlight(
            _player.Sprite,
            thrust,
            PlayerAcceleration,
            PlayerMaxSpeed);

        if (_keysDown.Contains(Keys.Space) && _player.FireCooldown <= 0f)
        {
            FireLaser(_player);
            _player.FireCooldown = PlayerFireDelay;
        }
    }

    private void UpdateEnemyFlight(ShipState enemy, float dt)
    {
        if (!enemy.IsAlive)
            return;

        Vector2 delta = GetWrappedDelta(
            enemy.Sprite.GetPosition(),
            _player.Sprite.GetPosition());

        float distance = delta.Length();
        float desiredRotation = NormalizeDegrees(
            MathF.Atan2(delta.X, -delta.Y) * 180f / MathF.PI);

        float angleDelta = DeltaAngle(
            enemy.Sprite.Rotation,
            desiredRotation);

        float maxTurn = EnemyTurnSpeed * dt;
        enemy.Sprite.Rotation = NormalizeDegrees(
            enemy.Sprite.Rotation + Math.Clamp(angleDelta, -maxTurn, maxTurn));

        bool thrust = distance > 3.2f && MathF.Abs(angleDelta) < 72f;

        ConfigureFlight(
            enemy.Sprite,
            thrust,
            EnemyAcceleration,
            EnemyMaxSpeed);

        if (distance < 12f &&
            MathF.Abs(angleDelta) < 11f &&
            enemy.FireCooldown <= 0f)
        {
            FireLaser(enemy);
            enemy.FireCooldown = EnemyFireDelay;
        }
    }

    private static void ConfigureFlight(
        Sprite sprite,
        bool thrust,
        float acceleration,
        float maxSpeed)
    {
        Vector2 forward = GetForwardVector(sprite.Rotation);

        sprite.Movement.SetAcceleration(
            thrust ? forward * acceleration : Vector2.Zero);

        sprite.Movement.SetMaxSpeed(maxSpeed);
        sprite.Movement.SetLinearDamping(
            thrust ? ThrustDamping : CoastingDamping);
    }

    private void FireLaser(ShipState owner)
    {
        Vector2 forward = GetForwardVector(owner.Sprite.Rotation);
        Vector2 spawnPosition = owner.Sprite.GetPosition() + forward * 0.82f;

        Sprite laser = Engine.Managers.Sprites.CreateSprite(
            _shipLayer,
            _effectsTilesheet[SpaceDuelArt.LaserFrame, 0],
            $"{owner.Sprite.Nickname}-laser");

        laser.RenderSize = new Size(12, 30);
        laser.HorizAlign = SpriteHorizontalAlignment.Center;
        laser.VertAlign = SpriteVerticalAlignment.Middle;
        laser.SetPosition(spawnPosition);
        laser.Rotation = owner.Sprite.Rotation;
        laser.Visible = true;
        laser.ZOrder = 40;
        laser.AdjustCollisionArea = new CollisionAdjust(
            top: 4,
            bottom: 4,
            left: 2,
            right: 2);

        laser.Movement.WrapX = true;
        laser.Movement.WrapY = true;
        laser.Movement.SetVelocity(
            owner.Sprite.Movement.MovementState.Velocity +
            forward * LaserSpeed);

        _projectiles.Add(new Projectile(laser, owner));
    }

    private void UpdateProjectiles(float dt)
    {
        for (int index = _projectiles.Count - 1; index >= 0; index--)
        {
            Projectile projectile = _projectiles[index];
            projectile.Age += dt;

            if (projectile.Age < LaserLifetime)
                continue;

            RemoveProjectileAt(index);
        }
    }

    private void ResolveProjectileHits()
    {
        for (int projectileIndex = _projectiles.Count - 1;
             projectileIndex >= 0;
             projectileIndex--)
        {
            Projectile projectile = _projectiles[projectileIndex];

            ShipState? target = _ships.FirstOrDefault(ship =>
                ship.IsAlive &&
                ship.IsPlayer != projectile.Owner.IsPlayer &&
                ship.Sprite.CollisionArea.IntersectsWith(
                    projectile.Sprite.CollisionArea));

            if (target is null)
                continue;

            CreateHitExplosion(projectile.Sprite.CollisionArea);
            DamageShip(target, LaserDamage);
            RemoveProjectileAt(projectileIndex);

            if (_gameState != GameState.Playing)
                return;
        }
    }

    private void DamageShip(ShipState target, float amount)
    {
        target.Health = Math.Max(0f, target.Health - amount);
        target.HealthBar.Value = target.Health;

        if (target.Health > 0f)
            return;

        CreateDeathExplosion(target.Sprite.CollisionArea);
        target.Sprite.Movement.StopAllMovement();
        target.Sprite.Visible = false;
        target.HealthBar.Hide();

        if (target.IsPlayer)
        {
            EndGame(
                GameState.Lost,
                "SHIP LOST\nPress R to redeploy");
        }
        else if (_enemies.All(enemy => !enemy.IsAlive))
        {
            EndGame(
                GameState.Won,
                "SECTOR SECURED\nPress R for another duel");
        }
    }

    private void CreateHitExplosion(Rectangle collisionArea)
    {
        _particleSurface.Burst(
            CreateExplosionEmitter(
                collisionArea,
                lifeRange: (0.18f, 0.42f),
                velocityRange: 165f,
                sizeRange: (2f, 5f),
                color: new SKColor(255, 194, 72, 245)),
            count: 22);
    }

    private void CreateDeathExplosion(Rectangle collisionArea)
    {
        _particleSurface.Burst(
            CreateExplosionEmitter(
                collisionArea,
                lifeRange: (0.55f, 1.15f),
                velocityRange: 430f,
                sizeRange: (4f, 10f),
                color: new SKColor(255, 102, 34, 255)),
            count: 110);

        _particleSurface.Burst(
            CreateExplosionEmitter(
                collisionArea,
                lifeRange: (0.75f, 1.5f),
                velocityRange: 260f,
                sizeRange: (6f, 14f),
                color: new SKColor(255, 214, 112, 220)),
            count: 48);
    }

    private static ParticleEmitter CreateExplosionEmitter(
        Rectangle collisionArea,
        (float Min, float Max) lifeRange,
        float velocityRange,
        (float Min, float Max) sizeRange,
        SKColor color)
    {
        return new ParticleEmitter
        {
            Position = new PointF(
                collisionArea.Left + collisionArea.Width * 0.5f,
                collisionArea.Top + collisionArea.Height * 0.5f),
            EmitRate = 0f,
            LifeRange = lifeRange,
            VelocityRangeX = (-velocityRange, velocityRange),
            VelocityRangeY = (-velocityRange, velocityRange),
            SizeRange = sizeRange,
            Color = color,
            GravityX = 0f,
            GravityY = 0f,
            JitterX = collisionArea.Width * 0.2f,
            JitterY = collisionArea.Height * 0.2f,
            SpawnDistribution = ParticleSpawnDistribution.Gaussian
        };
    }

    private void EndGame(GameState state, string message)
    {
        _gameState = state;
        _keysDown.Clear();

        foreach (ShipState ship in _ships)
            ship.Sprite.Movement.StopAllMovement();

        _messageText.SetText(message);
        _messageText.Visible = true;
        UpdateHud(force: true);
    }

    private void RestartGame()
    {
        for (int index = _projectiles.Count - 1; index >= 0; index--)
            RemoveProjectileAt(index);

        foreach (ShipState ship in _ships)
        {
            ship.Health = ship.MaxHealth;
            ship.FireCooldown = ship.IsPlayer ? 0f : EnemyFireDelay * 0.5f;
            ship.Sprite.Movement.StopAllMovement();
            ship.Sprite.SetPosition(ship.SpawnPosition);
            ship.Sprite.Rotation = ship.SpawnRotation;
            ship.Sprite.Visible = true;
            ship.HealthBar.Value = ship.MaxHealth;
            ship.HealthBar.RefreshPosition();
            ship.HealthBar.Show();
        }

        _gameState = GameState.Playing;
        _messageText.Visible = false;
        _keysDown.Clear();
        _lastUpdateTick = HighResTimer.GetCurrentTick();
        UpdateHud(force: true);
    }

    private void RemoveProjectileAt(int index)
    {
        Projectile projectile = _projectiles[index];
        projectile.Sprite.Visible = false;
        projectile.Sprite.Dispose();
        _projectiles.RemoveAt(index);
    }

    private void UpdateHud(bool force = false)
    {
        int enemiesRemaining = _enemies.Count(enemy => enemy.IsAlive);
        string state = _gameState switch
        {
            GameState.Won => "Sector secure",
            GameState.Lost => "Ship lost",
            _ => "Destroy the three raiders"
        };

        string hud =
            $"Hull {_player.Health:0}/{_player.MaxHealth:0}   Raiders {enemiesRemaining}/3   {state}\n" +
            "A/D or ←/→ turn   W/↑ thrust   Space fire   R restart   Esc quit";

        if (!force && string.Equals(hud, _lastHud, StringComparison.Ordinal))
            return;

        _lastHud = hud;
        _hudText.SetText(hud);
    }

    private static Vector2 GetForwardVector(float rotation)
    {
        float radians = rotation * MathF.PI / 180f;
        return new Vector2(MathF.Sin(radians), -MathF.Cos(radians));
    }

    private static Vector2 GetWrappedDelta(Vector2 from, Vector2 to)
    {
        float dx = WrapDelta(to.X - from.X, WorldColumns);
        float dy = WrapDelta(to.Y - from.Y, WorldRows);
        return new Vector2(dx, dy);
    }

    private static float WrapDelta(float delta, float period)
    {
        float half = period * 0.5f;

        if (delta > half)
            return delta - period;

        if (delta < -half)
            return delta + period;

        return delta;
    }

    private static float DeltaAngle(float current, float target)
    {
        return (target - current + 540f) % 360f - 180f;
    }

    private static float NormalizeDegrees(float value)
    {
        value %= 360f;
        return value < 0f ? value + 360f : value;
    }

    private sealed class ShipState
    {
        internal ShipState(
            Sprite sprite,
            Vector2 spawnPosition,
            float spawnRotation,
            bool isPlayer)
        {
            Sprite = sprite;
            SpawnPosition = spawnPosition;
            SpawnRotation = spawnRotation;
            IsPlayer = isPlayer;
            MaxHealth = isPlayer
                ? PlayerStartingHealth
                : EnemyStartingHealth;
            Health = MaxHealth;
        }

        internal Sprite Sprite { get; }
        internal Vector2 SpawnPosition { get; }
        internal float SpawnRotation { get; }
        internal bool IsPlayer { get; }
        internal HealthBarWidget HealthBar { get; set; } = null!;
        internal float MaxHealth { get; }
        internal float Health { get; set; }
        internal float FireCooldown { get; set; }
        internal bool IsAlive => Health > 0f && Sprite.Visible;
    }

    private sealed class Projectile
    {
        internal Projectile(Sprite sprite, ShipState owner)
        {
            Sprite = sprite;
            Owner = owner;
        }

        internal Sprite Sprite { get; }
        internal ShipState Owner { get; }
        internal float Age { get; set; }
    }

    private enum GameState
    {
        Starting,
        Playing,
        Won,
        Lost
    }
}
