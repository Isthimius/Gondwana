using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Physics.Collisions;

namespace Gondwana.Tests;

public sealed class TileTransformGeometryTests
{
    public static IEnumerable<object[]> Orientations()
    {
        yield return [TileTransform.Identity, 1, 2, 3, 4, false];
        yield return [TileTransform.Rotate90, 4, 1, 2, 3, true];
        yield return [TileTransform.Rotate180, 3, 4, 1, 2, false];
        yield return [TileTransform.Rotate270, 2, 3, 4, 1, true];
        yield return [TileTransform.FlipHorizontal, 3, 2, 1, 4, false];
        yield return [TileTransform.FlipVertical, 1, 4, 3, 2, false];
        yield return [TileTransform.FlipDiagonal, 2, 1, 4, 3, true];
        yield return [TileTransform.FlipAntiDiagonal, 4, 3, 2, 1, true];
    }

    [Theory]
    [MemberData(nameof(Orientations))]
    public void TransformsDimensionsAndDirectionalMetadata(TileTransform transform, int left, int top, int right, int bottom, bool swap)
    {
        Assert.Equal(new Spacing(left, top, right, bottom), TileTransformGeometry.TransformSpacing(new(1, 2, 3, 4), transform));
        Assert.Equal(new CollisionAdjust(top, bottom, left, right), TileTransformGeometry.TransformCollisionAdjust(new(2, 4, 1, 3), transform));
        Assert.Equal(swap ? new Size(20, 40) : new Size(40, 20), TileTransformGeometry.TransformSize(new(40, 20), transform));
    }

    [Fact]
    public void CompositionMatchesSequentialOperationsAndHasInverses()
    {
        foreach (var current in Enum.GetValues<TileTransform>())
        {
            Assert.Contains(Enum.GetValues<TileTransform>(), operation => TileTransformGeometry.Compose(current, operation) == TileTransform.Identity);
            foreach (var operation in Enum.GetValues<TileTransform>())
            {
                var composed = TileTransformGeometry.Compose(current, operation);
                Assert.Equal(TileTransformGeometry.TransformPoint(TileTransformGeometry.TransformPoint(new(7, -3), current), operation),
                    TileTransformGeometry.TransformPoint(new(7, -3), composed));
                Assert.Equal(TileTransformGeometry.TransformSpacing(TileTransformGeometry.TransformSpacing(new(1, -2, 3, -4), current), operation),
                    TileTransformGeometry.TransformSpacing(new(1, -2, 3, -4), composed));
            }
        }
    }

    [Fact]
    public void NonSquareCellRotatesAroundItsCenterWithOrientedOverhang()
    {
        Assert.Equal(new Rectangle(6, -11, 26, 44),
            TileTransformGeometry.GetVisualBounds(new(0, 0, 40, 20), new(1, 2, 3, 4), TileTransform.Rotate90));
    }
}
