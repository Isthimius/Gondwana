using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

public interface ISceneLayerCoordinates
{
    Point GetSrcPixelAtLayerPoint(SceneLayer sceneLayer, PointF layerPoint);

    PointF GetLayerPointAtPixel(SceneLayer sceneLayer, Point pixelPt);

    List<SceneLayerPoint> GetLayerPointListInPixelRange(SceneLayer sceneLayer, Rectangle pixelRange, bool includeOverhang);

    Rectangle GetPixelRangeAtLayerPoint(Tile tile, bool includeOverhang);

    Rectangle GetPixelRangeAtLayerPointList(List<Tile> tileList, bool includeOverhang);

    SceneLayerPoint GetAdjacentLayerPoint(SceneLayerPoint layerPoint, CardinalDirections direction);

    Point[] GetPolygonPts(Tile tile, bool includeOverhang);

    PointF FindEquivalentLayerPoint(PointF valColRow, int xUpperBound, int yUpperBound);
}