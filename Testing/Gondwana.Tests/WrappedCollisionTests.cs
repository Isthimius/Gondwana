using System.Drawing;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes;

namespace Gondwana.Tests;

public sealed class WrappedCollisionTests
{
    [Theory]
    [InlineData(126, 10, false, true)]
    [InlineData(10, 126, false, true)]
    [InlineData(126, 126, false, true)]
    [InlineData(-2, -2, false, true)]
    [InlineData(126, 126, true, true)]
    [InlineData(126, 126, false, false)]
    public void Resolver_UsesTranslatedBounds_AndReportsCanonicalCollider(int x, int y, bool trigger, bool isStatic)
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        layer.WrapHorizontally = layer.WrapVertically = true;
        var mover = new Collider(new(x, y, 10, 10), false);
        var other = new Collider(new(0, 0, 32, 32), isStatic) { ResponseType = trigger ? CollisionResponseType.Trigger : CollisionResponseType.Solid };
        layer.ColliderRegistry.Register(mover);
        layer.ColliderRegistry.Register(other);
        int events = 0;
        void OnOverlap(ICollider a, ICollider b, Rectangle bounds)
        {
            if (!ReferenceEquals(a, mover)) return;
            Assert.Same(other, b);
            Assert.False(bounds.IsEmpty);
            events++;
        }
        layer.CollisionResolver.TriggerOverlap += OnOverlap;
        layer.CollisionResolver.SolidOverlap += OnOverlap;
        var before = mover.CollisionArea;
        layer.CollisionResolver.Resolve();
        Assert.Equal(1, events);
        if (trigger) Assert.Equal(before, mover.CollisionArea);
        else Assert.NotEqual(before, mover.CollisionArea);
    }

    [Fact]
    public void FrameOverhangAndCollisionAdjust_AreAppliedBeforeTranslation()
    {
        using var bitmap = new global::SkiaSharp.SKBitmap(64, 64);
        using var sheet = Gondwana.Drawing.Tilesheets.TilesheetFactory.FromBitmap("WrappedOverhang", bitmap);
        sheet.DefaultRegion.TileSize = new(32, 32);
        sheet.DefaultRegion.Overhang = new Gondwana.Drawing.Spacing(40, 40, 40, 40);
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        layer.WrapHorizontally = layer.WrapVertically = true;
        var tile = layer[0, 0]!;
        tile.CurrentFrame = sheet.GetFrame(0, 0);
        tile.AdjustCollisionArea = new CollisionAdjust(-10, -10, -10, -10);
        tile.CollisionsEnabled = true;
        tile.Collider!.CollisionGroup = tile.Collider.CollidesWith = 1;
        var area = tile.Collider.BoundsWorldPx;
        var query = new Aabb(area.MinX + 128, area.MinY + 128, area.MinX + 130, area.MinY + 130);
        var instances = new List<ColliderInstance>();
        layer.ColliderRegistry.QueryInstances(query, 1, 1, instances);
        Assert.Contains(instances, instance => ReferenceEquals(instance.Collider, tile.Collider) && instance.BoundsWorldPx.MinX == area.MinX + 128);
        var draws = layer.GetDrawablesInWorldRect(new(90, 90, 2, 2)).Cast<Gondwana.Drawing.WrappedDrawable>();
        Assert.Contains(draws, draw => ReferenceEquals(draw.Owner, tile) && draw.Offset == new PointF(128, 128));
    }

    private sealed class Collider(Rectangle bounds, bool isStatic) : ICollider, ICollisionMovableEntity
    {
        private Rectangle _bounds = bounds;
        public Aabb BoundsWorldPx => Aabb.FromRectangle(_bounds);
        public ICollisionEntity Owner => this;
        public bool IsStatic => isStatic;
        public int CollisionGroup { get; set; } = 1;
        public int CollidesWith { get; set; } = 1;
        public CollisionResponseType ResponseType { get; set; } = CollisionResponseType.Solid;
        public Rectangle CollisionArea => _bounds;
        public void TranslateWorldPx(int dx, int dy) => _bounds.Offset(dx, dy);
        public void CancelVelocityComponent(bool cancelX, bool cancelY) { }
    }
}
