using Gondwana.Blazor.Hosting;
using Gondwana.Blazor.Input.Keyboard;
using Gondwana.Blazor.Rendering;
using Gondwana.Scenes;
using Microsoft.JSInterop;

namespace Gondwana.Demos.Breakout;

internal sealed class BreakoutGameHost : BlazorGpuGameHost
{
    private readonly BreakoutRuntime _game;
    internal BreakoutGameHost(BlazorGpuRenderSurfaceComponent surface, IJSRuntime jsRuntime)
        : base(surface, jsRuntime) => _game = new BreakoutRuntime(surface.Host);

    protected override void LoadTilesheets() => _game.LoadTilesheets();
    protected override Scene CreateInitialScene() => _game.CreateScene();
    protected override void OnSceneBound() => _game.OnSceneBound(Scene!);
    protected override void CreateSprites() => _game.CreateSprites();
    protected override void CreateDirectDrawings() => _game.CreateDirectDrawings();

    protected override void OnKeyboardAdapterInitialized() =>
        _game.HookKeyboard(Engine.Input.KeyboardEventPoller!,
            ((int)BlazorKey.ArrowLeft, "left"), ((int)BlazorKey.ArrowRight, "right"),
            ((int)BlazorKey.KeyA, "a"), ((int)BlazorKey.KeyD, "d"),
            ((int)BlazorKey.Space, "space"), ((int)BlazorKey.KeyP, "p"), ((int)BlazorKey.KeyR, "r"));

    protected override void OnEngineInitialized()
    {
        Engine.Configuration.TargetFPS = 60;
        _game.Start();
    }

    protected override void UnhookEvents() => _game.UnhookEvents();
}
