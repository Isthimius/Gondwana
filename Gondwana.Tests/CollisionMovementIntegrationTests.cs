using System.Drawing;
using System.Numerics;
using Gondwana.Collisions;
using Gondwana.Movement;

namespace Gondwana.Tests;

public sealed class CollisionMovementIntegrationTests
{
    [Fact]
    public void Resolve_WhenMovingIntoVerticalWallWithoutMaxSpeed_PreservesTangentVelocity()
    {
        var simulation = new WallSlideSimulation(
            initialPosition: new Vector2(11f, 0f),
            initialVelocity: new Vector2(0f, 3f),
            acceleration: new Vector2(40f, 0f),
            maxSpeed: null);

        simulation.Step(0.1f);
        float speedAfterFirstContact = simulation.Mover.Movement.MovementState.Velocity.Y;
        simulation.Step(0.1f);
        float speedAfterSecondFrame = simulation.Mover.Movement.MovementState.Velocity.Y;

        Assert.Equal(speedAfterFirstContact, speedAfterSecondFrame, 4);
        Assert.Equal(0f, simulation.Mover.Movement.MovementState.Velocity.X, 4);
    }

    [Fact]
    public void Resolve_WhenBlockedAxisIsReacceleratedWithMaxSpeed_PreservesTangentVelocityAfterInitialContact()
    {
        var simulation = new WallSlideSimulation(
            initialPosition: new Vector2(11f, 0f),
            initialVelocity: new Vector2(0f, 8f),
            acceleration: new Vector2(40f, 0f),
            maxSpeed: 8f);

        simulation.Step(0.1f);
        float speedAfterFirstContact = simulation.Mover.Movement.MovementState.Velocity.Y;
        simulation.Step(0.1f);
        float speedAfterSecondFrame = simulation.Mover.Movement.MovementState.Velocity.Y;

        Assert.Equal(speedAfterFirstContact, speedAfterSecondFrame, 3);
        Assert.Equal(0f, simulation.Mover.Movement.MovementState.Velocity.X, 4);
    }

    private sealed class WallSlideSimulation
    {
        private readonly CollisionResolver _resolver;
        private readonly ColliderRegistry _registry;

        public TestCollisionMover Mover { get; }

        public WallSlideSimulation(Vector2 initialPosition, Vector2 initialVelocity, Vector2 acceleration, float? maxSpeed)
        {
            _registry = new ColliderRegistry();
            _resolver = new CollisionResolver(_registry);

            Mover = new TestCollisionMover(initialPosition, width: 10, height: 10);
            Mover.Movement.SetVelocity(initialVelocity);
            Mover.Movement.SetAcceleration(acceleration);
            Mover.Movement.SetMaxSpeed(maxSpeed);

            var dynamicCollider = new TestCollider(Mover, isStatic: false);
            var wall = new TestStaticEntity(new Rectangle(20, -100, 10, 200));
            var staticCollider = new TestCollider(wall, isStatic: true);

            _registry.Register(dynamicCollider);
            _registry.Register(staticCollider);
        }

        public void Step(float dt)
        {
            Mover.Movement.AdvanceMovement(dt);
            _resolver.Resolve();
        }
    }

    private sealed class TestCollisionMover : IMovable, ICollisionMovableEntity
    {
        private Vector2 _position;
        private readonly int _width;
        private readonly int _height;

        public TestCollisionMover(Vector2 initialPosition, int width, int height)
        {
            _position = initialPosition;
            _width = width;
            _height = height;
            Movement = new MovementController(this, MovementState.ForPixel());
        }

        public MovementController Movement { get; }

        public MovementSpace PositionSpace => MovementSpace.Pixel;

        public Rectangle CollisionArea => new(
            x: (int)MathF.Round(_position.X),
            y: (int)MathF.Round(_position.Y),
            width: _width,
            height: _height);

        public Vector2 GetPosition() => _position;

        public void SetPosition(Vector2 pos) => _position = pos;

        public void TranslateWorldPx(int dx, int dy) => _position += new Vector2(dx, dy);

        public void CancelVelocityComponent(bool cancelX, bool cancelY) => Movement.ZeroVelocityComponent(cancelX, cancelY);

        public void SetBlockedAxesForNextIntegratedStep(bool blockX, bool blockY) =>
            Movement.SetBlockedAxesForNextIntegratedStep(blockX, blockY);
    }

    private sealed class TestStaticEntity(Rectangle collisionArea) : ICollisionEntity
    {
        public Rectangle CollisionArea { get; } = collisionArea;
    }

    private sealed class TestCollider : ICollider
    {
        public TestCollider(ICollisionEntity owner, bool isStatic)
        {
            Owner = owner;
            IsStatic = isStatic;
        }

        public Aabb BoundsWorldPx => Aabb.FromRectangle(Owner.CollisionArea);

        public ICollisionEntity Owner { get; }

        public bool IsStatic { get; }

        public int CollisionGroup { get; set; } = 1;

        public int CollidesWith { get; set; } = 1;

        public CollisionResponseType ResponseType { get; set; } = CollisionResponseType.Solid;
    }
}
