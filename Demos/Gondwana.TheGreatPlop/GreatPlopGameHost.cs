using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Scenes;
using Gondwana.Timers;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;

namespace Gondwana.Demos.TheGreatPlop;

internal sealed class GreatPlopGameHost : WinFormsGpuGameHost
{
    private static readonly Keys[] MonitoredKeys = [Keys.W, Keys.A, Keys.S, Keys.D, Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Space, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.R];
    private readonly HashSet<Keys> _keys = [];
    private readonly PlopState _game = new();
    private MeadowDrawing _drawing = null!;
    private long _lastTick;

    internal GreatPlopGameHost(WinFormGpuRenderSurfaceControl surface) : base(surface) { }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();
        scene.AddLayer(20, 10, 64, 64, 0, 1f, CoordinateSystemTypes.Orthogonal);
        return scene;
    }

    protected override void OnSceneBound()
    {
        RenderSurface.Host.Backbuffer.ClearColor = new SKColor(30, 72, 50);
        var view = RenderSurface.Host.ViewManager.Views[0];
        view.Camera.WorldBoundsPx = Scene!.GetWorldBoundsPx();
        view.Camera.SnapTo(PointF.Empty);
    }

    protected override void CreateDirectDrawings()
    {
        var view = RenderSurface.Host.ViewManager.Views[0];
        _drawing = new MeadowDrawing(RenderSurface.Host, view, new Rectangle(0, 0, 1280, 720), _game)
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
        if (!Enum.TryParse(args.KeyConfig.Key, true, out Keys key)) return;
        if (args.KeyAction == KeyAction.Released) _keys.Remove(key); else _keys.Add(key);
        if (args.KeyAction == KeyAction.Pressed)
        {
            if (key is >= Keys.D1 and <= Keys.D6) _game.TryBuy((int)key - (int)Keys.D1);
            if (key == Keys.R) _game.Reset();
        }
    }

    private void OnMouse(Gondwana.Input.Mouse.MouseEventArgs args)
    {
        _game.Mouse = args.CurrentPosition;
        bool overButton = MeadowDrawing.PlopButton.Contains(args.CurrentPosition);
        if (args.LeftButtonJustPressed && overButton) _game.StartCharge();
        if (args.LeftButtonJustReleased && _game.Charging) _game.ReleaseCharge();
    }

    private void Update()
    {
        long tick = HighResTimer.GetCurrentTick();
        float dt = Math.Clamp(HighResTimer.GetDuration(_lastTick, tick), 0f, 0.05f);
        _lastTick = tick;
        if (dt <= 0f) return;

        Vector2 direction = new(
            Axis(Keys.A, Keys.Left, Keys.D, Keys.Right),
            Axis(Keys.W, Keys.Up, Keys.S, Keys.Down));
        bool charging = _game.Charging || _keys.Contains(Keys.Space);
        if (_keys.Contains(Keys.Space) && !_game.Charging) _game.StartCharge();
        if (!_keys.Contains(Keys.Space) && _game.KeyboardCharging) _game.ReleaseCharge();
        _game.KeyboardCharging = _keys.Contains(Keys.Space);
        _game.Update(dt, direction, charging);
        _drawing.Invalidate();
    }

    private float Axis(Keys negative, Keys negativeAlt, Keys positive, Keys positiveAlt)
    {
        bool n = _keys.Contains(negative) || _keys.Contains(negativeAlt);
        bool p = _keys.Contains(positive) || _keys.Contains(positiveAlt);
        return n == p ? 0f : n ? -1f : 1f;
    }
}

internal sealed class PlopState
{
    internal const float FieldWidth = 1280f;
    internal const float FieldHeight = 560f;
    internal readonly List<Plop> Plops = [];
    internal Vector2 Cow = new(510, 270);
    internal Point Mouse;
    internal float Pressure;
    internal float Money = 18f;
    internal float TimeOfDay = 7.2f;
    internal float Stun;
    internal float Shake;
    internal float MessageTime;
    internal string Message = "Hold the big button. Release before BLOWOUT!";
    internal bool Charging;
    internal bool KeyboardCharging;
    internal bool Alfalfa;
    internal bool Chili;
    internal bool Plutonium;
    internal bool DualCore;
    internal bool Compressor;
    internal bool Beetle;
    private readonly Random _random = new(8128);
    private float _automationTimer;

    internal void Reset()
    {
        Plops.Clear(); Cow = new(510, 270); Pressure = 0; Money = 18; Stun = 0; Shake = 0;
        Alfalfa = Chili = Plutonium = DualCore = Compressor = Beetle = false;
        Message = "Pasture reset. The cow remembers nothing."; MessageTime = 3;
    }

    internal void StartCharge()
    {
        if (Stun > 0 || Charging) return;
        Charging = true;
        Pressure = Math.Max(Pressure, .02f);
    }

    internal void ReleaseCharge()
    {
        if (!Charging) return;
        Charging = false;
        Drop(Math.Max(.06f, Pressure));
        Pressure = 0;
    }

    internal void Update(float dt, Vector2 input, bool charging)
    {
        TimeOfDay = (TimeOfDay + dt * .12f) % 24f;
        Stun = Math.Max(0, Stun - dt);
        Shake = Math.Max(0, Shake - dt * 2.4f);
        MessageTime = Math.Max(0, MessageTime - dt);

        if (charging && Charging)
        {
            Pressure += dt * (Alfalfa ? .34f : .24f);
            if (Pressure > 1.13f)
            {
                Charging = false; Pressure = 0; Stun = 3; Shake = 1;
                Message = "CATASTROPHIC BLOWOUT — dignity rebooting..."; MessageTime = 3;
            }
        }

        if (Stun <= 0 && !Charging && input.LengthSquared() > 0)
        {
            input = Vector2.Normalize(input);
            Cow += input * 165f * dt;
            Cow.X = Math.Clamp(Cow.X, 55, 1195);
            Cow.Y = Math.Clamp(Cow.Y, 80, 505);
        }

        foreach (Plop plop in Plops)
        {
            Vector2 delta = plop.Position - Cow;
            float range = plop.Radius + 45;
            if (delta.LengthSquared() < range * range && delta.LengthSquared() > .1f && !Charging)
                plop.Velocity += Vector2.Normalize(delta) * 280f * dt / MathF.Max(.7f, plop.Radius / 55f);
            plop.Position += plop.Velocity * dt;
            plop.Velocity *= MathF.Pow(.13f, dt);
            plop.Squash = MathF.Sin(plop.Age * 8f) * MathF.Exp(-plop.Age * 1.5f) * .18f;
            plop.Age += dt;
        }

        if (Beetle)
        {
            _automationTimer += dt;
            if (_automationTimer > .6f)
            {
                _automationTimer = 0;
                Plop? target = Plops.OrderBy(p => Vector2.DistanceSquared(p.Position, new(110, 175))).FirstOrDefault();
                if (target is not null) target.Velocity += Vector2.Normalize(new Vector2(110, 175) - target.Position) * 120f;
            }
        }

        var sold = Plops.Where(p => p.Position.X < 155 && p.Position.Y < 245).ToList();
        if (sold.Count > 0)
        {
            float combo = 1f + Math.Max(0, sold.Count - 1) * .75f;
            float earned = sold.Sum(p => p.Value) * combo;
            Money += earned;
            foreach (Plop p in sold) Plops.Remove(p);
            Message = sold.Count > 1 ? $"BEETLE COMBO x{combo:0.00}!  +${earned:0}" : $"SOLD!  +${earned:0}";
            MessageTime = 2.2f;
        }
    }

    private void Drop(float pressure)
    {
        int count = DualCore ? 2 : 1;
        for (int i = 0; i < count; i++)
        {
            bool flaming = Chili && _random.NextDouble() < .25;
            float radius = 14 + pressure * pressure * 118;
            if (Compressor) radius *= .65f;
            float value = MathF.Round(1 + pressure * pressure * 235) * (flaming ? 2 : 1) * (Plutonium ? 3 : 1);
            Plops.Add(new Plop
            {
                Position = Cow + new Vector2(-18 + i * 38, 32), Radius = radius,
                Value = value, Flaming = flaming, Radioactive = Plutonium,
                Velocity = new Vector2((i == 0 ? -1 : 1) * 18, 35)
            });
        }
        Shake = Math.Clamp(pressure, .15f, 1f);
        Message = pressure >= .82f ? "TITANIC PLOP! Roll that monument to market!" : $"Fresh inventory: ${MathF.Round(1 + pressure * pressure * 235):0}";
        MessageTime = 2.4f;
    }

    internal void TryBuy(int slot)
    {
        (float cost, bool owned, string name) item = slot switch
        {
            0 => (45, Alfalfa, "High-Fiber Alfalfa"), 1 => (90, Chili, "Volcanic Chili Beans"),
            2 => (160, Plutonium, "Plutonium Sludge"), 3 => (240, DualCore, "Dual Chamber"),
            4 => (350, Compressor, "Compressor"), _ => (600, Beetle, "Beetle Assistant")
        };
        if (item.owned) { Message = $"{item.name} already installed."; MessageTime = 1.5f; return; }
        if (Money < item.cost) { Message = $"Need ${item.cost:0} for {item.name}."; MessageTime = 1.5f; return; }
        Money -= item.cost;
        switch (slot) { case 0: Alfalfa = true; break; case 1: Chili = true; break; case 2: Plutonium = true; break; case 3: DualCore = true; break; case 4: Compressor = true; break; case 5: Beetle = true; break; }
        Message = $"UPGRADE ACQUIRED: {item.name}!"; MessageTime = 2;
    }
}

internal sealed class Plop
{
    internal Vector2 Position;
    internal Vector2 Velocity;
    internal float Radius;
    internal float Value;
    internal float Age;
    internal float Squash;
    internal bool Flaming;
    internal bool Radioactive;
}
