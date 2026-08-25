using Gondwana.Drawing.Coordinates;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Scenes;
using Gondwana.Timers;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;

namespace Gondwana.Demos.RageToPro;

internal sealed class RageToProGameHost : WinFormsGameHost
{
    private static readonly Keys[] MonitoredKeys = [Keys.Space, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.R];
    private readonly GamerState _game = new();
    private BedroomDrawing _drawing = null!;
    private long _lastTick;

    internal RageToProGameHost(WinFormBitmapRenderSurfaceControl surface) : base(surface) { }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();
        scene.AddLayer(20, 12, 64, 60, 0, 1f, CoordinateSystemTypes.Orthogonal);
        return scene;
    }

    protected override void OnSceneBound()
    {
        RenderSurface.Host.Backbuffer.ClearColor = new SKColor(18, 19, 29);
        var view = RenderSurface.Host.ViewManager.Views[0];
        view.Camera.WorldBoundsPx = Scene!.GetWorldBoundsPx();
        view.Camera.SnapTo(PointF.Empty);
    }

    protected override void CreateDirectDrawings()
    {
        var view = RenderSurface.Host.ViewManager.Views[0];
        _drawing = new BedroomDrawing(RenderSurface.Host, view, new Rectangle(0, 0, 1280, 720), _game)
        {
            ZOrder = 1000
        };
    }

    protected override void OnKeyboardAdapterInitialized()
    {
        var keyboard = Engine.Input.KeyboardEventPoller!;
        keyboard.KeyDown += OnKeyDown;
        foreach (Keys key in MonitoredKeys)
            keyboard.StartMonitoringKey((int)key, key.ToString());
    }

    protected override void OnMouseAdapterInitialized()
    {
        var mouse = Engine.Input.MouseEventPoller!;
        mouse.MouseEvent += OnMouse;
        mouse.StartMonitoringMouse();
    }

    protected override void OnEngineInitialized()
    {
        Engine.Configuration.TargetFPS = 60;
        _lastTick = HighResTimer.GetCurrentTick();
        Engine.BeforeBackgroundTasksExecute += Update;
    }

    protected override void UnhookEvents()
    {
        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;
        if (Engine.Input.MouseEventPoller is not null)
            Engine.Input.MouseEventPoller.MouseEvent -= OnMouse;
        Engine.BeforeBackgroundTasksExecute -= Update;
    }

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (args.KeyAction != KeyAction.Pressed || !Enum.TryParse(args.KeyConfig.Key, true, out Keys key)) return;
        if (key == Keys.Space) _game.TogglePlaying();
        if (key is >= Keys.D1 and <= Keys.D4) _game.TryBuy((int)key - (int)Keys.D1);
        if (key == Keys.R) _game.Reset();
    }

    private void OnMouse(Gondwana.Input.Mouse.MouseEventArgs args)
    {
        _game.Mouse = args.CurrentPosition;
        if (!args.LeftButtonJustPressed) return;
        if (BedroomDrawing.PlayButton.Contains(args.CurrentPosition)) _game.TogglePlaying();
        for (int i = 0; i < BedroomDrawing.ShopButtons.Length; i++)
            if (BedroomDrawing.ShopButtons[i].Contains(args.CurrentPosition)) _game.TryBuy(i);
    }

    private void Update()
    {
        long tick = HighResTimer.GetCurrentTick();
        float dt = Math.Clamp(HighResTimer.GetDuration(_lastTick, tick), 0f, .05f);
        _lastTick = tick;
        if (dt <= 0) return;
        _game.Update(dt);
        _drawing.Invalidate();
    }
}

internal sealed class GamerState
{
    internal static readonly string[] UpgradeNames = ["Less-awful RAM", "Mechanical Keyboard", "Real Gaming PC", "Pro Stream Rig"];
    internal static readonly int[] UpgradeCosts = [80, 180, 400, 900];
    internal static readonly string[] BrokenThings = ["keyboard", "mouse", "monitor", "desk lamp", "controller"];

    internal Point Mouse;
    internal float Money = 12;
    internal float Rage = 8;
    internal float TotalHours;
    internal float HourProgress;
    internal float MessageTime = 5;
    internal float SmashTime;
    internal int UpgradeLevel;
    internal int ThingsBroken;
    internal bool Playing;
    internal string Message = "The PC wheezes. SPACE or click PLAY to grind.";

    private readonly Random _random = new();

    internal float HourlyPay => 7 + UpgradeLevel * 3;
    internal float RageMultiplier => UpgradeLevel switch
    {
        0 => 1f,
        1 => .78f,
        2 => .55f,
        3 => .32f,
        _ => .14f
    };
    internal bool IsPro => UpgradeLevel == UpgradeNames.Length;

    internal void Reset()
    {
        Money = 12; Rage = 8; TotalHours = 0; HourProgress = 0; UpgradeLevel = 0;
        ThingsBroken = 0; Playing = false; SmashTime = 0;
        Message = "Fresh start. Same terrible computer."; MessageTime = 3;
    }

    internal void TogglePlaying()
    {
        if (SmashTime > 0) return;
        Playing = !Playing;
        Message = Playing ? "Queue popped. Money and questionable decisions incoming." : "Taking five. Rage is cooling down.";
        MessageTime = 2;
    }

    internal void Update(float dt)
    {
        MessageTime = Math.Max(0, MessageTime - dt);
        SmashTime = Math.Max(0, SmashTime - dt);

        if (!Playing)
        {
            Rage = Math.Max(0, Rage - dt * (2.4f + UpgradeLevel * .35f));
            return;
        }

        HourProgress += dt;
        while (HourProgress >= 1f)
        {
            HourProgress -= 1f;
            CompleteHour();
        }
    }

    private void CompleteHour()
    {
        TotalHours++;
        Money += HourlyPay;

        if (_random.NextDouble() < .68)
        {
            float increase = (4f + (float)_random.NextDouble() * 8f) * RageMultiplier;
            Rage = Math.Min(100, Rage + increase);
            Message = $"Lag spike! +${HourlyPay:0}, rage +{increase:0.0}";
        }
        else
        {
            Rage = Math.Max(0, Rage - 1.4f);
            Message = $"Clean win! +${HourlyPay:0}";
        }
        MessageTime = 1.15f;

        if (Rage >= 100) SmashSomething();
    }

    private void SmashSomething()
    {
        string item = BrokenThings[_random.Next(BrokenThings.Length)];
        int bill = 35 + _random.Next(5, 16) * 5;
        Money = Math.Max(0, Money - bill);
        Rage = 28;
        Playing = false;
        ThingsBroken++;
        SmashTime = 2.4f;
        Message = $"RAGE QUIT! You broke the {item}. Repair bill: ${bill}.";
        MessageTime = 4;
    }

    internal void TryBuy(int slot)
    {
        if (slot != UpgradeLevel)
        {
            Message = slot < UpgradeLevel ? "Already installed." : "Buy the upgrades in order.";
            MessageTime = 2; return;
        }

        int cost = UpgradeCosts[slot];
        if (Money < cost)
        {
            Message = $"Need ${cost - Money:0} more for {UpgradeNames[slot]}.";
            MessageTime = 2; return;
        }

        Money -= cost;
        UpgradeLevel++;
        Rage = Math.Max(0, Rage - 22);
        Message = IsPro ? "YOU WENT PRO! The setup hums. Rage is nearly obsolete." : $"Installed {UpgradeNames[slot]} — rage gain reduced!";
        MessageTime = 4;
    }
}
