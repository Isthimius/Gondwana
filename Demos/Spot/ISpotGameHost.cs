using System;
using System.Threading.Tasks;
using Gondwana.Demos.Spot.Game;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.Spot;

internal interface ISpotGameHost : IDisposable
{
    Engine Engine { get; }

    NewGameOptions? LastNewGameOptions { get; }

    void Initialize(string? configPath = null, bool? autoSaveConfig = null, LogLevel logLevel = LogLevel.Warning);

    Task InitializeAsync(string? configPath = null, bool? autoSaveConfig = null, LogLevel logLevel = LogLevel.Warning);

    void BeginPostSplashStartup();

    void OpenNewGameDialog(NewGameOptions? newGameOptions = null);

    void SetMusicEnabled(bool enabled);

    void SetSoundEffectsEnabled(bool enabled);

    void SetJiggleEnabled(bool enabled);

    void SetCloudsEnabled(bool enabled);

    void StartNewGame(NewGameOptions options);
}
