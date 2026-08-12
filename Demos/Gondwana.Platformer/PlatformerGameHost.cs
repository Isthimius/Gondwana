using System.Numerics;
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
        _player.Movement.SetVelocity(Vector2.Zero);
        _player.Movement.SetAcceleration(new Vector2(0f, Gravity));
        _grounded = false;
        ShowTemporaryMessage(message, 2d);
    }

    private void WinGame()
    {
        _gameState = GameState.Won;
        _keysDown.Clear();
        _player.Movement.StopAllMovement();
        _messageText.SetText("YOU FOUND THE OLD ROAD\nPress R to play again");
        _messageText.Visible = true;
        UpdateHud(force: true);
    }

    private void RestartGame()
    {
        _gameState = GameState.Playing;
        _relicsCollected = 0;

        foreach (var relic in _relics)
            relic.Visible = true;

        Respawn("The road begins again.");
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
