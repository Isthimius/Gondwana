using Gondwana;
using Gondwana.Audio;
using Gondwana.Audio.Midi;
using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;

namespace HWG.Spot;

public sealed class SpotGameHost : WinFormsGameHost
{
    public AudioResource _dorian;
    public AudioResource _tada;

    public SpotGameHost(WinFormBitmapRenderSurfaceControl renderSurface)
        : base(renderSurface) { }

    protected override void LoadAssets()
    {
        // load asset files
        _dorian = AudioResourceManager.Instance.LoadFromFile("dorian", "dorian.mid");
        //_dorian.IsLooping = true;

        _tada = AudioResourceManager.Instance.LoadFromFile("tada", "tada.wav");
        _tada.IsLooping = true;

        // load standalone audio files

        // load standalone image files

        // load standalone video files

        // load standalone cursor files
    }

    protected override void LoadTilesheets()
    {
        // Implementation for loading tilesheets goes here
    }

    protected override void LoadAnimationCycles()
    {
        // Implementation for loading animation cycles goes here
    }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();

        var sceneLayer1 = scene.AddLayer(
            columnCount: 12,
            rowCount: 12,
            width: 64,
            height: 64,
            zOrder: 10,
            parallax: 1f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);

        sceneLayer1.ShowGridLines = true;

        // Example:
        // var sourceTilesheet = TilesheetRegistry.Instance.GetAll()["tiles"];
        // sceneLayer1[0, 0] = new Tile(sourceTilesheet, 0);

        return scene;
    }

    protected override void CreateSprites()
    {
        // Implementation for creating sprites goes here
    }

    protected override void CreateDirectDrawings()
    {
        // Implementation for creating direct drawings goes here
    }

    protected override void OnStartEngine()
    {
        _dorian.Volume = 1f;
        _dorian.Play();
        //_tada.Play();
    }

    protected override void OnConfigurePlatform()
    {
        Engine.Instance.InitializeMidiAudioFormats();
    }

    protected override void OnMouseAdapterInitialized()
    {
        if (Engine.MouseEventPoller is null)
            return;

        Engine.MouseEventPoller.MouseEvent += MouseEventPoller_MouseEvent;
        Engine.MouseEventPoller.StartMonitoringMouse();
    }

    protected override void UnhookEvents()
    {
        if (Engine.MouseEventPoller is not null)
            Engine.MouseEventPoller.MouseEvent -= MouseEventPoller_MouseEvent;
    }

    private void MouseEventPoller_MouseEvent(Gondwana.Input.Mouse.MouseEventArgs args)
    {
        if (Scene is null || Scene.SceneLayers.Count == 0)
            return;

        if (RenderSurface.Host.ViewManager.Views.Count == 0)
            return;

        var view = RenderSurface.Host.ViewManager.Views[0];
        var layer = Scene.SceneLayers[0];

        var screenPos = args.CurrentPosition;

        // 1) screen -> world
        var worldFromScreen = view.ScreenPxToWorldPx(layer, screenPos);

        // 2) screen -> grid
        var gridFromScreen = view.ScreenPxToGrid(layer, screenPos);

        // 3) grid -> world
        var worldFromGrid = layer.GridToWorldPx(gridFromScreen);

        // 4) world -> screen
        var screenFromGrid = view.WorldPxToScreenPx(layer, worldFromGrid);

        _ = worldFromScreen;
        _ = gridFromScreen;
        _ = worldFromGrid;
        _ = screenFromGrid;
    }
}
