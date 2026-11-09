using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Input.Keyboard;
using Gondwana.Scenes;
using Gondwana.Timers;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;
using SpriteHorizontalAlignment = Gondwana.Drawing.Sprites.HorizontalAlignment;
using SpriteVerticalAlignment = Gondwana.Drawing.Sprites.VerticalAlignment;

namespace Gondwana.Torchlight;

/// <summary>
/// A small, intentionally game-like test of view darkness, tracked radial lights,
/// independent flickering light sources, world positioning, and camera follow.
/// </summary>
internal sealed class TorchlightGameHost : WinFormsGpuGameHost
{
    private const int Columns = 40;
    private const int Rows = 26;
    private const float WalkSpeedTilesPerSecond = 4.2f;
    private const float PlayerRadiusTiles = 0.29f;
    private const float DefaultTorchRadius = 155f;

    private static readonly Vector2 Spawn = new(8f, 12f);
    private static readonly Point[] WallTorches =
    [
        new(6, 5), new(10, 19), new(19, 13), new(24, 5), new(34, 18)
    ];

    private readonly HashSet<Keys> _keysDown = [];
    private readonly bool[,] _blocked = new bool[Columns, Rows];
    private readonly List<DirectRadialLight> _wallLights = [];

    private SceneLayer _floorLayer = null!;
    private SceneLayer _actorLayer = null!;
    private Sprite _player = null!;
    private DirectLightLayer _lights = null!;
    private DirectRadialLight _playerTorch = null!;
    private DirectDarknessOverlay _darkness = null!;
    private TextBlock _hud = null!;

    private bool _flicker = true;
    private bool _wallTorchesEnabled = true;
    private bool _playerTorchEnabled = true;
    private float _radius = DefaultTorchRadius;
    private long _lastTick;
    private int _playerFrame = DungeonArt.HeroDown;

    internal TorchlightGameHost(WinFormGpuRenderSurfaceControl renderSurface)
        : base(renderSurface)
    {
    }

    protected override void LoadTilesheets() => DungeonArt.Load(Engine.Managers.Tilesheets);

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();
        _floorLayer = scene.AddLayer(Columns, Rows, DungeonArt.TileSize, DungeonArt.TileSize,
            zOrder: 0, parallax: 1f, coordinateSystem: CoordinateSystemTypes.Orthogonal);
        _actorLayer = scene.AddLayer(Columns, Rows, DungeonArt.TileSize, DungeonArt.TileSize,
            zOrder: 10, parallax: 1f, coordinateSystem: CoordinateSystemTypes.Orthogonal);

        for (int y = 0; y < Rows; y++)
        for (int x = 0; x < Columns; x++)
            _floorLayer[x, y]!.CurrentFrame = DungeonArt.Get((x * 17 + y * 13) % 9 == 0
                ? DungeonArt.FloorCracked : DungeonArt.Floor);

        // Dividing walls deliberately have gaps to walk through, so moving lights
        // exercise screen projection and camera-follow across multiple rooms.
        for (int x = 0; x < Columns; x++)
        {
            AddWall(x, 0);
            AddWall(x, Rows - 1);
        }

        for (int y = 1; y < Rows - 1; y++)
        {
            AddWall(0, y);
            AddWall(Columns - 1, y);
        }

        for (int y = 2; y < 23; y++)
            if (y is < 11 or > 14)
                AddWall(14, y);

        for (int x = 15; x < 38; x++)
            if (x is < 22 or > 26)
                AddWall(x, 8);

        for (int y = 9; y < Rows - 1; y++)
            if (y is < 17 or > 20)
                AddWall(29, y);

        foreach (Point point in new[] { new Point(5, 9), new Point(11, 16),
                                        new Point(20, 19), new Point(34, 12) })
            AddWall(point.X, point.Y, DungeonArt.Pillar);

        foreach (Point point in WallTorches)
            _actorLayer[point.X, point.Y]!.CurrentFrame = DungeonArt.Get(DungeonArt.Sconce);

        foreach (Point point in new[] { new Point(9, 6), new Point(25, 14), new Point(34, 21) })
            _floorLayer[point.X, point.Y]!.CurrentFrame = DungeonArt.Get(DungeonArt.Rune);

        foreach (Point point in new[] { new Point(4, 16), new Point(18, 4), new Point(36, 23) })
            _actorLayer[point.X, point.Y]!.CurrentFrame = DungeonArt.Get(DungeonArt.Stones);

        return scene;
    }

    private void AddWall(int x, int y, int frame = DungeonArt.Wall)
    {
        _blocked[x, y] = true;
        _actorLayer[x, y]!.CurrentFrame = DungeonArt.Get(frame);
    }

    protected override void OnSceneBound()
    {
        RenderSurface.Host.Backbuffer.ClearColor = new SKColor(20, 22, 30);
        var camera = RenderSurface.Host.ViewManager.Views[0].Camera;
        camera.WorldBoundsPx = Scene!.GetWorldBoundsPx();
        camera.SnapTo(PointF.Empty);
    }

    protected override void CreateSprites()
    {
        _player = Engine.Managers.Sprites.CreateSprite(
            _actorLayer, DungeonArt.Get(_playerFrame), "dungeon-explorer");
        _player.RenderSize = new Size(30, 32);
        _player.HorizAlign = SpriteHorizontalAlignment.Center;
        _player.VertAlign = SpriteVerticalAlignment.Middle;
        _player.SetPosition(Spawn);
        _player.Visible = true;
        _player.ZOrder = 100;

        var camera = RenderSurface.Host.ViewManager.Views[0].Camera;
        camera.FollowCentered(_player, speed: 12f, hard: true);
        camera.CenterOnGrid(_actorLayer, (int)Spawn.X, (int)Spawn.Y);
    }

    protected override void CreateDirectDrawings()
    {
        var host = RenderSurface.Host;
        var view = host.ViewManager.Views[0];

        _lights = new DirectLightLayer(host, _actorLayer);
        _darkness = new DirectDarknessOverlay(host, view, _actorLayer, "dungeon-darkness")
            .SetDarknessColor(Color.Black)
            .SetDarknessOpacity(214)
            .SetInnerClearRadiusRatio(0.18f)
            .SetMidpointRadiusRatio(0.62f)
            .SetMidpointStrength(0.48f);

        // Tracking a light owner also tracks lights added after this call.
        _darkness.TrackLightLayer(_lights, radiusScale: 1.16f);

        _playerTorch = _lights.AddTorchLight(TileCenter(Spawn), _radius,
            Color.FromArgb(210, 255, 178, 68), "explorer-torch");
        _playerTorch.Intensity = 0.95f;
        _playerTorch.FlickerEnabled = true;
        _playerTorch.FlickerAmount = 0.12f;
        _playerTorch.FlickerRefreshHz = 12;

        for (int i = 0; i < WallTorches.Length; i++)
        {
            var wall = _lights.AddTorchLight(
                TileCenter(new Vector2(WallTorches[i].X, WallTorches[i].Y)),
                112f, Color.FromArgb(170, 255, 146, 50), $"sconce-{i + 1}");
            wall.Intensity = 0.82f;
            wall.FlickerEnabled = true;
            wall.FlickerAmount = 0.15f;
            wall.FlickerRefreshHz = 9 + i % 4;
            _wallLights.Add(wall);
        }

        // View-space UI must be ordered above the view-space darkness overlay.
        _hud = new TextBlock(host, view, new Rectangle(14, 10, 930, 69), "torchlight-hud")
            .SetFont(SKTypeface.Default, 16f)
            .SetColors(new SKColor(255, 227, 185), new SKColor(21, 23, 30, 222))
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center)
            .SetMaxLines(2)
            .UseShadow();
        _hud.HorizontalPadding = 12;
        _hud.ZOrder = 30_000;
        RefreshHud();
    }

    private static PointF TileCenter(Vector2 position) => new(
        (position.X + 0.5f) * DungeonArt.TileSize,
        (position.Y + 0.5f) * DungeonArt.TileSize);

    protected override void OnKeyboardAdapterInitialized()
    {
        var keyboard = Engine.Input.KeyboardEventPoller!;
        keyboard.KeyDown += OnKeyDown;
        foreach (Keys key in new[]
        {
            Keys.W, Keys.A, Keys.S, Keys.D,
            Keys.Up, Keys.Left, Keys.Down, Keys.Right,
            Keys.V, Keys.F, Keys.T, Keys.L, Keys.R,
            Keys.Oemplus, Keys.Add, Keys.OemMinus, Keys.Subtract
        })
            keyboard.StartMonitoringKey((int)key, key.ToString());
    }

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (!Enum.TryParse(args.KeyConfig.Key, true, out Keys key))
            return;

        if (args.KeyAction == KeyAction.Released)
        {
            _keysDown.Remove(key);
            return;
        }

        if (args.KeyAction != KeyAction.Pressed || !_keysDown.Add(key))
            return;

        switch (key)
        {
            case Keys.V:
                _darkness.Visible = !_darkness.Visible;
                break;
            case Keys.F:
                _flicker = !_flicker;
                foreach (DirectRadialLight light in _lights.Lights)
                    light.FlickerEnabled = _flicker;
                break;
            case Keys.T:
                _playerTorchEnabled = !_playerTorchEnabled;
                _playerTorch.Intensity = _playerTorchEnabled ? 0.95f : 0f;
                break;
            case Keys.L:
                _wallTorchesEnabled = !_wallTorchesEnabled;
                foreach (DirectRadialLight wall in _wallLights)
                    wall.Intensity = _wallTorchesEnabled ? 0.82f : 0f;
                break;
            case Keys.Oemplus:
            case Keys.Add:
                _radius = Math.Min(270f, _radius + 15f);
                _playerTorch.RadiusWorldPx = _radius;
                break;
            case Keys.OemMinus:
            case Keys.Subtract:
                _radius = Math.Max(60f, _radius - 15f);
                _playerTorch.RadiusWorldPx = _radius;
                break;
            case Keys.R:
                _player.SetPosition(Spawn);
                _playerFrame = DungeonArt.HeroDown;
                _player.CurrentFrame = DungeonArt.Get(_playerFrame);
                _playerTorch.MoveTo(TileCenter(Spawn));
                break;
        }

        RefreshHud();
    }

    private void RefreshHud()
    {
        if (_hud is null)
            return;

        _hud.SetText(
            $"TORCHLIGHT DUNGEON   WASD / Arrows move   V darkness   F flicker   T carried torch   L sconces\n" +
            $"+/- radius ({_radius:0} px)   R respawn   ESC exit   |   Darkness {(_darkness.Visible ? "ON" : "OFF")}  Flicker {(_flicker ? "ON" : "OFF")}");
    }

    protected override void OnEngineInitialized()
    {
        Engine.Configuration.VSync = true;
        _lastTick = HighResTimer.GetCurrentTick();
        Engine.BeforeBackgroundTasksExecute += UpdateMovement;
    }

    private void UpdateMovement()
    {
        long tick = HighResTimer.GetCurrentTick();
        float dt = Math.Clamp(HighResTimer.GetDuration(_lastTick, tick), 0f, 0.05f);
        _lastTick = tick;
        if (dt <= 0f)
            return;

        Vector2 movement = new(
            (_keysDown.Contains(Keys.D) || _keysDown.Contains(Keys.Right) ? 1f : 0f) -
            (_keysDown.Contains(Keys.A) || _keysDown.Contains(Keys.Left) ? 1f : 0f),
            (_keysDown.Contains(Keys.S) || _keysDown.Contains(Keys.Down) ? 1f : 0f) -
            (_keysDown.Contains(Keys.W) || _keysDown.Contains(Keys.Up) ? 1f : 0f));

        if (movement.LengthSquared() > 1f)
            movement = Vector2.Normalize(movement);

        if (movement.LengthSquared() > 0f)
        {
            int nextFrame = MathF.Abs(movement.X) > MathF.Abs(movement.Y)
                ? movement.X < 0 ? DungeonArt.HeroLeft : DungeonArt.HeroRight
                : movement.Y < 0 ? DungeonArt.HeroUp : DungeonArt.HeroDown;

            if (_playerFrame != nextFrame)
            {
                _playerFrame = nextFrame;
                _player.CurrentFrame = DungeonArt.Get(nextFrame);
            }

            Vector2 position = _player.GetPosition();
            Vector2 step = movement * WalkSpeedTilesPerSecond * dt;
            Vector2 horizontal = new(position.X + step.X, position.Y);
            if (CanOccupy(horizontal))
                position = horizontal;
            Vector2 vertical = new(position.X, position.Y + step.Y);
            if (CanOccupy(vertical))
                position = vertical;

            _player.SetPosition(position);
        }

        // DirectRadialLight uses WORLD PIXELS, while Sprite positions use GRID coordinates.
        _playerTorch.MoveTo(TileCenter(_player.GetPosition()));
    }

    private bool CanOccupy(Vector2 position)
    {
        float centerX = position.X + 0.5f;
        float centerY = position.Y + 0.5f;
        foreach (float dx in new[] { -PlayerRadiusTiles, PlayerRadiusTiles })
        foreach (float dy in new[] { -PlayerRadiusTiles, PlayerRadiusTiles })
        {
            int tileX = (int)MathF.Floor(centerX + dx);
            int tileY = (int)MathF.Floor(centerY + dy);
            if (tileX < 0 || tileX >= Columns || tileY < 0 || tileY >= Rows ||
                _blocked[tileX, tileY])
                return false;
        }

        return true;
    }

    protected override void UnhookEvents()
    {
        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;
        Engine.BeforeBackgroundTasksExecute -= UpdateMovement;
    }

    protected override void OnDisposing()
    {
        _darkness?.Dispose();
        _lights?.Dispose();
    }
}
