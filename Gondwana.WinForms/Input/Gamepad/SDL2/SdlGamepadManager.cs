using Microsoft.Extensions.Logging;
using Gondwana.Input.Gamepad;
using static SDL2.SDL;

namespace Gondwana.WinForms.Input.Gamepad.SDL2;

public sealed class SdlGamepadManager : IGamepadManager<SdlGamepadAdapter>
{
    public static SdlGamepadManager? Instance { get; private set; }

    private readonly Dictionary<int, SdlGamepadAdapter> _connected = new();

    public IReadOnlyCollection<SdlGamepadAdapter> ConnectedAdapters => _connected.Values;

    private SdlGamepadManager()
    {
        SDL_Init(SDL_INIT_GAMECONTROLLER);
        SDL_GameControllerEventState(SDL_DISABLE); // Polling only

        Engine.Logger.LogInformation("SdlGamepadManager initialized. Polling enabled.");
    }

    public static SdlGamepadManager Start()
    {
        if (Instance == null)
            Instance = new SdlGamepadManager();

        return Instance;
    }

    public static void Stop()
    {
        if (Instance != null)
        {
            Engine.Logger.LogInformation("SdlGamepadManager stopped. Disposing gamepad adapters...");

            foreach (var adapter in Instance._connected.Values)
                adapter.Dispose();

            Instance._connected.Clear();
            Instance = null;
        }
    }

    public void Update()
    {
        SDL_GameControllerUpdate();

        var stillConnected = new HashSet<int>();

        for (int i = 0; i < SDL_NumJoysticks(); i++)
        {
            if (SDL_IsGameController(i) == SDL_bool.SDL_FALSE)
                continue;

            stillConnected.Add(i);

            if (!_connected.ContainsKey(i))
            {
                _connected[i] = new SdlGamepadAdapter(i);
                Engine.Logger.LogInformation("Gamepad connected: SDL_CONTROLLER_{ControllerIndex}", i);
            }

            // ** DO NOT CALL THIS UNBOUNDED!! **
            // ** limit to Engine framerate **
            _connected[i].Poll();
        }

        foreach (var index in _connected.Keys.Except(stillConnected).ToList())
        {
            Engine.Logger.LogInformation("Gamepad disconnected: SDL_CONTROLLER_{ControllerIndex}", index);
            _connected[index].Dispose();
            _connected.Remove(index);
        }
    }
}
