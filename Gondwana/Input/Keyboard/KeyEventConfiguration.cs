
namespace Gondwana.Input.Keyboard;

public class KeyEventConfiguration : InputEventConfigurationBase
{
    public string Key { get; private set; } // Could be "A", "Enter", "ArrowUp", etc.

    public KeyEventConfiguration(string key, double secondsBetweenEvents = 0, bool isPaused = false)
        : base(secondsBetweenEvents, isPaused)
    {
        Key = key;
    }

    public override string ToString()
    {
        return $"KeyEventConfiguration: Key={Key}, TimeBetweenEvents={TimeBetweenEvents}, IsPaused={IsPaused}, ReadyForNextEvent={ReadyForNextEvent}";
    }
}