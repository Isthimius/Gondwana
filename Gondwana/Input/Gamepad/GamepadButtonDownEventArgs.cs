namespace Gondwana.Input.Gamepad;

public sealed class GamepadButtonDownEventArgs : EventArgs
{
    public GamepadButtonEventConfiguration Config { get; }
    public IGamepadAdapter Adapter { get; }

    public GamepadButtonDownEventArgs(GamepadButtonEventConfiguration config, IGamepadAdapter adapter)
    {
        Config = config;
        Adapter = adapter;
    }
}