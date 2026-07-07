using Gondwana.Blazor.Hosting;
using Gondwana.Blazor.Rendering;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using Microsoft.JSInterop;

namespace MyGame;

/// <summary>
/// The main game host for MyGame. Override the methods below to load assets,
/// build the scene graph, and wire up input.
/// </summary>
/// <remarks>
/// Full lifecycle documentation:
/// <see href="https://github.com/Isthimius/Gondwana/wiki">Gondwana Wiki</see>
/// and the 15-minute guide at
/// <see href="https://github.com/Isthimius/Gondwana/wiki/Make-Your-First-Game-in-15-Minutes">Make Your First Game in 15 Minutes</see>.
/// </remarks>
internal sealed class MyGameHost : BlazorGameHost
{
    internal MyGameHost(BlazorBitmapRenderSurfaceComponent renderSurface, IJSRuntime jsRuntime)
        : base(renderSurface, jsRuntime)
    {
    }

    #region GameHostBase overrides

    // TODO: Run pre-initialization setup here, such as choosing config paths,
    // toggling feature flags, or preparing services before assets begin loading.
    protected override void OnInitializing()
    {
    }

    // TODO: Load non-tilesheet assets such as audio, fonts, and data files.
    protected override void LoadAssets()
    {
        // TODO: Load your tilesheets/sprites here.
        // Example:
        //   var sheet = new Tilesheet("hero", @"assets\hero.png", 32, 32);
        //   TilesheetRegistry.Instance.Register(sheet);

        // TODO: Load audio files here.
        // Example (browser):
        //   var audioManager = Engine.GetBrowserAudioManager();
        //   var theme = audioManager.Load("theme", "assets/theme.mp3");
        //   theme.IsLooping = true;
        //   theme.Play();

        // TODO: Load fonts here.
        // Example:
        //   var font = Engine.Managers.Fonts.LoadFromFile("main", @"assets\font.ttf");
    }

    // TODO: Load tilesheet definitions after other assets are available.
    protected override void LoadTilesheets()
    {
    }

    // TODO: Load animation cycle definitions after tilesheets are available.
    protected override void LoadAnimationCycles()
    {
    }

    // TODO: Run after the initial scene and views are created, but before the scene is bound.
    protected override void OnSceneGraphCreated()
    {
    }

    // TODO: Build and return the initial Scene.
    protected override Scene CreateInitialScene()
    {
        return Scene.Empty;
    }

    // TODO: Create initial camera/view objects after the Scene has been created.
    protected override void CreateInitialViews()
    {
    }

    // Note: OnSceneBound() is invoked by GameHostBase after BindScene() completes.
    // Override OnSceneBound() if you need post-bind setup before sprites/direct drawings are created.

    // TODO: Called after assets are loaded. Build your scene layers and add sprites here.
    protected override void CreateSprites()
    {
        // TODO: Create one or more SceneLayers.
        // Example:
        //   var layer = new SceneLayer(RenderSurface.Host, 32, 32, 24, 18)
        //   {
        //       HorizontalAlignment = HorizontalAlignment.Centered,
        //       VerticalAlignment = VerticalAlignment.Centered
        //   };
        //   Scene.AddLayer(layer);

        // TODO: Create sprites and add them to your layer.
        // Example:
        //   var sprite = new Sprite("hero", "hero");
        //   sprite.SetGridPosition(layer, new GridCoordinate(5, 5));
        //   Engine.Managers.Sprites.Add(sprite, layer);
    }

    // TODO: Add direct drawings (UI overlays, particles, etc.) after sprites are created.
    protected override void CreateDirectDrawings()
    {
        // TODO: Add direct drawings (non-grid-aligned graphics).
        // Example:
        //   var label = new TextBlock(
        //       RenderSurface.Host,
        //       Scene[0],
        //       new Rectangle(10, 10, 200, 30),
        //       "Hello, Gondwana!");
    }

    // TODO: Run after Engine.Initialize() completes but before the engine starts.
    protected override void OnEngineInitialized()
    {
    }

    // TODO: Called when the engine starts. Begin gameplay, start timers, or play music here.
    protected override void OnEngineStarted()
    {
    }

    // TODO: Run after the full host initialization sequence has completed.
    protected override void OnInitialized()
    {
    }

    // TODO: Unsubscribe from any events you hooked during initialization.
    protected override void UnhookEvents()
    {
        // TODO: Unsubscribe from any events you hooked in OnKeyboardAdapterInitialized.
        // Example:
        //   Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;
    }

    // TODO: Run just before the host begins disposing managed resources.
    protected override void OnDisposing()
    {
    }

    #endregion

    #region BlazorGameHost overrides

    // TODO: Configure Blazor-specific platform services after the default setup runs.
    protected override void OnConfigurePlatform()
    {
    }

    // TODO: Called after keyboard input is initialized. Wire up key event handlers here.
    protected override void OnKeyboardAdapterInitialized()
    {
        // TODO: Subscribe to keyboard events.
        // Example:
        //   Engine.Input.KeyboardEventPoller.KeyDown += OnKeyDown;
    }

    // TODO: Subscribe to mouse events here after the adapter is initialized.
    protected override void OnMouseAdapterInitialized()
    {
    }

    // TODO: Configure gamepad behavior after the default setup runs.
    protected override void OnConfigureGamepads()
    {
    }

    // TODO: Subscribe to touch or gesture events here after the adapter is initialized.
    protected override void OnTouchAdapterInitialized()
    {
    }

    // TODO: Run after Blazor-specific interop resources have been released.
    protected override void OnBlazorDisposed()
    {
    }

    #endregion
}
