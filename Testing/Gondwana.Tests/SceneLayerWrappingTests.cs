using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes;
using Newtonsoft.Json;

namespace Gondwana.Tests;

public sealed class SceneLayerWrappingTests
{
    public static IEnumerable<object[]> Projections => Enum.GetValues<CoordinateSystemTypes>().Select(p => new object[] { p });

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Lookup_OnlyEnabledAxesWrap_AndIndexerNeverWraps(bool horizontal, bool vertical)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 6);
        layer.WrapHorizontally = horizontal;
        layer.WrapVertically = vertical;
        Assert.Equal(new PointF(horizontal ? 3 : -9, vertical ? 1 : 19), layer.WrapGrid(new(-9, 19)));
        Assert.Null(layer[-1, 0]);
        Assert.Null(layer[4, 6]);
        Assert.Same(horizontal ? layer[3, 0] : null, layer.ResolveWrappedTile(-9, 0));
        Assert.Same(vertical ? layer[0, 1] : null, layer.ResolveWrappedTile(0, 19));
        Assert.Same(horizontal && vertical ? layer[3, 1] : null, layer.ResolveWrappedTile(-9, 19));
        Assert.Same(horizontal ? layer[3, 0] : null, layer.GetAdjacentTile(layer[0, 0]!, CardinalDirections.W));
    }

    [Theory]
    [MemberData(nameof(Projections))]
    public void RepeatVectors_MatchVirtualAnchors_AndRenderingKeepsCanonicalIdentity(CoordinateSystemTypes projection)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 6, 32, 32, coordinateSystem: projection);
        layer.WrapHorizontally = layer.WrapVertically = true;
        var period = layer.GetPeriod();
        for (int x = -9; x < 10; x++)
            for (int y = -13; y < 14; y++)
            {
                var canonical = layer.WrapGrid(new(x, y));
                var anchor = layer.GridToWorldPx(new(x, y));
                var original = layer.GridToWorldPx(canonical);
                var offset = period.Offset((x - canonical.X) / 4, (y - canonical.Y) / 6);
                Assert.Equal(new PointF(original.X + offset.X, original.Y + offset.Y), anchor);
                Assert.Same(layer[(int)canonical.X, (int)canonical.Y], layer.ResolveWrappedTile(x, y));
            }
        var target = period.Offset(-3, 4);
        var rect = layer[0, 0]!.DrawLocationWorld;
        rect.Offset(Point.Round(target));
        var draws = layer.GetDrawablesInWorldRect(rect).Cast<WrappedDrawable>().ToList();
        Assert.Contains(draws, d => ReferenceEquals(d.Owner, layer[0, 0]) && d.Offset == target);
    }

    [Theory]
    [InlineData(CoordinateSystemTypes.HexAxialFlatTop, true)]
    [InlineData(CoordinateSystemTypes.HexAxialPointedTop, false)]
    public void StaggerValidation_IsDeferredUntilUse_AndDisabledAxisIsAllowed(CoordinateSystemTypes projection, bool horizontal)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(3, 3, coordinateSystem: projection);
        layer.WrapHorizontally = horizontal;
        layer.WrapVertically = !horizontal;
        Assert.Throws<InvalidOperationException>(() => layer.WrapGrid(new(-1, -1)));
        layer.WrapHorizontally = !horizontal;
        layer.WrapVertically = horizontal;
        layer.WrapGrid(new(-1, -1));
        layer.CoordinateSystemType = CoordinateSystemTypes.Orthogonal;
        layer.WrapHorizontally = layer.WrapVertically = true;
        Assert.Equal(new PointF(2, 2), layer.WrapGrid(new(-1, -1)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SerializedFlags_RoundTrip_AndOldTrueFieldsBecomeOperational(bool horizontal, bool vertical)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        layer.WrapHorizontally = horizontal;
        layer.WrapVertically = vertical;
        layer.OriginPx = new(7, 9);
        layer[2, 3]!.Nickname = "preserved tile";
        layer[2, 3]!.Visible = false;
        var json = JsonConvert.SerializeObject(layer, EngineState.JsonSerializerSettings);
        Assert.Equal(horizontal, (bool)Newtonsoft.Json.Linq.JObject.Parse(json)["WrapHorizontally"]!);
        using var restored = JsonConvert.DeserializeObject<SceneLayer>(json, EngineState.JsonSerializerSettings)!;
        Assert.Equal(new PointF(horizontal ? 3 : -1, vertical ? 3 : -1), restored.WrapGrid(new(-1, -1)));
        Assert.Equal(horizontal, restored.WrapHorizontally);
        Assert.Equal(vertical, restored.WrapVertically);
        Assert.Equal(new Point(7, 9), restored.OriginPx);
        Assert.Equal("preserved tile", restored[2, 3]!.Nickname);
        Assert.False(restored[2, 3]!.Visible);
        Assert.Same(restored, restored[2, 3]!.SceneLayer);
    }

    [Fact]
    public void LargeView_RepeatsMoreThanNineCopies_AndSortsTranslatedBounds()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(1, 1, 16, 16);
        layer.WrapHorizontally = layer.WrapVertically = true;
        var draws = layer.GetDrawablesInWorldRect(new(-32, -32, 80, 80)).Cast<WrappedDrawable>().ToArray();
        Assert.Equal(25, draws.Length);
        Assert.All(draws, d => Assert.Same(layer[0, 0], d.Owner));
        Assert.Equal(draws.OrderBy(d => d.Offset.Y).ThenBy(d => d.Offset.X).ToArray(), draws);
        Assert.Throws<InvalidOperationException>(() => layer.GetWrappedOffsets(new(0, 0, 16, 16), new(0, 0, 1e9f, 1e9f)).ToArray());
    }

    [Theory]
    [InlineData(125, 10)]
    [InlineData(10, 125)]
    [InlineData(125, 125)]
    [InlineData(-3, -3)]
    [InlineData(509, -259)]
    public void CollisionQueries_UseTranslatedTileBounds(int x, int y)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        layer.WrapHorizontally = layer.WrapVertically = true;
        var tile = layer[0, 0]!;
        tile.CollisionsEnabled = true;
        tile.Collider!.CollisionGroup = tile.Collider.CollidesWith = 1;
        var results = new List<ColliderInstance>();
        var area = new Aabb(x, y, x + 10, y + 10);
        layer.ColliderRegistry.QueryInstances(area, 1, 1, results);
        var hit = Assert.Single(results);
        Assert.Same(tile.Collider, hit.Collider);
        Assert.True(area.Intersects(hit.BoundsWorldPx));
        Assert.Same(tile, hit.Collider.Owner);
        layer.ColliderRegistry.QueryInstances(area, 1, 1, results, tile.Collider);
        Assert.Empty(results);
    }

}
