using Gondwana.Input;

namespace Gondwana.Input.Mouse;

public class MouseEventConfiguration : InputEventConfigurationBase
{
    public bool TrackMouseMovement { get; set; }

    public MouseEventConfiguration(bool trackMouseMovement, double secondsBetweenEvents = 0, bool isPaused = false)
        : base(secondsBetweenEvents, isPaused)
    {
        TrackMouseMovement = trackMouseMovement;
    }
}
