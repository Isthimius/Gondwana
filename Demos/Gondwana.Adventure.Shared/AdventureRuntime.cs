using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Gondwana.Timers;
using SkiaSharp;

namespace Gondwana.Demos.Adventure;

/// <summary>Shared engine lifecycle, game state, rendering and input actions.</summary>
internal sealed class AdventureRuntime(RenderSurfaceHostBase surface)
{
    private readonly AdventureGame _game = new();
    private readonly AdventureAudio _audio = new();
    private readonly HashSet<string> _pressed = new(StringComparer.Ordinal);
    private KeyboardEventPoller? _keyboard;
    private AdventureDrawing? _drawing;
    private SKTypeface? _font;
    private TextBlock? _hud;
    private TextBlock? _message;
    private string _lastHud = "";
    private string _lastMessage = "";
    private long _lastTick;

    private static Engine Engine => Gondwana.Engine.Instance;

    internal void LoadAssets()
    {
        _audio.Load();
        _game.SoundRequested += _audio.Play;
    }

    internal Scene CreateScene()
    {
        var scene = new Scene();
        scene.AddLayer(AdventureWorld.Width / 40, (AdventureWorld.Height + 39) / 40, 40, 40, 0, 1f, CoordinateSystemTypes.Orthogonal);
        return scene;
    }

    internal void OnSceneBound(Scene scene)
    {
        surface.Backbuffer.ClearColor = SKColors.Black;
        var camera = surface.ViewManager.Views[0].Camera;
        camera.WorldBoundsPx = scene.GetWorldBoundsPx();
        camera.SnapTo(PointF.Empty);
    }

    internal void CreateDirectDrawings()
    {
        using var stream = typeof(AdventureRuntime).Assembly.GetManifestResourceStream(
            "Gondwana.Demos.Adventure.Fonts.ArchitectsDaughter-Regular.ttf")
            ?? throw new InvalidOperationException("Missing embedded Adventure font.");
        var font = SKTypeface.FromStream(stream)
            ?? throw new InvalidOperationException("Failed to decode Adventure font.");
        _font = font;
        var view = surface.ViewManager.Views[0];
        _drawing = new AdventureDrawing(surface, view, _game);
        _hud = new TextBlock(surface, view, new Rectangle(12, AdventureWorld.Floor + 6, AdventureWorld.Width - 24, 48), "adventure-hud")
            .SetFont(font, 23f, 16f)
            .SetColors(SKColors.White, SKColors.Black)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center);
        _hud.ZOrder = 10;
        _message = new TextBlock(surface, view, new Rectangle(80, 220, AdventureWorld.Width - 160, 255), "adventure-message")
            .SetFont(font, 32f, 20f)
            .SetColors(SKColors.White, new SKColor(12, 12, 20, 245))
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .SetMaxLines(7);
        _message.ZOrder = 20;
        Refresh();
    }

    internal void HookKeyboard(KeyboardEventPoller keyboard, params (int Code, string Name)[] bindings)
    {
        _keyboard = keyboard;
        keyboard.KeyDown += HandleKey;

        foreach (var (code, name) in bindings)
            keyboard.StartMonitoringKey(code, name);
    }

    private void HandleKey(KeyDownEventArgs args)
    {
        string key = args.KeyConfig.Key;

        if (args.KeyAction == KeyAction.Released)
        {
            _pressed.Remove(key);
            return;
        }

        if (args.KeyAction != KeyAction.Pressed || !_pressed.Add(key))
            return;

        switch (key)
        {
            case "space":
                if (_game.State == AdventureGame.Phase.Playing)
                    _game.Drop();
                else
                    _game.Start();

                break;
            case "p":
                _game.Pause();
                break;
            case "r":
                _game.Reset(_game.Randomized);
                break;
            case "m":
                _audio.Muted = !_audio.Muted;
                break;
            case "v":
                if (_game.State == AdventureGame.Phase.Title)
                    _game.Reset(!_game.Randomized);

                break;
        }
    }

    internal void Start()
    {
        _lastTick = HighResTimer.GetCurrentTick();
        Engine.BeforeBackgroundTasksExecute += Update;
    }

    private void Update()
    {
        long now = HighResTimer.GetCurrentTick();
        float dt = Math.Clamp(HighResTimer.GetDuration(_lastTick, now), 0f, 0.05f);
        _lastTick = now;
        float x = (Down("right", "d") ? 1f : 0f) - (Down("left", "a") ? 1f : 0f);
        float y = (Down("down", "s") ? 1f : 0f) - (Down("up", "w") ? 1f : 0f);
        _game.Update(dt, new Vector2(x, y));
        Refresh();
    }

    private bool Down(string first, string second) => _pressed.Contains(first) || _pressed.Contains(second);

    private void Refresh()
    {
        _drawing?.Invalidate();
        string audio = !_audio.Available ? "AUDIO UNAVAILABLE" : _audio.Muted ? "MUTED" : "SOUND ON";
        string held = _game.Held?.Kind.ToString() ?? "nothing";
        string hud = $"{AdventureWorld.GetRoom(_game.RoomId).Name}     Carrying: {held}     {audio}";

        if (_hud is not null && hud != _lastHud)
        {
            _hud.SetText(hud);
            _lastHud = hud;
        }

        string variant = _game.Randomized ? "GAME 3 - RANDOMIZED" : "GAME 2 - CLASSIC QUEST";
        string message = _game.State switch
        {
            AdventureGame.Phase.Title => $"ADVENTURE\n{variant}\nRecover the chalice and return it to the gold castle.\nArrow keys / WASD: move    SPACE: start or drop\nV: change variant    P: pause    M: mute",
            AdventureGame.Phase.Paused => "PAUSED\nP TO RESUME",
            AdventureGame.Phase.Dead => "THE DRAGON GOT YOU!\nSPACE TO CONTINUE",
            AdventureGame.Phase.Won => "THE CHALICE IS HOME!\nSPACE FOR A NEW QUEST",
            _ => ""
        };

        if (_message is not null && message != _lastMessage)
        {
            _message.SetText(message);
            _message.Visible = message.Length != 0;
            _lastMessage = message;
        }
    }

    internal void UnhookEvents()
    {
        Engine.BeforeBackgroundTasksExecute -= Update;

        if (_keyboard is not null)
            _keyboard.KeyDown -= HandleKey;

        _keyboard = null;
        _pressed.Clear();
        _game.SoundRequested -= _audio.Play;
        _audio.Dispose();
        _drawing?.Dispose();
        _hud?.Dispose();
        _message?.Dispose();
        _font?.Dispose();
    }
}
