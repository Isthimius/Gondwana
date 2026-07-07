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

    #region BlazorGameHost overrides

    /// <summary>
    /// Called once on engine initialization. Load your sprites, audio, and fonts here.
    /// </summary>
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

    /// <summary>
    /// Called after assets are loaded. Build your scene layers and add sprites here.
    /// </summary>
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

    /// <summary>
    /// Called after scene layers are created. Add direct drawings (UI overlays, particles, etc.) here.
    /// </summary>
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

    /// <summary>
    /// Called when the engine starts. Begin gameplay, start timers, play music, etc.
    /// </summary>
    protected override void OnEngineStarted()
    {
        // TODO: Start your game loop, begin animations, play background music.
    }

    /// <summary>
    /// Called after keyboard input is initialized. Wire up key event handlers here.
    /// </summary>
    protected override void OnKeyboardAdapterInitialized()
    {
        // TODO: Subscribe to keyboard events.
        // Example:
        //   Engine.Input.KeyboardEventPoller.KeyDown += OnKeyDown;
    }

    /// <summary>
    /// Called when the host is being disposed. Clean up custom resources here.
    /// </summary>
    protected override void UnhookEvents()
    {
        // TODO: Unsubscribe from any events you hooked in OnKeyboardAdapterInitialized.
        // Example:
        //   Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;
    }

    #endregion
}