using Avalonia.Input;
using Gondwana.Avalonia.Hosting;
using Gondwana.Avalonia.Rendering;
using Gondwana.Scenes;

namespace Gondwana.Demos.Breakout;

internal sealed class BreakoutGameHost : AvaloniaGpuGameHost
{
    private readonly BreakoutRuntime _game;

    internal BreakoutGameHost(AvaloniaGpuRenderSurfaceControl surface) : base(surface)
        => _game = new BreakoutRuntime(surface.Host);

    protected override void LoadTilesheets() => _game.LoadTilesheets();
    protected override Scene CreateInitialScene() => _game.CreateScene();
    protected override void OnSceneBound() => _game.OnSceneBound(Scene!);
    protected override void CreateSprites() => _game.CreateSprites();
    protected override void CreateDirectDrawings() => _game.CreateDirectDrawings();

    protected override void OnKeyboardAdapterInitialized() =>
        _game.HookKeyboard(Engine.Input.KeyboardEventPoller!,
            ((int)Key.Left, "left"), ((int)Key.Right, "right"),
            ((int)Key.A, "a"), ((int)Key.D, "d"),
            ((int)Key.Space, "space"), ((int)Key.P, "p"), ((int)Key.R, "r"));

    protected override void OnEngineInitialized()
    {
        Engine.Configuration.RenderScale = 1.5f;
        Engine.Configuration.TargetFPS = 60;
        _game.Start();
    }

    protected override void UnhookEvents() => _game.UnhookEvents();
}
