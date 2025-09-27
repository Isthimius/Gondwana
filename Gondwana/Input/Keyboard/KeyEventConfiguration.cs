namespace Gondwana.Input.Keyboard;

public class KeyEventConfiguration : InputEventConfigurationBase
{
    public string Key { get; private set; } // Could be "A", "Enter", "ArrowUp", etc.

    public KeyEventConfiguration(string key, double secondsBetweenEvents = 0, bool isPaused = false)
        : base(secondsBetweenEvents, isPaused)
    {
        Key = key;
    }
}