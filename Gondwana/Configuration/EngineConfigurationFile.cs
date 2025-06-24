using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Gondwana.Configuration;

public class EngineConfigurationFile
{
    private const string _defaultConfigFileName = "gondwana.json";

    public string FileName { get; private set; } = _defaultConfigFileName;
    public EngineConfiguration EngineConfig { get; private set; }

    private EngineConfigurationFile() { }

    public static EngineConfigurationFile Load(string jsonPath = _defaultConfigFileName)
    {
        var configRoot = new ConfigurationBuilder()
            .AddJsonFile(jsonPath, optional: false, reloadOnChange: true)
            .Build();

        var settings = configRoot.GetSection(nameof(EngineConfiguration)).Get<EngineConfiguration>();
        return new EngineConfigurationFile { EngineConfig = settings ?? new EngineConfiguration() };
    }

    public void Save()
    {
        Save(FileName);
    }

    public void Save(string jsonPath)
    {
        var json = JsonSerializer.Serialize(EngineConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);
    }

    #region public static methods
    ///<summary>
    ///Get this configuration set from the application's default config file
    ///</summary>
    //public static EngineConfiguration Open()
    //{
    //    var assm = System.Reflection.Assembly.GetEntryAssembly();
    //    return Open(assm.Location);
    //}

    ///<summary>
    /// Get this configuration set from a specific config file
    ///</summary>
    //public static EngineConfiguration Open(string path)
    //{
    //    if (instance == null)
    //    {
    //        if (path.EndsWith(".config", StringComparison.InvariantCultureIgnoreCase))
    //            spath = path.Remove(path.Length - 7);
    //        else
    //            spath = path;

    //        System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(spath);
    //        if (config.Sections[ConfigSectionName] == null)
    //        {
    //            instance = new EngineConfiguration();
    //            config.Sections.Add(ConfigSectionName, instance);
    //            config.Save(ConfigurationSaveMode.Full);
    //        }
    //        else
    //            instance = (EngineConfiguration)config.Sections[ConfigSectionName];
    //    }

    //    return instance;
    //}
    #endregion

    #region public methods
    ///<summary>
    ///Save the current property values to the config file
    ///</summary>
    //public void Save()
    //{
    //    Save(ConfigurationSaveMode.Full, spath);
    //}

    //public void Save(ConfigurationSaveMode saveMode)
    //{
    //    Save(saveMode, spath);
    //}

    //public void Save(string path)
    //{
    //    Save(ConfigurationSaveMode.Full, path);
    //}

    //public void Save(ConfigurationSaveMode saveMode, string path)
    //{
    //    if (path == spath)
    //        CurrentConfiguration.Save(saveMode);
    //    else
    //        CurrentConfiguration.SaveAs(path, saveMode);
    //}
    #endregion

    #region public properties
    /// <summary>
    /// Path of config file holding current <see cref="EngineConfiguration"/> values
    /// </summary>
    //public string ConfigPath
    //{
    //    get { return CurrentConfiguration.FilePath; }
    //}

    //[ConfigurationProperty("Settings", IsRequired = true)]
    //public EngineSettings Settings
    //{
    //    get { return (EngineSettings)this["Settings"]; }
    //    set { this["Settings"] = value; }
    //}

    //[ConfigurationProperty("StateFiles", IsRequired = false, IsDefaultCollection = true)]
    //public EngineStateFiles StateFiles
    //{
    //    get { return (EngineStateFiles)this["StateFiles"]; }
    //    set { this["StateFiles"] = value; }
    //}
    #endregion
}
