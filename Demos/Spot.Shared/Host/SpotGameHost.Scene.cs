using System.Drawing;
using Gondwana.Demos.Spot.Game;
using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameRuntime
{
    internal Scene CreateInitialScene()
    {
        var scene = new Scene();

        var sceneLayer = scene.AddLayer(
            columnCount: 1,
            rowCount: 1,
            width: 768,
            height: 768,
            zOrder: 10,
            parallax: 1f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);

        sceneLayer.ShowGridLines = false;
        sceneLayer.OriginPx = new Point(0, -PersistentMenuHeight);

        _scene = scene;
        return scene;
    }

    internal void OnSceneGraphCreated()
    {
        SurfaceHost.Backbuffer.ClearColor = Color.CornflowerBlue.ToSKColor();

        SpotGame = new SpotGame();
        HookSpotGameEvents();
    }
}
