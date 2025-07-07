using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gondwana.Configuration;

public partial class EngineConfigurationFile : IDisposable
{
    private const string _defaultConfigFileName = "gondwana.json";
    private string _fileName = _defaultConfigFileName;

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
    public string FileName
    {
        get => Path.GetFileName(_fileName);
        private set => _fileName = Path.GetFullPath(value);
    }

    [JsonIgnore]
    public string FilePath => Path.GetFullPath(_fileName);

    [JsonIgnore]
    public bool AutoSave { get; set; } = false;

    public EngineConfiguration EngineConfig { get; private set; } = new();

    public void Save()
    {
        Save(FilePath);
    }

    public void Save(string jsonPath)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);
        FileName = jsonPath;
    }

    public void Dispose()
    {
        if (AutoSave)
            Save();
    }
}
