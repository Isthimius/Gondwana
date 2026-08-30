using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using Gondwana.Timers;
using Gondwana.WinForms;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;

namespace Gondwana.ZeldaPrototype;

internal sealed partial class ZeldaGameHost : WinFormsGameHost
{
    private const int WorldColumns = 80;
    private const int WorldRows = 30;
    private const float PlayerSpeed = 5.2f;
    private const float SwordDurationSeconds = 0.18f;
    private const float DamageCooldownSeconds = 0.85f;
    private const int PlayerMaximumHealth = 8;

    private static readonly Vector2 OverworldSpawn = new(8f, 15f);
    private static readonly Vector2 DungeonSpawn = new(55f, 15f);
    private static readonly Vector2 OverworldReturn = new(44f, 15f);
    private static readonly Vector2 ElderPosition = new(12f, 14f);
    private static readonly Vector2 DungeonEntrancePosition = new(45f, 15f);
    private static readonly Vector2 DungeonExitPosition = new(53f, 15f);

    private readonly HashSet<Keys> _keysDown = [];
    private readonly HashSet<string> _gamepadButtonsDown = new(StringComparer.Ordinal);
    private readonly HashSet<string> _swingHitIds = new(StringComparer.Ordinal);
    private readonly List<EnemyState> _enemies = [];
    private readonly List<PickupState> _pickups = [];
    private readonly List<SceneLayerTile> _gateTiles = [];
    private readonly Dictionary<InventoryItem, int> _inventory = [];
    private readonly HashSet<string> _collectedPickups = new(StringComparer.Ordinal);

    private readonly string[] _elderDialogue =
    [
        "Elder Rowan: The old barrow has opened again.",
        "The rusted key lies beyond the eastern bridge. Take it before you enter.",
        "Steel is honest. Strike with Space or the controller's X button.",
        "Bring down the Hollow King, and the Greenward may yet see spring."
    ];

    private SceneLayer _groundLayer = null!;
    private SceneLayer _objectLayer = null!;
    private SceneLayer _actorLayer = null!;

    private Sprite _player = null!;
    private Sprite _sword = null!;
    private Sprite _elder = null!;
    private GameHealthBar _playerHealthBar = null!;

    private DirectRectangle _hudPanel = null!;
    private TextBlock _hudText = null!;
    private DirectRectangle _messagePanel = null!;
    private TextBlock _messageText = null!;
    private DirectRectangle _inventoryPanel = null!;
    private TextBlock _inventoryText = null!;
    private DirectRectangle _titlePanel = null!;
    private TextBlock _titleText = null!;
    private TextBlock _titleOptionsText = null!;
    private DirectRectangle _pausePanel = null!;
    private TextBlock _pauseText = null!;

    private long _lastUpdateTick;
    private float _frameDelta;
    private float _swordTimer;
    private float _damageCooldown;
    private DateTime _messageExpiresUtc;
    private int _dialogueIndex;
    private int _playerHealth = PlayerMaximumHealth;
    private GameMode _mode = GameMode.Title;
    private WorldArea _currentArea = WorldArea.Overworld;
    private Facing _facing = Facing.Down;
    private string _lastHud = string.Empty;

    internal ZeldaGameHost(WinFormBitmapRenderSurfaceControl renderSurface)
        : base(renderSurface)
    {
        ((BitmapBackbuffer)renderSurface.Host.Backbuffer).FilterQuality = SKFilterQuality.None;
    }

    protected override void LoadTilesheets()
    {
        string assetsDirectory = Path.Combine(AppContext.BaseDirectory, "assets");
        GameArt.Load(Engine.Managers.Tilesheets, assetsDirectory);
    }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();

        _groundLayer = scene.AddLayer(
            WorldColumns,
            WorldRows,
            GameArt.TileSize,
            GameArt.TileSize,
            zOrder: 0,
            parallax: 1f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);

        _objectLayer = scene.AddLayer(
            WorldColumns,
            WorldRows,
            GameArt.TileSize,
            GameArt.TileSize,
            zOrder: 10,
            parallax: 1f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);

        // Collision registries are layer-scoped. Keep blocking map tiles and
        // movable actors on the same layer so the engine's resolver can pair them.
        _actorLayer = _objectLayer;

        BuildWorld();
        return scene;
    }

    protected override void OnSceneBound()
    {
        RenderSurface.Host.Backbuffer.ClearColor = new SKColor(18, 24, 27);

        var camera = RenderSurface.Host.ViewManager.Views[0].Camera;
        camera.WorldBoundsPx = Scene!.GetWorldBoundsPx();
        camera.SnapTo(PointF.Empty);
    }

    protected override void CreateSprites()
    {
        CreateWorldSprites();

        var camera = RenderSurface.Host.ViewManager.Views[0].Camera;
        camera.FollowCentered(_player, speed: 12f, hard: true);
        camera.CenterOnGrid(_actorLayer, (int)OverworldSpawn.X, (int)OverworldSpawn.Y);
    }

    protected override void CreateDirectDrawings()
    {
        CreateHealthBars();
        CreateScreenUi();
        EnterTitleMode();
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

        // WinFormsGameHost initializes XInput before Engine.Initialize(). The current
        // Engine.Initialize signature assigns its optional gamepad argument afterward,
        // so reattaching here preserves the host's intended XInput manager.
        Engine.InitializeXInputGamepadManager();

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

    private static Keys[] MonitoredKeys =>
    [
        Keys.W,
        Keys.A,
        Keys.S,
        Keys.D,
        Keys.Up,
        Keys.Down,
        Keys.Left,
        Keys.Right,
        Keys.Space,
        Keys.E,
        Keys.Enter,
        Keys.I,
        Keys.H,
        Keys.P,
        Keys.N,
        Keys.L,
        Keys.R,
        Keys.F5,
        Keys.F9
    ];

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (!Enum.TryParse(args.KeyConfig.Key, ignoreCase: true, out Keys key))
            return;

        switch (args.KeyAction)
        {
            case KeyAction.Pressed:
                if (_keysDown.Add(key))
                    HandlePressedKey(key);
                break;

            case KeyAction.Released:
                _keysDown.Remove(key);
                break;
        }
    }

    private void HandlePressedKey(Keys key)
    {
        switch (_mode)
        {
            case GameMode.Title:
                if (key is Keys.Enter or Keys.N)
                    StartNewGame();
                else if (key is Keys.L or Keys.F9)
                    TryLoadGame();
                return;

            case GameMode.Dialogue:
                if (key is Keys.E or Keys.Enter or Keys.Space)
                    AdvanceDialogue();
                return;

            case GameMode.Inventory:
                if (key is Keys.I or Keys.Enter)
                    CloseInventory();
                else if (key == Keys.H)
                    UsePotion();
                return;

            case GameMode.Paused:
                if (key is Keys.P or Keys.Enter)
                    ResumeGame();
                return;

            case GameMode.GameOver:
            case GameMode.Victory:
                if (key is Keys.Enter or Keys.R or Keys.N)
                    EnterTitleMode();
                else if (key is Keys.L or Keys.F9)
                    TryLoadGame();
                return;
        }

        if (_mode != GameMode.Playing)
            return;

        switch (key)
        {
            case Keys.Space:
                BeginSwordAttack();
                break;
            case Keys.E:
            case Keys.Enter:
                Interact();
                break;
            case Keys.I:
                OpenInventory();
                break;
            case Keys.H:
                UsePotion();
                break;
            case Keys.P:
                PauseGame();
                break;
            case Keys.F5:
                TrySaveGame();
                break;
            case Keys.F9:
                TryLoadGame();
                break;
        }
    }

    private void BeforeBackgroundTasksExecute()
    {
        long tick = HighResTimer.GetCurrentTick();
        _frameDelta = Math.Clamp(HighResTimer.GetDuration(_lastUpdateTick, tick), 0f, 0.05f);
        _lastUpdateTick = tick;

        PollGamepad();
        UpdateTimers(_frameDelta);

        if (_mode != GameMode.Playing || _frameDelta <= 0f)
        {
            StopActorMovement();
            return;
        }

        UpdatePlayerMovement();
        UpdateSword();
        UpdateEnemies();
    }

    private void AfterBackgroundTasksExecute()
    {
        if (_mode != GameMode.Playing)
            return;

        CollectPickups();
        ResolveSwordHits();
        ResolveEnemyContact();
        UpdateMessageVisibility();
        UpdateHud();
    }

    private void PollGamepad()
    {
        var adapter = Engine.Input.GamepadManager?.ConnectedAdapters.FirstOrDefault();
        var currentButtons = adapter?.PressedButtons is { } buttons
            ? new HashSet<string>(buttons, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        foreach (string button in currentButtons)
        {
            if (!_gamepadButtonsDown.Contains(button))
                HandleGamepadPressed(button);
        }

        _gamepadButtonsDown.Clear();
        _gamepadButtonsDown.UnionWith(currentButtons);
    }

    private void HandleGamepadPressed(string button)
    {
        switch (_mode)
        {
            case GameMode.Title:
                if (button is "A" or "Start")
                    StartNewGame();
                else if (button == "Y")
                    TryLoadGame();
                return;

            case GameMode.Dialogue:
                if (button is "A" or "B")
                    AdvanceDialogue();
                return;

            case GameMode.Inventory:
                if (button is "Y" or "Start")
                    CloseInventory();
                else if (button is "A" or "B")
                    UsePotion();
                return;

            case GameMode.Paused:
                if (button is "Start" or "A")
                    ResumeGame();
                return;

            case GameMode.GameOver:
            case GameMode.Victory:
                if (button is "A" or "Start")
                    EnterTitleMode();
                else if (button == "Y")
                    TryLoadGame();
                return;
        }

        if (_mode != GameMode.Playing)
            return;

        switch (button)
        {
            case "X":
                BeginSwordAttack();
                break;
            case "A":
                Interact();
                break;
            case "Y":
                OpenInventory();
                break;
            case "B":
                UsePotion();
                break;
            case "Start":
                PauseGame();
                break;
            case "LeftShoulder":
                TrySaveGame();
                break;
            case "RightShoulder":
                TryLoadGame();
                break;
        }
    }

    private void UpdateTimers(float dt)
    {
        _damageCooldown = Math.Max(0f, _damageCooldown - dt);
        _swordTimer = Math.Max(0f, _swordTimer - dt);

        if (_swordTimer <= 0f && _sword.Visible)
        {
            _sword.Visible = false;
            _swingHitIds.Clear();
        }
    }

    private void StopActorMovement()
    {
        if (_player is not null)
            _player.Movement.SetVelocity(Vector2.Zero);

        foreach (EnemyState enemy in _enemies)
            enemy.Sprite.Movement.SetVelocity(Vector2.Zero);
    }

    private void CreateHealthBars()
    {
        _playerHealthBar = new GameHealthBar(
            RenderSurface.Host,
            _player,
            maximum: PlayerMaximumHealth,
            size: new Size(64, 9),
            nickname: "player-health");
        _playerHealthBar.SetFillColor(Color.FromArgb(245, 61, 210, 97));
        _playerHealthBar.SetZOrder(200);
        _playerHealthBar.Show();

        foreach (EnemyState enemy in _enemies)
        {
            var size = enemy.IsBoss ? new Size(88, 10) : new Size(48, 8);
            var bar = new GameHealthBar(
                RenderSurface.Host,
                enemy.Sprite,
                maximum: enemy.MaximumHealth,
                size: size,
                nickname: $"{enemy.Id}-health");

            bar.SetFillColor(
                enemy.IsBoss
                    ? Color.FromArgb(245, 194, 51, 64)
                    : Color.FromArgb(245, 232, 126, 67));
            bar.SetZOrder(200);
            bar.Show();
            enemy.HealthBar = bar;
        }
    }

    private void CreateScreenUi()
    {
        var host = RenderSurface.Host;
        var view = host.ViewManager.Views[0];

        _hudPanel = new DirectRectangle(
                Color.FromArgb(218, 24, 31, 35),
                host,
                view,
                new Rectangle(12, 12, 650, 66),
                "hud-panel")
            .SetFilled(true)
            .SetBorderColor(Color.FromArgb(235, 219, 198, 133))
            .SetStrokeWidth(2f)
            .SetCornerRadius(8f);
        _hudPanel.ZOrder = 1000;

        _hudText = new TextBlock(
                host,
                view,
                new Rectangle(26, 20, 624, 50),
                "hud-text")
            .SetFont(SKTypeface.Default, 17f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false)
            .UseShadow();
        _hudText.ZOrder = 1001;

        _messagePanel = new DirectRectangle(
                Color.FromArgb(232, 24, 27, 31),
                host,
                view,
                new Rectangle(100, 480, 760, 130),
                "message-panel")
            .SetFilled(true)
            .SetBorderColor(Color.FromArgb(240, 219, 198, 133))
            .SetStrokeWidth(3f)
            .SetCornerRadius(10f);
        _messagePanel.ZOrder = 1100;

        _messageText = new TextBlock(
                host,
                view,
                new Rectangle(122, 495, 716, 100),
                "message-text")
            .SetFont(SKTypeface.Default, 22f, minSize: 17f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center)
            .SetMaxLines(3)
            .UseShadow();
        _messageText.HorizontalPadding = 8f;
        _messageText.ZOrder = 1101;

        _inventoryPanel = new DirectRectangle(
                Color.FromArgb(244, 24, 27, 31),
                host,
                view,
                new Rectangle(235, 115, 490, 410),
                "inventory-panel")
            .SetFilled(true)
            .SetBorderColor(Color.FromArgb(240, 219, 198, 133))
            .SetStrokeWidth(3f)
            .SetCornerRadius(12f);
        _inventoryPanel.ZOrder = 1200;

        _inventoryText = new TextBlock(
                host,
                view,
                new Rectangle(265, 145, 430, 350),
                "inventory-text")
            .SetFont(SKTypeface.Default, 24f, minSize: 18f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Top)
            .SetMaxLines(12)
            .UseShadow();
        _inventoryText.ZOrder = 1201;

        _titlePanel = new DirectRectangle(
                Color.FromArgb(246, 17, 30, 25),
                host,
                view,
                new Rectangle(0, 0, GameWindow.GameSize.Width, GameWindow.GameSize.Height),
                "title-panel")
            .SetFilled(true)
            .SetStrokeWidth(0f);
        _titlePanel.ZOrder = 2000;

        _titleText = new TextBlock(
                host,
                view,
                new Rectangle(90, 105, 780, 185),
                "title-text")
            .SetFont(SKTypeface.Default, 52f, minSize: 30f)
            .SetColors(new SKColor(238, 218, 143), SKColors.Transparent)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .SetMaxLines(3)
            .UseShadow()
            .UseOutline();
        _titleText.ZOrder = 2001;

        _titleOptionsText = new TextBlock(
                host,
                view,
                new Rectangle(135, 310, 690, 240),
                "title-options")
            .SetFont(SKTypeface.Default, 23f, minSize: 18f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .SetMaxLines(9)
            .UseShadow();
        _titleOptionsText.ZOrder = 2002;

        _pausePanel = new DirectRectangle(
                Color.FromArgb(224, 12, 15, 18),
                host,
                view,
                new Rectangle(250, 200, 460, 240),
                "pause-panel")
            .SetFilled(true)
            .SetBorderColor(Color.FromArgb(240, 219, 198, 133))
            .SetStrokeWidth(3f)
            .SetCornerRadius(12f);
        _pausePanel.ZOrder = 1500;

        _pauseText = new TextBlock(
                host,
                view,
                new Rectangle(275, 225, 410, 190),
                "pause-text")
            .SetText("PAUSED\n\nP / Enter / Start to continue")
            .SetFont(SKTypeface.Default, 32f, minSize: 20f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .SetMaxLines(5)
            .UseShadow();
        _pauseText.ZOrder = 1501;

        SetMessageVisible(false);
        SetInventoryVisible(false);
        SetPauseVisible(false);
        UpdateHud(force: true);
    }
}
