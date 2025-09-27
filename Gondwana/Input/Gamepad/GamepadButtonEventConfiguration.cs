namespace Gondwana.Input.Gamepad;

public class GamepadButtonEventConfiguration : InputEventConfigurationBase
{
    public string Button { get; private set; }

    public GamepadButtonEventConfiguration(string button, double secondsBetweenEvents = 0, bool isPaused = false)
        : base(secondsBetweenEvents, isPaused)
    {
        Button = button;
    }
}