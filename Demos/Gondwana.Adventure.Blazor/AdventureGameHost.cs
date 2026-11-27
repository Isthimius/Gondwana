using Gondwana.Blazor.Hosting;
using Gondwana.Blazor.Input.Keyboard;
using Gondwana.Blazor.Rendering;
using Gondwana.Scenes;
using Microsoft.JSInterop;

namespace Gondwana.Demos.Adventure;

internal sealed class AdventureGameHost : BlazorGpuGameHost
{
    private readonly AdventureRuntime _game;

    internal AdventureGameHost(BlazorGpuRenderSurfaceComponent surface, IJSRuntime jsRuntime)
        : base(surface, jsRuntime) => _game = new AdventureRuntime(surface.Host);

    protected override void LoadAssets() => _game.LoadAssets();
    protected override Scene CreateInitialScene() => _game.CreateScene();
    protected override void OnSceneBound() => _game.OnSceneBound(Scene!);
    protected override void CreateDirectDrawings() => _game.CreateDirectDrawings();

    protected override void OnKeyboardAdapterInitialized() =>
        _game.HookKeyboard(
            Engine.Input.KeyboardEventPoller!,
            ((int)BlazorKey.ArrowLeft, "left"),
            ((int)BlazorKey.ArrowRight, "right"),
            ((int)BlazorKey.KeyA, "a"),
            ((int)BlazorKey.KeyD, "d"),
            ((int)BlazorKey.ArrowUp, "up"),
            ((int)BlazorKey.KeyW, "w"),
            ((int)BlazorKey.ArrowDown, "down"),
            ((int)BlazorKey.KeyS, "s"),
            ((int)BlazorKey.KeyM, "m"),
            ((int)BlazorKey.KeyV, "v"),
            ((int)BlazorKey.Space, "space"),
            ((int)BlazorKey.KeyP, "p"),
            ((int)BlazorKey.KeyR, "r"));

    protected override void OnEngineInitialized()
    {
        Engine.Configuration.TargetFPS = 60;
        _game.Start();
    }

    protected override void UnhookEvents() => _game.UnhookEvents();
}
