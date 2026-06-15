using Gondwana.Avalonia.Hosting;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;

namespace TestBrowserApp;

/// <summary>
/// The main game host for TestBrowserApp. Override the methods below to load assets,
/// build the scene graph, and wire up input.
/// </summary>
/// <remarks>
/// Full lifecycle documentation:
/// <see href="https://github.com/Isthimius/Gondwana/wiki">Gondwana Wiki</see>
/// and the 15-minute guide at
/// <see href="https://github.com/Isthimius/Gondwana/wiki/Make-Your-First-Game-in-15-Minutes">Make Your First Game in 15 Minutes</see>.
/// </remarks>
internal sealed class TestBrowserAppHost : AvaloniaGameHost
{
    internal TestBrowserAppHost(GameRenderSurface renderSurface)
        : base(renderSurface) { }

    // TODO: Load tilesheets from the assets\ folder.
    // A Tilesheet is an image atlas; TileSize tells Gondwana the size of one frame.
    // Example:
    //   var sheet = new Tilesheet("mySprite", @"assets\my-sprite.png");
    //   sheet.TileSize = new System.Drawing.Size(64, 64);
    protected override void LoadTilesheets()
    {
    }

    // TODO: Load audio.
    //
    // On desktop targets, use the NAudio-based AudioResourceManager:
    //   Engine.Managers.AudioResources.LoadFromFile("music", @"assets\theme.mp3");
    //
    // On browser/WASM, NAudio is not available. Use BrowserAudioManager instead:
    //   if (OperatingSystem.IsBrowser())
    //   {
    //       var audio = Engine.GetBrowserAudioManager();
    //       _browserMusic = audio.Load("music", "assets/theme.mp3", volume: 0.5f, loop: true);
    //   }
    //   else
    //   {
    //       _desktopMusic = Engine.Managers.AudioResources.LoadFromFile("music", @"assets\theme.mp3");
    //       _desktopMusic.IsLooping = true;
    //   }
    //
    // NOTE: Browser autoplay policy requires audio to be triggered by a user gesture.
    // Start playback from OnStartEngine() or in response to a click/keypress.
    protected override void LoadAssets()
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

    // TODO: Subscribe to touch/gesture events here after the adapter is initialized.
    // Example:
    //   var tap = new TapGestureRecognizer(Engine.Input.TouchEventPoller!);
    //   tap.Tapped += (_, e) => { /* handle tap at e.Position */ };
    protected override void OnTouchAdapterInitialized()
    {
    }

    // TODO: Unsubscribe any events subscribed in OnKeyboardAdapterInitialized
    // to avoid memory leaks during shutdown.
    protected override void UnhookEvents()
    {
    }
}
