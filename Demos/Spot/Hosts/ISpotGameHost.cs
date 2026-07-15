using System;
using Gondwana.Demos.Spot.Game;
using Gondwana.Rendering;
using Gondwana.Widgets.Overlays;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.Spot.Hosts;

internal interface ISpotGameHost : IDisposable
{
    Engine Engine { get; }

    NewGameOptions? LastNewGameOptions { get; }

    void Initialize(string? configPath = null, bool? autoSaveConfig = null, LogLevel logLevel = LogLevel.Warning);

    SplashScreen? CreateSplash(RenderSurfaceHostBase host, Action onSplashCompleted);

    void BeginPostSplashStartup();

    void OpenNewGameDialog(NewGameOptions? newGameOptions = null);

    void SetMusicEnabled(bool enabled);

    void SetSoundEffectsEnabled(bool enabled);

    void SetJiggleEnabled(bool enabled);

    void SetCloudsEnabled(bool enabled);

    void StartNewGame(NewGameOptions options);
}
