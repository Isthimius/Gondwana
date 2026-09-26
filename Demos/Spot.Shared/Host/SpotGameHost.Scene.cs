using System.Drawing;
using Gondwana.Demos.Spot.Game;
using Gondwana.Drawing.Coordinates;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameHost
{
    protected override Scene CreateInitialScene()
    {
        Logging.EngineLogger.SetLogLevel(LogLevel.Information);
        Gondwana.Engine.Instance.CPSCalculated += args =>
        {
            if (SurfaceHost.Backbuffer is GpuBackbuffer gpuBackbuffer)
            {
                string gpuFps = args.GpuFps.HasValue
                    ? args.GpuFps.Value.ToString("0.0")
                    : "n/a";

                Engine.Logger.LogInformation(
                    "CPS {Cps:0.0} | engine FPS {EngineFps:0.0} | GPU FPS {GpuFps} | " +
                    "MSAA requested {MsaaSampleCount} | MSAA actual {ActualMsaaSampleCount} | " +
                    "MSAA max {MaxSupportedMsaaSampleCount}",
                    args.GrossCPS,
                    args.NetCPS,
                    gpuFps,
                    gpuBackbuffer.MsaaSampleCount,
                    gpuBackbuffer.ActualMsaaSampleCount,
                    gpuBackbuffer.MaxSupportedMsaaSampleCount);

                return;
            }

            Engine.Logger.LogInformation("{CyclesPerSecond}", args);
        };

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

        return scene;
    }

    protected override void OnSceneGraphCreated()
    {
        SurfaceHost.Backbuffer.ClearColor = Color.CornflowerBlue.ToSKColor();

        SpotGame = new SpotGame();
        HookSpotGameEvents();
    }
}
