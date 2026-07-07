using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Scenes;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;

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
//#if (UseGpuBackbuffer)
internal sealed class MyGameHost : WinFormsGpuGameHost
{
    internal MyGameHost(WinFormGpuRenderSurfaceControl renderSurface)
        : base(renderSurface) { }
//#else
internal sealed class MyGameHost : WinFormsGameHost
{
    internal MyGameHost(WinFormBitmapRenderSurfaceControl renderSurface)
        : base(renderSurface) { }
//#endif

    #region GameHostBase overrides

    // TODO: Run pre-initialization setup here, such as choosing config paths,
    // toggling feature flags, or preparing services before assets begin loading.
    protected override void OnInitializing()
    {
    }

    // TODO: Load non-tilesheet assets such as audio, fonts, and data files.
    protected override void LoadAssets()
    {
    }

    // TODO: Load tilesheets from the assets\ folder.
    // A Tilesheet is an image atlas; TileSize tells Gondwana the size of one frame.
    // Example:
    //   var sheet = new Tilesheet("mySprite", @"assets\my-sprite.png");
    //   sheet.TileSize = new System.Drawing.Size(64, 64);
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
    // Example:
    //   var scene = new Scene();
    //   scene.AddLayer(columnCount: 8, rowCount: 8, width: 64, height: 64,
    //                  zOrder: 10, parallax: 1f,
    //                  coordinateSystem: CoordinateSystemTypes.Orthogonal);
    //   return scene;
    protected override Scene CreateInitialScene()
    {
        return Scene.Empty;
    }

    // TODO: Create initial camera/view objects after the Scene has been created.
    protected override void CreateInitialViews()
    {
    }

    // TODO: Place sprites into the scene.
    // Scene is already created and bound when this runs.
    // Example:
    //   var frame = new Frame(mySheet, 0, 0);
    //   var sprite = SpriteManager.Instance.CreateSprite(Scene![0], frame);
    //   sprite.SetPosition(new(0, 0));
    //   sprite.Visible = true;
    protected override void CreateSprites()
    {
    }

    // TODO: Create direct-drawing primitives such as UI overlays or debug shapes.
    protected override void CreateDirectDrawings()
    {
    }

    // TODO: Run after Engine.Initialize() completes but before the engine starts.
    protected override void OnEngineInitialized()
    {
    }

    // TODO: Run after the engine has started. Start gameplay, timers, or music here.
    protected override void OnEngineStarted()
    {
    }

    // TODO: Run after the full host initialization sequence has completed.
    protected override void OnInitialized()
    {
    }

    // TODO: Unsubscribe any events subscribed during initialization to avoid memory leaks.
    protected override void UnhookEvents()
    {
    }

    // TODO: Run just before the host begins disposing managed resources.
    protected override void OnDisposing()
    {
    }

    // TODO: Run after disposal is complete.
    protected override void OnDisposed()
    {
    }

    #endregion

    #region WinFormsGameHostBase overrides

    // TODO: Configure WinForms-specific platform services after the default setup runs.
    protected override void OnConfigurePlatform()
    {
    }

    // TODO: Run after the current scene has been bound to the render surface.
    protected override void OnSceneBound()
    {
    }

    // TODO: Subscribe to keyboard events here after the adapter is initialized.
    // Example:
    //   Engine.Input.KeyboardEventPoller!.KeyDown += OnKeyDown;
    //   Engine.Input.KeyboardEventPoller.StartMonitoringKey((int)System.Windows.Forms.Keys.Left);
    protected override void OnKeyboardAdapterInitialized()
    {
    }

    // TODO: Subscribe to mouse events here after the adapter is initialized.
    protected override void OnMouseAdapterInitialized()
    {
    }

    // TODO: Attach gamepad behavior here after gamepad support is initialized.
    protected override void OnGamepadManagerInitialized()
    {
    }

    // TODO: Subscribe to touch/gesture events here after the adapter is initialized.
    // Note: WinForms does not currently provide a built-in touch adapter. Override
    // ConfigureTouch() upstream to assign a custom adapter via Engine.Input.TouchAdapter,
    // then use this hook to attach gesture recognizers.
    // Example:
    //   var tap = new TapGestureRecognizer(Engine.Input.TouchEventPoller!);
    //   tap.Tapped += (_, e) => { /* handle tap at e.Position */ };
    protected override void OnTouchAdapterInitialized()
    {
    }

    #endregion
}
