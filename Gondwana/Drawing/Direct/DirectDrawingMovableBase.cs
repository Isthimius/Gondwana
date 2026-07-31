using System.Drawing;
using System.Numerics;
using Gondwana.Physics.Movement;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Timers;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Provides an abstract base class for direct drawings that support physics-based movement.
/// </summary>
/// <remarks>
/// <para>
/// DirectDrawingMovableBase extends <see cref="DirectDrawingBase"/> with integrated movement capabilities via
/// the <see cref="MovementController"/>. It implements <see cref="IMovable"/> to participate in the engine's
/// movement, enabling direct drawings to move smoothly, respond to forces, and interact
/// with other movable objects.
/// </para>
/// <para>
/// Key features:
/// <list type="bullet">
/// <item><description>Fixed-timestep physics integration at 240 Hz for stable, deterministic movement.</description></item>
/// <item><description>Automatic position synchronization between the movement controller and drawing bounds.</description></item>
/// <item><description>Support for both world-space (scene-layer mode) and screen-space (view mode) movement.</description></item>
/// <item><description>Frame-rate independent physics with accumulator and substep clamping to prevent spiral of death.</description></item>
/// </list>
/// </para>
/// <para>
/// The class uses a fixed-timestep update loop (240 Hz / ~4.16ms per step) to advance movement physics,
/// ensuring consistent behavior regardless of the application frame rate. Each frame's variable delta time
/// is accumulated and subdivided into fixed steps, with a maximum substep limit to prevent performance
/// degradation during frame rate drops.
/// </para>
/// <para>
/// Derived classes must implement <see cref="DirectDrawingBase.OnDraw"/> to perform the actual rendering.
/// The base class handles position updates and ensures drawing bounds remain synchronized with the
/// <see cref="Movement"/> controller's position.
/// </para>
/// <para>
/// Thread safety: This class is not thread-safe. All operations should be performed on the UI thread.
/// </para>
/// </remarks>
public abstract class DirectDrawingMovableBase : DirectDrawingBase, IDirectCompositeChild
{
    private Vector2 _posPx;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectDrawingMovableBase"/> class with integrated movement capabilities.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that manages rendering for this direct drawing. Must not be <see langword="null"/>.</param>
    /// <param name="mode">The drawing mode determining how this object is positioned and transformed.</param>
    /// <param name="sceneLayer">The scene layer to which this drawing is attached (required if <paramref name="mode"/> is <see cref="DirectDrawingMode.SceneLayer"/>).</param>
    /// <param name="view">The view to which this drawing is attached (required if <paramref name="mode"/> is <see cref="DirectDrawingMode.View"/>).</param>
    /// <param name="screenBounds">The screen-space bounds in pixels (required if <paramref name="mode"/> is <see cref="DirectDrawingMode.View"/>).</param>
    /// <param name="worldBounds">The world-space bounds in pixels (required if <paramref name="mode"/> is <see cref="DirectDrawingMode.SceneLayer"/>).</param>
    /// <param name="name">An optional human-readable name for this direct drawing, useful for debugging and identification.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderSurfaceHost"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when required parameters for the specified <paramref name="mode"/> are <see langword="null"/>.
    /// See <see cref="DirectDrawingBase"/> constructor documentation for details.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The constructor initializes the <see cref="Movement"/> controller with the drawing's initial position
    /// extracted from the provided bounds (either <paramref name="worldBounds"/> or <paramref name="screenBounds"/>
    /// depending on the mode). The movement controller is configured for pixel-space movement matching the
    /// drawing's coordinate system.
    /// </para>
    /// <para>
    /// After construction, use the <see cref="Movement"/> property to configure velocity, acceleration,
    /// friction, and other movement parameters. The drawing's position will automatically update each frame
    /// based on the movement controller's physics simulation.
    /// </para>
    /// </remarks>
    protected DirectDrawingMovableBase(RenderSurfaceHostBase renderSurfaceHost,
                                       DirectDrawingMode mode,
                                       SceneLayer? sceneLayer,
                                       View? view,
                                       Rectangle? screenBounds,
                                       Rectangle? worldBounds,
                                       string? name = null)
        : base(renderSurfaceHost, mode, sceneLayer, view, screenBounds, worldBounds, name)
    {
        Rectangle bounds = (mode == DirectDrawingMode.SceneLayer ? worldBounds : screenBounds)!.Value;
        _posPx = new Vector2(bounds.X, bounds.Y);

        var movementState = MovementState.ForPixel();
        Movement = new MovementController(this, movementState);
    }

    /// <summary>
    /// Gets the movement controller that manages physics-based position, velocity, and forces for this direct drawing.
    /// </summary>
    /// <value>
    /// A <see cref="MovementController"/> instance providing access to movement state, velocity, acceleration,
    /// friction, and other physics properties.
    /// </value>
    /// <remarks>
    /// <para>
    /// Use the movement controller to:
    /// <list type="bullet">
    /// <item><description>Apply forces and impulses (e.g., <c>Movement.ApplyForce()</c>, <c>Movement.ApplyImpulse()</c>).</description></item>
    /// <item><description>Set or query velocity (e.g., <c>Movement.Velocity</c>).</description></item>
    /// <item><description>Configure friction, mass, and other physical properties.</description></item>
    /// <item><description>Enable or disable movement integration.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The controller is automatically integrated each frame by <see cref="Update"/> using a fixed timestep
    /// (240 Hz). Position changes from the controller are synchronized to the drawing's bounds, triggering
    /// dirty-rectangle updates as needed.
    /// </para>
    /// <para>
    /// The controller operates in pixel space (<see cref="MovementSpace.Pixel"/>), matching the coordinate
    /// system used by the drawing's bounds (either world or screen coordinates depending on the mode).
    /// </para>
    /// </remarks>
    public MovementController Movement { get; }

    /// <summary>
    /// Gets the coordinate space in which this movable object's position is expressed.
    /// </summary>
    /// <value>
    /// Always returns <see cref="MovementSpace.Pixel"/>, indicating that position values are in pixel coordinates.
    /// </value>
    /// <remarks>
    /// This property is part of the <see cref="IMovable"/> interface contract. Direct drawings always use
    /// pixel-space positioning, regardless of whether they are in scene-layer mode (world pixels) or
    /// view mode (screen pixels).
    /// </remarks>
    public MovementSpace PositionSpace => MovementSpace.Pixel;

    /// <summary>
    /// Gets the current position of this direct drawing in pixel coordinates.
    /// </summary>
    /// <returns>
    /// A <see cref="Vector2"/> representing the top-left corner position.
    /// For scene-layer mode, this is the world-space position.
    /// For view mode, this is the screen-space position.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is part of the <see cref="IMovable"/> interface contract and is used by the
    /// <see cref="MovementController"/> to query the current position during physics integration.
    /// </para>
    /// <para>
    /// The position corresponds to the X and Y coordinates of the drawing's bounding rectangle
    /// (<see cref="DirectDrawingBase.WorldBounds"/> for scene-layer mode or
    /// <see cref="DirectDrawingBase.ScreenBounds"/> for view mode).
    /// </para>
    /// </remarks>
    public Vector2 GetPosition() => _posPx;

    /// <summary>
    /// Sets the position of this direct drawing to the specified pixel coordinates.
    /// </summary>
    /// <param name="p">
    /// The new position as a <see cref="Vector2"/>. The X and Y components specify the top-left corner.
    /// For scene-layer mode, this is the world-space position.
    /// For view mode, this is the screen-space position.
    /// </param>
    /// <remarks>
    /// <para>
    /// This method is part of the <see cref="IMovable"/> interface contract and is used by the
    /// <see cref="MovementController"/> to apply position changes resulting from physics integration.
    /// </para>
    /// <para>
    /// The method performs the following operations:
    /// <list type="number">
    /// <item><description>Marks the current position as dirty (via <see cref="DirectDrawingBase.ForceRefresh"/>).</description></item>
    /// <item><description>Rounds the vector components to the nearest integer to maintain pixel-aligned positioning.</description></item>
    /// <item><description>Updates the appropriate bounds rectangle (world or screen) with the new position while preserving width and height.</description></item>
    /// <item><description>Marks the new position as dirty to ensure rendering occurs at the updated location.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Calling this method directly (outside of movement controller integration) will move the drawing
    /// but may not update the movement controller's internal state. To manually reposition a movable
    /// drawing, consider setting <c>Movement.State.Position</c> instead.
    /// </para>
    /// </remarks>
    public void SetPosition(Vector2 p)
    {
        // mark old area dirty
        ForceRefresh();

        // keep precise position for movement math
        _posPx = p;

        // pixel-align only for rendering bounds
        int x = (int)MathF.Round(p.X);
        int y = (int)MathF.Round(p.Y);

        if (Mode == DirectDrawingMode.SceneLayer)
        {
            var r = WorldBounds;
            WorldBounds = new Rectangle(x, y, r.Width, r.Height);
        }
        else
        {
            var r = ScreenBounds;
            ScreenBounds = new Rectangle(x, y, r.Width, r.Height);
        }

        // mark new area dirty
        ForceRefresh();
    }

    void IDirectCompositeChild.SetIsVisible(bool visible)
    {
        Visible = visible;
    }

    void IDirectCompositeChild.SetZOrder(int zOrder)
    {
        ZOrder = zOrder;
    }

    void IDirectCompositeChild.SetOpacity(float opacity)
    {
        Opacity = opacity;
    }

    void IDirectCompositeChild.FadeTo(
        float targetOpacity,
        float durationSec)
    {
        base.FadeTo(
            targetOpacity,
            durationSec);
    }

    /// <summary>
    /// Advances this direct drawing's movement using the elapsed time since
    /// its previous update, then performs the standard direct-drawing update logic.
    /// </summary>
    /// <param name="tick">
    /// The current engine tick value supplied by the direct-drawing update cycle.
    /// </param>
    /// <remarks>
    /// <para>
    /// Movement is advanced once per engine update using the actual elapsed duration
    /// calculated from <see cref="_lastTick"/> and <paramref name="tick"/>.
    /// </para>
    /// <para>
    /// The base implementation is called afterward to advance inherited behavior,
    /// including fade and reveal animations, and to record the current tick.
    /// </para>
    /// </remarks>
    public override void Update(long tick)
    {
        if (tick <= _lastTick)
            return;

        float dt =
            HighResTimer.GetDuration(_lastTick, tick);

        Movement.AdvanceMovement(dt);

        base.Update(tick);
    }
}
