using Gondwana.Input.Gamepad;
using static SDL2.SDL;

namespace Gondwana.WinForms.Input.Gamepad.SDL2;

public sealed class SdlGamepadManager : IGamepadManager<SdlGamepadAdapter>
{
    private readonly Dictionary<int, SdlGamepadAdapter> _connected = new();

    public IReadOnlyCollection<SdlGamepadAdapter> ConnectedAdapters => _connected.Values;

    public SdlGamepadManager()
    {
        SDL_Init(SDL_INIT_JOYSTICK);
        SDL_JoystickEventState(SDL_DISABLE); // Poll-only mode
    }

    public void Update()
    {
        SDL_JoystickUpdate();

        for (int i = 0; i < SDL_NumJoysticks(); i++)
        {
            if (!_connected.ContainsKey(i))
            {
                _connected[i] = new SdlGamepadAdapter(i);
            }

            _connected[i].Poll();
        }
    }
}
