using System.Configuration;

namespace Gondwana.Configuration;

public class EngineConfiguration : ConfigurationSection
{
    public const string ConfigSectionName = "Gondwana";

    #region fields
    private static string spath;
    private static EngineConfiguration instance = null;
    #endregion

    #region public static methods
    ///<summary>
    ///Get this configuration set from the application's default config file
    ///</summary>
    public static EngineConfiguration Open()
    {
        var assm = System.Reflection.Assembly.GetEntryAssembly();
        return Open(assm.Location);
    }

    ///<summary>
    /// Get this configuration set from a specific config file
    ///</summary>
    public static EngineConfiguration Open(string path)
    {
        if ((object)instance == null)
        {
            if (path.EndsWith(".config", StringComparison.InvariantCultureIgnoreCase))
                spath = path.Remove(path.Length - 7);
            else
                spath = path;

            System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(spath);
            if (config.Sections[ConfigSectionName] == null)
            {
                instance = new EngineConfiguration();
                config.Sections.Add(ConfigSectionName, instance);
                config.Save(ConfigurationSaveMode.Full);
            }
            else
                instance = (EngineConfiguration)config.Sections[ConfigSectionName];
        }

        return instance;
    }
    #endregion

    #region public methods
    ///<summary>
    ///Save the current property values to the config file
    ///</summary>
    public void Save()
    {
        Save(ConfigurationSaveMode.Full, spath);
    }

    public void Save(ConfigurationSaveMode saveMode)
    {
        Save(saveMode, spath);
    }

    public void Save(string path)
    {
        Save(ConfigurationSaveMode.Full, path);
    }

    public void Save(ConfigurationSaveMode saveMode, string path)
    {
        if (path == spath)
            this.CurrentConfiguration.Save(saveMode);
        else
            this.CurrentConfiguration.SaveAs(path, saveMode);
    }
    #endregion

    #region public properties
    /// <summary>
    /// Path of config file holding current <see cref="EngineConfiguration"/> values
    /// </summary>
    public string ConfigPath
    {
        get { return this.CurrentConfiguration.FilePath; }
    }

    [ConfigurationProperty("Settings", IsRequired = true)]
    public EngineSettings Settings
    {
        get { return (EngineSettings)this["Settings"]; }
        set { this["Settings"] = value; }
    }

    [ConfigurationProperty("StateFiles", IsRequired = false, IsDefaultCollection = true)]
    public EngineStateFiles StateFiles
    {
        get { return (EngineStateFiles)this["StateFiles"]; }
        set { this["StateFiles"] = value; }
    }
    #endregion
}
