using Avalonia.Input;
using Gondwana.Avalonia.Hosting;
using Gondwana.Avalonia.Rendering;
using Gondwana.Scenes;

namespace Gondwana.Demos.Asteroids;

internal sealed class AsteroidsGameHost : AvaloniaGpuGameHost
{
    private readonly AsteroidsRuntime _game;

    internal AsteroidsGameHost(AvaloniaGpuRenderSurfaceControl surface) : base(surface)
        => _game = new AsteroidsRuntime(surface.Host);

    protected override void OnConfigurePlatform()
    {
        base.OnConfigurePlatform();
#if WINDOWS
        Engine.UseNAudio();
#endif
    }

    protected override void LoadAssets() => _game.LoadAssets();
    protected override void LoadTilesheets() => _game.LoadTilesheets();
    protected override Scene CreateInitialScene() => _game.CreateScene();
    protected override void OnSceneBound() => _game.OnSceneBound(Scene!);
    protected override void CreateDirectDrawings() => _game.CreateDirectDrawings();

    protected override void OnKeyboardAdapterInitialized() =>
        _game.HookKeyboard(
            Engine.Input.KeyboardEventPoller!,
            ((int)Key.Left, "left"),
            ((int)Key.Right, "right"),
            ((int)Key.A, "a"),
            ((int)Key.D, "d"),
            ((int)Key.Up, "up"),
            ((int)Key.W, "w"),
            ((int)Key.H, "h"),
            ((int)Key.Space, "space"),
            ((int)Key.M, "m"),
            ((int)Key.P, "p"),
            ((int)Key.R, "r"));

    protected override void OnEngineInitialized()
    {
        Engine.Configuration.TargetFPS = 60;
        _game.Start();
    }

    protected override void UnhookEvents() => _game.UnhookEvents();
}
