using Gondwana.Scenes;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;

namespace Gondwana.Demos.Breakout;

internal sealed class BreakoutGameHost : WinFormsGpuGameHost
{
    private readonly BreakoutRuntime _game;

    internal BreakoutGameHost(WinFormGpuRenderSurfaceControl surface) : base(surface)
        => _game = new BreakoutRuntime(surface.Host);

    protected override void LoadTilesheets() => _game.LoadTilesheets();
    protected override Scene CreateInitialScene() => _game.CreateScene();
    protected override void OnSceneBound() => _game.OnSceneBound(Scene!);
    protected override void CreateSprites() => _game.CreateSprites();
    protected override void CreateDirectDrawings() => _game.CreateDirectDrawings();

    protected override void OnKeyboardAdapterInitialized() =>
        _game.HookKeyboard(Engine.Input.KeyboardEventPoller!,
            ((int)Keys.Left, "left"), ((int)Keys.Right, "right"),
            ((int)Keys.A, "a"), ((int)Keys.D, "d"),
            ((int)Keys.Space, "space"), ((int)Keys.P, "p"), ((int)Keys.R, "r"));

    protected override void OnEngineInitialized()
    {
        Engine.Configuration.RenderScale = 1.5f;
        Engine.Configuration.TargetFPS = 60;
        Engine.Configuration.VSync = true;
        _game.Start();
    }

    protected override void UnhookEvents() => _game.UnhookEvents();
}
