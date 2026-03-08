using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Gondwana.Configuration;

/// <summary>
/// Represents a configuration file for the engine, providing functionality to create, load, and save engine settings.
/// </summary>
/// <remarks>This class manages the persistence of <see cref="EngineConfiguration"/> to and from a JSON file.
/// It supports automatic saving on disposal and can be created from scratch or loaded from an existing file.</remarks>
[JsonObject(IsReference = true)]
public partial class EngineConfigurationFile : IDisposable
{
    private const string _defaultConfigFileName = "gondwana.json";
    private string _fileName = _defaultConfigFileName;

    private EngineConfigurationFile()
    { }

    /// <summary>
    /// Creates a new engine configuration file with default settings.
    /// </summary>
    /// <param name="configFileName">The name of the configuration file to create. If <see langword="null"/>, uses the default file name "gondwana.json".</param>
    /// <param name="autoSave">A value indicating whether the configuration should be automatically saved when disposed. If <see langword="null"/>, defaults to <see langword="false"/>.</param>
    /// <returns>A new <see cref="EngineConfigurationFile"/> instance with default engine configuration settings.</returns>
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

    /// <summary>
    /// Loads an engine configuration file from disk.
    /// </summary>
    /// <remarks>If the specified file does not exist or cannot be read, a new configuration with default settings
    /// is created. The configuration file is monitored for changes and will reload automatically when modified.</remarks>
    /// <param name="configFileName">The name of the configuration file to load. If <see langword="null"/>, uses the default file name "gondwana.json".</param>
    /// <param name="autoSave">A value indicating whether the configuration should be automatically saved when disposed. If <see langword="null"/>, defaults to <see langword="false"/>.</param>
    /// <returns>An <see cref="EngineConfigurationFile"/> instance loaded from the specified file, or a new instance with default settings if the file doesn't exist.</returns>
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

    /// <summary>
    /// Gets the name of the configuration file without the directory path.
    /// </summary>
    [JsonIgnore]
    public string FileName
    {
        get => Path.GetFileName(_fileName);
        private set => _fileName = Path.GetFullPath(value);
    }

    /// <summary>
    /// Gets the full path to the configuration file.
    /// </summary>
    [JsonIgnore]
    public string FilePath => Path.GetFullPath(_fileName);

    /// <summary>
    /// Gets or sets a value indicating whether the configuration should be automatically saved when the instance is disposed.
    /// </summary>
    /// <remarks>When set to <see langword="true"/>, the <see cref="Save()"/> method is automatically called during disposal.</remarks>
    [JsonIgnore]
    public bool AutoSave { get; set; } = false;

    /// <summary>
    /// Gets the engine configuration settings.
    /// </summary>
    public EngineConfiguration EngineConfig { get; private set; } = new();

    /// <summary>
    /// Saves the configuration to the file specified by <see cref="FilePath"/>.
    /// </summary>
    /// <remarks>The configuration is serialized to JSON with indented formatting for readability.</remarks>
    public void Save()
    {
        Save(FilePath);
    }

    /// <summary>
    /// Saves the configuration to the specified file path.
    /// </summary>
    /// <remarks>The configuration is serialized to JSON with indented formatting. After saving, the <see cref="FileName"/>
    /// property is updated to reflect the new path.</remarks>
    /// <param name="jsonPath">The full path where the configuration file should be saved.</param>
    public void Save(string jsonPath)
    {
        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(jsonPath, json);
        FileName = jsonPath;
    }

    /// <summary>
    /// Releases all resources used by the <see cref="EngineConfigurationFile"/>.
    /// </summary>
    /// <remarks>If <see cref="AutoSave"/> is <see langword="true"/>, the configuration is automatically saved before disposal.</remarks>
    public void Dispose()
    {
        if (AutoSave)
            Save();
    }
}