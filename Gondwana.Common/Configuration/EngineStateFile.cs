using System.Configuration;

namespace Gondwana.Configuration;

public class EngineStateFile : ConfigurationElement
{
    /// <summary>
    /// Unique identifier for entry
    /// </summary>
    [ConfigurationProperty("ID", IsRequired = true, IsKey = true)]
    public string ID
    {
        get { return (string)this["ID"]; }
        set { this["ID"] = value; }
    }

    /// <summary>
    /// Path to a serialized <see cref="Gondwana.Common.EngineState"/> instance
    /// </summary>
    [ConfigurationProperty("Path", IsRequired = true)]
    public string Path
    {
        get { return (string)this["Path"]; }
        set { this["Path"] = value; }
    }

    [ConfigurationProperty("IsBinary", IsRequired = false, DefaultValue = "false")]
    public bool IsBinary
    {
        get { return (bool)this["IsBinary"]; }
        set { this["IsBinary"] = value; }
    }

    [ConfigurationProperty("LoadAtStartup", IsRequired = false, DefaultValue = true)]
    public bool LoadAtStartup
    {
        get { return (bool)base["LoadAtStartup"]; }
        set { base["LoadAtStartup"] = value; }
    }
}
