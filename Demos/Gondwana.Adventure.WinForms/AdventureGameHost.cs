using Gondwana.Scenes;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;

namespace Gondwana.Demos.Adventure;

internal sealed class AdventureGameHost : WinFormsGpuGameHost
{
    private readonly AdventureRuntime _game;

    internal AdventureGameHost(WinFormGpuRenderSurfaceControl surface) : base(surface)
        => _game = new AdventureRuntime(surface.Host);

    protected override void LoadAssets() => _game.LoadAssets();
    protected override Scene CreateInitialScene() => _game.CreateScene();
    protected override void OnSceneBound() => _game.OnSceneBound(Scene!);
    protected override void CreateDirectDrawings() => _game.CreateDirectDrawings();

    protected override void OnKeyboardAdapterInitialized() =>
        _game.HookKeyboard(
            Engine.Input.KeyboardEventPoller!,
            ((int)Keys.Left, "left"),
            ((int)Keys.Right, "right"),
            ((int)Keys.A, "a"),
            ((int)Keys.D, "d"),
            ((int)Keys.Up, "up"),
            ((int)Keys.W, "w"),
            ((int)Keys.Down, "down"),
            ((int)Keys.S, "s"),
            ((int)Keys.M, "m"),
            ((int)Keys.V, "v"),
            ((int)Keys.Space, "space"),
            ((int)Keys.P, "p"),
            ((int)Keys.R, "r"));

    protected override void OnEngineInitialized()
    {
        Engine.Configuration.TargetFPS = 60;
        Engine.Configuration.VSync = true;
        _game.Start();
    }

    protected override void UnhookEvents() => _game.UnhookEvents();
}
