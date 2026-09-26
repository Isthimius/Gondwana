using Gondwana.Configuration;

namespace Gondwana.Demos.Spot;

internal static class SpotSettings
{
    internal const string Section = "spot";
    internal const string Music = "music";
    internal const string SoundEffects = "soundEffects";
    internal const string Jiggle = "jiggle";
    internal const string Clouds = "clouds";

    internal static bool ReadBool(EngineConfiguration configuration, string key, bool defaultValue = true)
    {
        var raw = configuration.GetConfigurationValue(
            Section,
            key,
            defaultValue ? "true" : "false");

        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }

    internal static void WriteBool(Engine engine, string key, bool value)
    {
        engine.Configuration.SetConfigurationValue(
            Section,
            key,
            value ? "true" : "false");
        engine.SaveConfiguration();
    }
}
