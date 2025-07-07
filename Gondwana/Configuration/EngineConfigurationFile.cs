using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gondwana.Configuration;

public class EngineConfigurationFile : IDisposable
{
    private const string _defaultConfigFileName = "gondwana.json";

    private EngineConfigurationFile() { }

    public static EngineConfigurationFile CreateNew(string? configFileName = null, bool? autoSave = null)
    {
        var config = new EngineConfigurationFile
        {
            FileName = configFileName ?? _defaultConfigFileName,
            AutoSave = autoSave ?? false,
            EngineConfig = new EngineConfiguration()
        };

        return config;
    }

    public static EngineConfigurationFile Load(string? configFileName = null, bool? autoSave = null)
    {
        var configFile = configFileName ?? _defaultConfigFileName;

        var configRoot = new ConfigurationBuilder()
            .AddJsonFile(configFile, optional: true, reloadOnChange: true)
            .Build();

        var settings = configRoot.GetSection(nameof(EngineConfig)).Get<EngineConfiguration>();
        return new EngineConfigurationFile
        {
            FileName = configFile,
            AutoSave = autoSave ?? false,
            EngineConfig = settings ?? new EngineConfiguration()
        };
    }

    [JsonIgnore]
    public string FileName { get; set; } = _defaultConfigFileName;

    [JsonIgnore]
    public bool AutoSave { get; set; } = false;

    public EngineConfiguration EngineConfig { get; private set; } = new();

    public void Save()
    {
        Save(FileName);
    }

    public void Save(string jsonPath)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);
    }

    public void Dispose()
    {
        if (AutoSave)
            Save();
    }
}
