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
/// <see href="https://github.com/Isthimius/Gondwana/blob/master/first-game-in-15-minutes.md">first-game-in-15-minutes.md</see>.
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

    // TODO: Load tilesheets from the assets\ folder.
    // A Tilesheet is an image atlas; TileSize tells Gondwana the size of one frame.
    // Example:
    //   var sheet = new Tilesheet("mySprite", @"assets\my-sprite.png");
    //   sheet.TileSize = new System.Drawing.Size(64, 64);
    protected override void LoadTilesheets()
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

    // TODO: Place sprites into the scene.
    // Call base.CreateSceneGraph() first so this.Scene is populated.
    // Example:
    //   base.CreateSceneGraph();
    //   var frame = new Frame(mySheet, 0, 0);
    //   var sprite = SpriteManager.Instance.CreateSprite(Scene![0], frame);
    //   sprite.SetPosition(new(0, 0));
    //   sprite.Visible = true;
    protected override void CreateSceneGraph()
    {
        base.CreateSceneGraph();
    }

    // TODO: Subscribe to keyboard events here after the adapter is initialized.
    // Key codes correspond to Avalonia.Input.Key values cast to int.
    // Example:
    //   Engine.Input.KeyboardEventPoller!.KeyDown += OnKeyDown;
    //   Engine.Input.KeyboardEventPoller.StartMonitoringKey((int)Avalonia.Input.Key.Left);
    protected override void OnKeyboardAdapterInitialized()
    {
    }

    // TODO: Unsubscribe any events subscribed in OnKeyboardAdapterInitialized
    // to avoid memory leaks during shutdown.
    protected override void UnhookEvents()
    {
    }
}
