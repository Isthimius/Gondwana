using System.Threading;
using Gondwana.Avalonia.Hosting;
using Gondwana.Avalonia.Rendering;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;

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
internal sealed class MyGameHost : AvaloniaGpuGameHost
{
    internal MyGameHost(AvaloniaGpuRenderSurfaceControl renderSurface)
        : base(renderSurface) { }
//#else
internal sealed class MyGameHost : AvaloniaGameHost
{
    internal MyGameHost(AvaloniaBitmapRenderSurfaceControl renderSurface)
        : base(renderSurface) { }
//#endif

    #region GameHostBase overrides

    // TODO: Run code before the host initialization pipeline begins.
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

    // TODO: Run after the current scene has been bound to the render surface.
    // GPU note: AvaloniaGpuGameHost does not call this hook automatically; use BindScene()
    // below when you need post-bind work in the GPU-backed template.
    protected override void OnSceneBound()
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

    // TODO: Override to customize how the engine starts.
    // Call base.StartEngineCore(syncContext) unless you are replacing the default behavior.
    protected override void StartEngineCore(SynchronizationContext syncContext)
    {
        base.StartEngineCore(syncContext);
    }

    // TODO: Override to provide a custom synchronization context for the engine.
    protected override SynchronizationContext? GetSynchronizationContext()
    {
        return base.GetSynchronizationContext();
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

    // TODO: Override to customize how the engine stops.
    // Call base.StopEngineCore() unless you are replacing the default behavior.
    protected override void StopEngineCore()
    {
        base.StopEngineCore();
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

    #region Avalonia host overrides

//#if (UseGpuBackbuffer)
    // TODO: Override to customize platform setup while preserving the default GPU setup.
    protected override void ConfigurePlatform()
    {
        base.ConfigurePlatform();
    }

    // TODO: Override to customize keyboard setup while preserving the default adapter wiring.
    protected override void ConfigureKeyboard()
    {
        base.ConfigureKeyboard();
    }

    // TODO: Override to customize mouse setup while preserving the default adapter wiring.
    protected override void ConfigureMouse()
    {
        base.ConfigureMouse();
    }

    // TODO: Override to customize gamepad setup while preserving the default behavior.
    protected override void ConfigureGamepads()
    {
        base.ConfigureGamepads();
    }

    // TODO: Override to customize touch setup while preserving the default adapter wiring.
    protected override void ConfigureTouch()
    {
        base.ConfigureTouch();
    }

    // TODO: Override to customize how the scene is bound to the GPU render surface.
    protected override void BindScene()
    {
        base.BindScene();
    }
//#endif

    // TODO: Configure Avalonia-specific platform services after the default setup runs.
    protected override void OnConfigurePlatform()
    {
    }

    // TODO: Subscribe to keyboard events here after the adapter is initialized.
    // Key codes correspond to Avalonia.Input.Key values cast to int.
    // Example:
    //   Engine.Input.KeyboardEventPoller!.KeyDown += OnKeyDown;
    //   Engine.Input.KeyboardEventPoller.StartMonitoringKey((int)Avalonia.Input.Key.Left);
    protected override void OnKeyboardAdapterInitialized()
    {
    }

    // TODO: Subscribe to mouse events here after the adapter is initialized.
    protected override void OnMouseAdapterInitialized()
    {
    }

//#if (UseGpuBackbuffer)
    // TODO: Attach gamepad behavior here after gamepad support is initialized.
    protected override void OnGamepadManagerInitialized()
    {
    }
//#else
    // TODO: Configure Avalonia gamepad support after the default setup runs.
    protected override void OnConfigureGamepads()
    {
    }
//#endif

    // TODO: Subscribe to touch/gesture events here after the adapter is initialized.
    // Example:
    //   var tap = new TapGestureRecognizer(Engine.Input.TouchEventPoller!);
    //   tap.Tapped += (_, e) => { /* handle tap at e.Position */ };
    protected override void OnTouchAdapterInitialized()
    {
    }

    #endregion
}
