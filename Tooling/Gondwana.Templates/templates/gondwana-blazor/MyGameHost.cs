using Gondwana.Assets;
using Gondwana.Blazor.Input.Keyboard;
using Gondwana.Blazor.Hosting;
using Gondwana.Blazor.Rendering;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
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
/// Browser asset loading:
/// <see href="https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Blazor/README.md">Blazor README</see>.
/// </remarks>
internal sealed class MyGameHost : BlazorGpuGameHost
{
    private readonly IReadOnlyDictionary<string, byte[]> _assetBytes;

    internal MyGameHost(
        BlazorGpuRenderSurfaceComponent renderSurface,
        IJSRuntime jsRuntime,
        IReadOnlyDictionary<string, byte[]> assetBytes)
        : base(renderSurface, jsRuntime)
    {
        _assetBytes = assetBytes;
    }

    #region GameHostBase overrides

    // TODO: Run pre-initialization setup here, such as choosing config paths,
    // toggling feature flags, or preparing services before assets begin loading.
    /// <inheritdoc/>
    protected override void OnInitializing()
    {
    }

    // TODO: Load non-tilesheet assets such as audio, fonts, and data files.
    /// <inheritdoc/>
    protected override void LoadAssets()
    {
        // Downloads happen in GameRenderSurface before Initialize(). These hooks are
        // synchronous: consume downloaded bytes here; never use async void.

        // TODO: Load audio files here.
        // Program.cs configures the browser backend before host initialization.
        // Example (serve the file at wwwroot/assets/theme.mp3):
        //   var theme = Engine.Managers.AudioResources.LoadFromUri("theme", "assets/theme.mp3");
        //   theme.IsLooping = true;
        // Call theme.Play() from a user gesture handler after loading; browser
        // autoplay policy may block playback during startup. Codec support varies.

        // For fonts/data, add their URLs to GameRenderSurface.DownloadAssetsAsync,
        // then decode _assetBytes through a stream API, not a desktop file path.
    }

    // TODO: Load tilesheet definitions after other assets are available.
    /// <inheritdoc/>
    protected override void LoadTilesheets()
    {
        // PNG example: uncomment the matching URL in DownloadAssetsAsync first.
        // using (var imageStream = new MemoryStream(_assetBytes["assets/spot_defaults.png"], writable: false))
        // {
        //     var bubble = Engine.Managers.Tilesheets.LoadFromStream("bubble", imageStream);
        //     bubble.DefaultRegion.TileSize = new System.Drawing.Size(93, 96);
        // }

        // GAF example: alternative to the loose PNG above. The package must contain
        // an Image entry named spot_defaults.png. Uncomment its download URL first.
        // using (var packageStream = new MemoryStream(_assetBytes["assets/game.gaf"], writable: false))
        // using (var package = AssetsFile.Load(packageStream))
        // using (var imageStream = package.Get(AssetTypes.Image, "spot_defaults.png")
        //     ?? throw new InvalidOperationException("Missing Image:spot_defaults.png in game.gaf"))
        // {
        //     var bubble = Engine.Managers.Tilesheets.LoadFromStream("bubble", imageStream);
        //     bubble.DefaultRegion.TileSize = new System.Drawing.Size(93, 96);
        // }

        // Both examples register "bubble"; choose one for that tilesheet.
        // Streams stay open until decoding completes. These examples consume the
        // package here and dispose it immediately. If retaining a package for later
        // hooks, keep it in a host field and dispose it during host cleanup.
        // Retrieve the registered sheet in CreateSprites via Engine.Managers.Tilesheets["bubble"].
    }

    // TODO: Load animation cycle definitions after tilesheets are available.
    /// <inheritdoc/>
    protected override void LoadAnimationCycles()
    {
    }

    // TODO: Run after the initial scene and views are created, but before the scene is bound.
    /// <inheritdoc/>
    protected override void OnSceneGraphCreated()
    {
    }

    // TODO: Build and return the initial Scene.
    /// <inheritdoc/>
    protected override Scene CreateInitialScene()
    {
        return Scene.Empty;
    }

    // TODO: Create initial camera/view objects after the Scene has been created.
    /// <inheritdoc/>
    protected override void CreateInitialViews()
    {
    }

    // Note: OnSceneBound() is invoked by GameHostBase after BindScene() completes.
    // Override OnSceneBound() if you need post-bind setup before sprites/direct drawings are created.

    // TODO: Called after assets are loaded. Build your scene layers and add sprites here.
    /// <inheritdoc/>
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
    /// <inheritdoc/>
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
    /// <inheritdoc/>
    protected override void OnEngineInitialized()
    {
    }

    // TODO: Called when the engine starts. Begin gameplay, start timers, or play music here.
    /// <inheritdoc/>
    protected override void OnEngineStarted()
    {
    }

    // TODO: Run after the full host initialization sequence has completed.
    /// <inheritdoc/>
    protected override void OnInitialized()
    {
    }

    // TODO: Unsubscribe from any events you hooked during initialization.
    /// <inheritdoc/>
    protected override void UnhookEvents()
    {
        if (Engine.Input.KeyboardEventPoller is { } keyboard)
            keyboard.KeyDown -= OnKeyDown;
    }

    // TODO: Run just before the host begins disposing managed resources.
    /// <inheritdoc/>
    protected override void OnDisposing()
    {
    }

    #endregion

    #region BlazorGameHost overrides

    // TODO: Configure Blazor-specific platform services after the default setup runs.
    /// <inheritdoc/>
    protected override void OnConfigurePlatform()
    {
    }

    // TODO: Called after keyboard input is initialized. Wire up key event handlers here.
    /// <inheritdoc/>
    protected override void OnKeyboardAdapterInitialized()
    {
        if (Engine.Input.KeyboardEventPoller is not { } keyboard)
            return;

        keyboard.KeyDown += OnKeyDown;
        // Browser codes, not System.Windows.Forms.Keys. Canvas focus is required.
        // Uncomment the keys your game uses.
        // keyboard.StartMonitoringKey((int)BlazorKey.ArrowLeft, nameof(BlazorKey.ArrowLeft));
        // keyboard.StartMonitoringKey((int)BlazorKey.ArrowRight, nameof(BlazorKey.ArrowRight));
        // keyboard.StartMonitoringKey((int)BlazorKey.ArrowUp, nameof(BlazorKey.ArrowUp));
        // keyboard.StartMonitoringKey((int)BlazorKey.ArrowDown, nameof(BlazorKey.ArrowDown));
    }

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (args.KeyAction != KeyAction.Pressed)
            return;

        // switch (args.KeyConfig.Key)
        // {
        //     case nameof(BlazorKey.ArrowLeft):
        //         // Move your player left here.
        //         break;
        // }
    }

    // TODO: Subscribe to mouse events here after the adapter is initialized.
    /// <inheritdoc/>
    protected override void OnMouseAdapterInitialized()
    {
    }

    // TODO: Configure gamepad behavior after the default setup runs.
    /// <inheritdoc/>
    protected override void OnConfigureGamepads()
    {
    }

    // TODO: Subscribe to touch or gesture events here after the adapter is initialized.
    /// <inheritdoc/>
    protected override void OnTouchAdapterInitialized()
    {
    }

    // TODO: Run after Blazor-specific interop resources have been released.
    /// <inheritdoc/>
    protected override void OnBlazorDisposed()
    {
    }

    #endregion
}
