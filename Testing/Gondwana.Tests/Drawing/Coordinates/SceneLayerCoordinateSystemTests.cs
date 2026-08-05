using System.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes;

namespace Gondwana.Tests.Drawing.Coordinates;

public sealed class SceneLayerCoordinateSystemTests
{
    [Fact]
    public void CoordinateSystemTypes_PreserveObliqueRightSerializedValue()
    {
        Assert.Equal(5, (int)CoordinateSystemTypes.ObliqueRight);
        Assert.Equal(6, (int)CoordinateSystemTypes.ObliqueLeft);
    }

    [Fact]
    public void IsometricAxial_UsesTightlyPackedAffineBasis()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(
            columnCount: 4,
            rowCount: 4,
            width: 64,
            height: 32,
            coordinateSystem: CoordinateSystemTypes.IsometricAxial);

        Assert.Equal(new PointF(0f, 0f), layer.GridToWorldPx(new PointF(0f, 0f)));
        Assert.Equal(new PointF(64f, 0f), layer.GridToWorldPx(new PointF(1f, 0f)));
        Assert.Equal(new PointF(32f, 16f), layer.GridToWorldPx(new PointF(0f, 1f)));
        Assert.Equal(new PointF(96f, 16f), layer.GridToWorldPx(new PointF(1f, 1f)));
        Assert.Equal(new PointF(64f, 32f), layer.GridToWorldPx(new PointF(0f, 2f)));
    }

    [Fact]
    public void IsometricAxial_WorldToGrid_InvertsAffineBasis()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(
            columnCount: 8,
            rowCount: 8,
            width: 64,
            height: 32,
            coordinateSystem: CoordinateSystemTypes.IsometricAxial);

        Assert.Equal(new PointF(3f, 2f), layer.WorldPxToGrid(new PointF(256f, 32f)));
        Assert.Equal(new PointF(1f, 1.5f), layer.WorldPxToGrid(new PointF(112f, 24f)));
    }

    [Fact]
    public void IsometricAxial_AdjacentRowsShareDiamondEdge()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(
            columnCount: 2,
            rowCount: 2,
            width: 64,
            height: 32,
            coordinateSystem: CoordinateSystemTypes.IsometricAxial);

        Point[] first = layer[0, 0].OutlinePointsWorld;
        Point[] nextRow = layer[0, 1].OutlinePointsWorld;

        Assert.Equal(first[1], nextRow[0]);
        Assert.Equal(first[2], nextRow[3]);
    }

    [Theory]
    [InlineData(CoordinateSystemTypes.ObliqueRight, 40f)]
    [InlineData(CoordinateSystemTypes.ObliqueLeft, -40f)]
    public void ObliqueRowsRecedeInSelectedDirection(
        CoordinateSystemTypes coordinateSystem,
        float expectedRowX)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(
            columnCount: 4,
            rowCount: 4,
            width: 80,
            height: 48,
            coordinateSystem: coordinateSystem);

        Assert.Equal(new PointF(40f, 0f), layer.GridToWorldPx(new PointF(1f, 0f)));
        Assert.Equal(new PointF(expectedRowX, 48f), layer.GridToWorldPx(new PointF(0f, 1f)));
    }

    [Theory]
    [InlineData(CoordinateSystemTypes.ObliqueRight, 200f)]
    [InlineData(CoordinateSystemTypes.ObliqueLeft, 40f)]
    public void ObliqueWorldToGrid_InvertsSelectedShear(
        CoordinateSystemTypes coordinateSystem,
        float anchorX)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(
            columnCount: 5,
            rowCount: 5,
            width: 80,
            height: 48,
            coordinateSystem: coordinateSystem);

        var world = new PointF(anchorX, 96f);
        Assert.Equal(world, layer.GridToWorldPx(new PointF(3f, 2f)));
        Assert.Equal(new PointF(3f, 2f), layer.WorldPxToGrid(world));
    }

    [Fact]
    public void ObliqueLeft_PolygonMirrorsObliqueRight()
    {
        using var scene = new Scene();
        var right = scene.AddLayer(
            1,
            1,
            width: 80,
            height: 48,
            coordinateSystem: CoordinateSystemTypes.ObliqueRight);

        var left = scene.AddLayer(
            1,
            1,
            width: 80,
            height: 48,
            coordinateSystem: CoordinateSystemTypes.ObliqueLeft);

        Assert.Equal(
            new[]
            {
                new Point(0, 0),
                new Point(40, 0),
                new Point(80, 48),
                new Point(40, 48)
            },
            right[0, 0].OutlinePointsWorld);

        Assert.Equal(
            new[]
            {
                new Point(40, 0),
                new Point(80, 0),
                new Point(40, 48),
                new Point(0, 48)
            },
            left[0, 0].OutlinePointsWorld);
    }

    [Fact]
    public void CoordinateSystemType_RoundTripsBothObliqueImplementations()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(2, 2);

        layer.CoordinateSystemType = CoordinateSystemTypes.ObliqueRight;
        Assert.Equal(CoordinateSystemTypes.ObliqueRight, layer.CoordinateSystemType);

        layer.CoordinateSystemType = CoordinateSystemTypes.ObliqueLeft;
        Assert.Equal(CoordinateSystemTypes.ObliqueLeft, layer.CoordinateSystemType);
    }
}
