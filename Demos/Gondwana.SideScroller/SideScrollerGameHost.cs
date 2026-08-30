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

namespace Gondwana.Demos.SideScroller;

internal sealed class SideScrollerGameHost : WinFormsGpuGameHost
{
    private const int WorldColumns = 160;
    private const int WorldRows = 18;
    private const int TileSize = 64;
    private const float ScrollSpeed = 4.2f;
    private const float MoveSpeed = 6.5f;
    private const float ShotSpeed = 15f;
    private const float EnemyShotSpeed = 8f;
    private const float PlayerFireDelay = .16f;
    private const float PlayerMaxHealth = 100f;

    private readonly HashSet<Keys> _keysDown = [];
    private readonly List<Enemy> _enemies = [];
    private readonly List<Projectile> _projectiles = [];
    private Tilesheet _tilesheet = null!;
    private SceneLayer _gameplayLayer = null!;
    private Sprite _player = null!;
    private HealthBarWidget _healthBar = null!;
    private ParticleSurface _particles = null!;
    private TextBlock _hud = null!;
    private TextBlock _message = null!;
    private long _lastTick;
    private float _delta;
    private float _playerHealth = PlayerMaxHealth;
    private float _fireCooldown;
    private int _score;
    private bool _playing;

    internal SideScrollerGameHost(WinFormGpuRenderSurfaceControl renderSurface) : base(renderSurface) { }

    internal void StartGame()
    {
        _playing = true;
        _lastTick = HighResTimer.GetCurrentTick();
        UpdateHud();
    }

    protected override void LoadTilesheets()
    {
        _tilesheet = Engine.Managers.Tilesheets.LoadFromBitmap("azure-strike", SideScrollerArt.CreateBitmap());
        _tilesheet.DefaultRegion.TileSize = new Size(SideScrollerArt.FrameSize, SideScrollerArt.FrameSize);
    }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();
        SceneLayer far = AddLayer(scene, 0, .08f);
        SceneLayer nebula = AddLayer(scene, 1, .22f);
        SceneLayer near = AddLayer(scene, 2, .48f);
        _gameplayLayer = AddLayer(scene, 10, 1f);
        Populate(far, SideScrollerArt.FarStars, 520, 7319);
        Populate(nebula, SideScrollerArt.Nebula, 95, 2481);
        Populate(near, SideScrollerArt.NearStars, 310, 9907);
        return scene;
    }

    protected override void OnSceneBound()
    {
        RenderSurface.Host.Backbuffer.ClearColor = new SKColor(2, 5, 18);
        var view = RenderSurface.Host.ViewManager.Views[0];
        view.Camera.WorldBoundsPx = Scene!.GetWorldBoundsPx();
        view.Camera.SnapTo(PointF.Empty);
    }

    protected override void CreateSprites()
    {
        _player = CreateSprite("player", SideScrollerArt.Player, new Vector2(5, 9), new Size(76, 58));
        _player.ZOrder = 30;

        var random = new Random(4771);
        for (int wave = 0; wave < 18; wave++)
        {
            float x = 16 + wave * 7.2f;
            int count = 2 + wave % 3;
            for (int i = 0; i < count; i++)
            {
                float y = 3f + i * 4f + (float)random.NextDouble() * 2f;
                Sprite sprite = CreateSprite($"raider-{wave}-{i}", SideScrollerArt.Enemy,
                    new Vector2(x + i * 1.2f, y), new Size(64, 48));
                _enemies.Add(new Enemy(sprite, y, .8f + (float)random.NextDouble() * 1.2f,
                    .5f + (float)random.NextDouble() * 1.4f));
            }
        }

        var camera = RenderSurface.Host.ViewManager.Views[0].Camera;
        camera.FollowCenteredX(_player, speed: 10f);
    }

    protected override void CreateDirectDrawings()
    {
        _healthBar = new HealthBarWidget(RenderSurface.Host, _player, PlayerMaxHealth,
            new Size(86, 9), nickname: "player-health");
        _healthBar.SetFillColor(Color.FromArgb(245, 50, 220, 250));
        _healthBar.SetZOrder(200); _healthBar.Show();

        RectangleF bounds = Scene!.GetWorldBoundsPx();
        _particles = new ParticleSurface(RenderSurface.Host, _gameplayLayer,
            Rectangle.FromLTRB((int)bounds.Left, (int)bounds.Top, (int)bounds.Right, (int)bounds.Bottom),
            "azure-strike-particles", 1000) { ZOrder = 80 };

        var view = RenderSurface.Host.ViewManager.Views[0];
        _hud = new TextBlock(RenderSurface.Host, view, new Rectangle(18, 16, 720, 58), "hud")
            .SetFont(SKTypeface.Default, 18f).SetColors(SKColors.White, new SKColor(4, 12, 34, 210))
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center).EnableWrapping(false).UseShadow();
        _hud.HorizontalPadding = 14; _hud.ZOrder = 1000;

        _message = new TextBlock(RenderSurface.Host, view, new Rectangle(260, 235, 760, 170), "message")
            .SetFont(SKTypeface.Default, 38f, 24f).SetColors(SKColors.White, new SKColor(7, 16, 42, 230))
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center).SetMaxLines(3).UseShadow().UseOutline();
        _message.ZOrder = 1100; _message.Visible = false;
    }

    protected override void OnKeyboardAdapterInitialized()
    {
        var keyboard = Engine.Input.KeyboardEventPoller!;
        keyboard.KeyDown += OnKeyDown;
        foreach (Keys key in MonitoredKeys) keyboard.StartMonitoringKey((int)key, key.ToString());
    }

    protected override void OnEngineInitialized()
    {
        _lastTick = HighResTimer.GetCurrentTick();
        Engine.BeforeBackgroundTasksExecute += BeforeUpdate;
        Engine.AfterBackgroundTasksExecute += AfterUpdate;
    }

    protected override void UnhookEvents()
    {
        if (Engine.Input.KeyboardEventPoller is not null) Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;
        Engine.BeforeBackgroundTasksExecute -= BeforeUpdate;
        Engine.AfterBackgroundTasksExecute -= AfterUpdate;
    }

    private static Keys[] MonitoredKeys =>
        [Keys.W, Keys.A, Keys.S, Keys.D, Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Space, Keys.R];

    private static SceneLayer AddLayer(Scene scene, int z, float parallax) =>
        scene.AddLayer(WorldColumns, WorldRows, TileSize, TileSize, z, parallax, CoordinateSystemTypes.Orthogonal);

    private void Populate(SceneLayer layer, int frame, int count, int seed)
    {
        var random = new Random(seed);
        var occupied = new HashSet<(int, int)>();
        while (occupied.Count < count)
        {
            var p = (random.Next(WorldColumns), random.Next(WorldRows));
            if (occupied.Add(p)) layer[p.Item1, p.Item2]!.CurrentFrame = _tilesheet[frame, 0];
        }
    }

    private Sprite CreateSprite(string name, int frame, Vector2 position, Size renderSize)
    {
        Sprite sprite = Engine.Managers.Sprites.CreateSprite(_gameplayLayer, _tilesheet[frame, 0], name);
        sprite.RenderSize = renderSize; sprite.HorizAlign = SpriteHorizontalAlignment.Center;
        sprite.VertAlign = SpriteVerticalAlignment.Middle; sprite.SetPosition(position);
        sprite.Visible = true; sprite.ZOrder = 30;
        sprite.AdjustCollisionArea = new CollisionAdjust(8, 8, 8, 8);
        return sprite;
    }

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (!Enum.TryParse(args.KeyConfig.Key, true, out Keys key)) return;
        if (args.KeyAction == KeyAction.Pressed)
        {
            _keysDown.Add(key);
            if (key == Keys.R && !_playing) Restart();
        }
        else if (args.KeyAction == KeyAction.Released) _keysDown.Remove(key);
    }

    private void BeforeUpdate()
    {
        long tick = HighResTimer.GetCurrentTick();
        _delta = Math.Clamp(HighResTimer.GetDuration(_lastTick, tick), 0, .05f); _lastTick = tick;
        if (!_playing || _delta <= 0) return;
        _fireCooldown = Math.Max(0, _fireCooldown - _delta);

        float vertical = Axis(Keys.W, Keys.Up, Keys.S, Keys.Down);
        float horizontal = Axis(Keys.A, Keys.Left, Keys.D, Keys.Right);
        Vector2 velocity = new(ScrollSpeed + horizontal * MoveSpeed * .55f, vertical * MoveSpeed);
        _player.Movement.SetVelocity(velocity);
        ClampPlayer();

        if (_keysDown.Contains(Keys.Space) && _fireCooldown <= 0)
        {
            Fire(_player, true); _fireCooldown = PlayerFireDelay;
        }

        foreach (Enemy enemy in _enemies.Where(e => e.Alive))
        {
            enemy.Age += _delta; enemy.FireCooldown -= _delta;
            float yVelocity = MathF.Cos(enemy.Age * enemy.Frequency) * 2.2f;
            enemy.Sprite.Movement.SetVelocity(new Vector2(-1.1f, yVelocity));
            if (enemy.FireCooldown <= 0 && MathF.Abs(enemy.Sprite.GetPosition().X - _player.GetPosition().X) < 13f)
            {
                Fire(enemy.Sprite, false); enemy.FireCooldown = 1.3f + enemy.Frequency * .5f;
            }
        }
    }

    private void AfterUpdate()
    {
        if (!_playing) return;
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            Projectile shot = _projectiles[i]; shot.Age += _delta;
            if (shot.Age > 2.5f) { RemoveShot(i); continue; }
            if (shot.Friendly)
            {
                Enemy? hit = _enemies.FirstOrDefault(e => e.Alive && e.Sprite.CollisionArea.IntersectsWith(shot.Sprite.CollisionArea));
                if (hit is not null) { Destroy(hit); RemoveShot(i); }
            }
            else if (_player.CollisionArea.IntersectsWith(shot.Sprite.CollisionArea))
            {
                Explode(shot.Sprite.CollisionArea, 20); RemoveShot(i); DamagePlayer(12);
            }
        }

        if (_enemies.All(e => !e.Alive)) End("SECTOR CLEARED\nPress R to fly again");
        else if (_player.GetPosition().X >= WorldColumns - 5) End("MISSION COMPLETE\nPress R to fly again");
        UpdateHud();
    }

    private float Axis(Keys negativeA, Keys negativeB, Keys positiveA, Keys positiveB) =>
        (_keysDown.Contains(positiveA) || _keysDown.Contains(positiveB) ? 1 : 0) -
        (_keysDown.Contains(negativeA) || _keysDown.Contains(negativeB) ? 1 : 0);

    private void ClampPlayer()
    {
        Vector2 p = _player.GetPosition();
        float minX = Math.Max(2, RenderSurface.Host.ViewManager.Views[0].Camera.PositionPx.X / TileSize + 2);
        _player.SetPosition(new Vector2(Math.Clamp(p.X, minX, WorldColumns - 2), Math.Clamp(p.Y, 1.5f, WorldRows - 1.5f)));
    }

    private void Fire(Sprite owner, bool friendly)
    {
        Vector2 p = owner.GetPosition() + new Vector2(friendly ? .75f : -.75f, 0);
        Sprite shot = CreateSprite($"shot-{Guid.NewGuid():N}", friendly ? SideScrollerArt.PlayerShot : SideScrollerArt.EnemyShot,
            p, new Size(42, 12));
        shot.ZOrder = 50; shot.AdjustCollisionArea = new CollisionAdjust(2, 2, 4, 4);
        shot.Movement.SetVelocity(new Vector2(friendly ? ShotSpeed : -EnemyShotSpeed, 0));
        _projectiles.Add(new Projectile(shot, friendly));
    }

    private void Destroy(Enemy enemy)
    {
        enemy.Alive = false; Explode(enemy.Sprite.CollisionArea, 55);
        enemy.Sprite.Movement.StopAllMovement(); enemy.Sprite.Visible = false; _score += 100;
    }

    private void DamagePlayer(float damage)
    {
        _playerHealth = Math.Max(0, _playerHealth - damage); _healthBar.Value = _playerHealth;
        if (_playerHealth <= 0) { _player.Visible = false; Explode(_player.CollisionArea, 100); End("SHIP DESTROYED\nPress R to redeploy"); }
    }

    private void Explode(Rectangle area, int count)
    {
        _particles.Burst(new ParticleEmitter
        {
            Position = new PointF(area.Left + area.Width / 2f, area.Top + area.Height / 2f),
            EmitRate = 0, LifeRange = (.3f, .9f), VelocityRangeX = (-260, 260), VelocityRangeY = (-260, 260),
            SizeRange = (3, 9), Color = new SKColor(255, 145, 50), SpawnDistribution = ParticleSpawnDistribution.Gaussian, GravityY = 0
        }, count);
    }

    private void End(string text)
    {
        _playing = false; _keysDown.Clear(); _player.Movement.StopAllMovement();
        _message.SetText(text); _message.Visible = true;
    }

    private void Restart()
    {
        for (int i = _projectiles.Count - 1; i >= 0; i--) RemoveShot(i);
        _playerHealth = PlayerMaxHealth; _score = 0; _fireCooldown = 0;
        _player.SetPosition(new Vector2(5, 9)); _player.Visible = true; _healthBar.Value = PlayerMaxHealth; _healthBar.Show();
        foreach (Enemy enemy in _enemies) { enemy.Alive = true; enemy.Age = 0; enemy.Sprite.SetPosition(enemy.Spawn); enemy.Sprite.Visible = true; }
        var camera = RenderSurface.Host.ViewManager.Views[0].Camera; camera.ClearFollow(); camera.SnapTo(PointF.Empty); camera.FollowCenteredX(_player, 10f);
        _message.Visible = false; _playing = true; _lastTick = HighResTimer.GetCurrentTick(); UpdateHud();
    }

    private void RemoveShot(int index)
    {
        _projectiles[index].Sprite.Visible = false; _projectiles[index].Sprite.Dispose(); _projectiles.RemoveAt(index);
    }

    private void UpdateHud() => _hud.SetText($"AZURE STRIKE   Hull {_playerHealth:0}%   Score {_score:00000}   Raiders {_enemies.Count(e => e.Alive)}\nMove WASD/arrows   Fire Space   Restart R   Quit Esc");

    private sealed class Enemy
    {
        internal Enemy(Sprite sprite, float centerY, float frequency, float cooldown)
        { Sprite = sprite; Spawn = sprite.GetPosition(); CenterY = centerY; Frequency = frequency; FireCooldown = cooldown; }
        internal Sprite Sprite { get; }
        internal Vector2 Spawn { get; }
        internal float CenterY { get; }
        internal float Frequency { get; }
        internal float FireCooldown { get; set; }
        internal float Age { get; set; }
        internal bool Alive { get; set; } = true;
    }

    private sealed class Projectile(Sprite sprite, bool friendly)
    {
        internal Sprite Sprite { get; } = sprite;
        internal bool Friendly { get; } = friendly;
        internal float Age { get; set; }
    }
}
