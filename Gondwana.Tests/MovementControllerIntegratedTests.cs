using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Movement;
using Gondwana.Scenes;
using System.Reflection;

namespace Gondwana.Tests;

public sealed class MovementControllerIntegratedTests
{
    [Theory]
    [InlineData(1f, -1.5f)]
    [InlineData(-1.5f, 1f)]
    public void CancelVelocityComponent_PreservesWorldHorizontalVelocity_WhenBlockingWorldY_OnIsometricRhombicLayer(float gridVelocityX, float gridVelocityY)
    {
        using var sceneLayer = new TestSceneLayer(10, 10, 32, 16, CoordinateSystemTypes.IsometricRhombic);
        var mover = new TestMover(new Vector2(5f, 5f));
        var controller = CreateMovementController(mover, sceneLayer);

        controller.SetVelocity(new Vector2(gridVelocityX, gridVelocityY));

        var before = GetWorldVelocity(sceneLayer, mover.GetPosition(), controller.MovementState.Velocity);
        Assert.True(before.Y < 0f);

        InvokeZeroVelocityComponent(controller, zeroX: false, zeroY: true);

        var after = GetWorldVelocity(sceneLayer, mover.GetPosition(), controller.MovementState.Velocity);
        Assert.Equal(before.X, after.X, 4);
        Assert.Equal(0f, after.Y, 4);
    }

    private static MovementController CreateMovementController(IMovable mover, SceneLayer sceneLayer)
    {
        var initialStateFactory = typeof(MovementState).GetMethod("ForSceneLayer", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("MovementState.ForSceneLayer was not found.");
        var initialState = initialStateFactory.Invoke(null, new object[] { 0f })
            ?? throw new InvalidOperationException("MovementState.ForSceneLayer returned null.");

        return (MovementController)(Activator.CreateInstance(
            typeof(MovementController),
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            args: [mover, initialState, sceneLayer],
            culture: null)
            ?? throw new InvalidOperationException("MovementController constructor returned null."));
    }

    private static void InvokeZeroVelocityComponent(MovementController controller, bool zeroX, bool zeroY)
    {
        var zeroVelocityMethod = typeof(MovementController).GetMethod("ZeroVelocityComponent", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("MovementController.ZeroVelocityComponent was not found.");
        zeroVelocityMethod.Invoke(controller, [zeroX, zeroY]);
    }

    private static Vector2 GetWorldVelocity(SceneLayer sceneLayer, Vector2 gridPosition, Vector2 gridVelocity)
    {
        PointF originPx = sceneLayer.GridToWorldPx(new PointF(gridPosition.X, gridPosition.Y));
        PointF stepXPx = sceneLayer.GridToWorldPx(new PointF(gridPosition.X + 1f, gridPosition.Y));
        PointF stepYPx = sceneLayer.GridToWorldPx(new PointF(gridPosition.X, gridPosition.Y + 1f));

        var worldBasisX = new Vector2(stepXPx.X - originPx.X, stepXPx.Y - originPx.Y);
        var worldBasisY = new Vector2(stepYPx.X - originPx.X, stepYPx.Y - originPx.Y);
        return (worldBasisX * gridVelocity.X) + (worldBasisY * gridVelocity.Y);
    }

    private sealed class TestMover(Vector2 position) : IMovable
    {
        private Vector2 _position = position;

        public MovementSpace PositionSpace => MovementSpace.Grid;

        public Vector2 GetPosition() => _position;

        public void SetPosition(Vector2 pos) => _position = pos;
    }

    private sealed class TestSceneLayer : SceneLayer
    {
        public TestSceneLayer(int columnCount, int rowCount, int width, int height, CoordinateSystemTypes coordinateSystem)
            : base(columnCount, rowCount, width, height, coordinateSystem: coordinateSystem)
        {
        }
    }
}
