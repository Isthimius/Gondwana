using Gondwana.Common.Exceptions;
using Gondwana.Timers;
using System.Configuration;
using System.Xml;

namespace Gondwana;

public static class Settings
{
    #region private fields
    private static int targetFPS;
    private static double cpsSampling;
    private static double minKeyboardTime;
    private static double visibleSurfaceRefreshTimer;
    private static int resizedFrameCacheLimit;
    #endregion

    #region static constructor
    static Settings()
    {
        targetFPS = InitializeSetting<int>("TargetFPS", ref targetFPS);
        SamplingTimeForCPS = InitializeSetting<double>("SamplingTimeForCPS", ref cpsSampling);
        minKeyboardTime = InitializeSetting<double>("TimeBetweenKeyboardEvents", ref minKeyboardTime);
        VisibleSurfaceRefreshTimer = InitializeSetting<double>("VisibleSurfaceRefreshTimer", ref visibleSurfaceRefreshTimer);
        ResizedFrameCacheLimit = InitializeSetting<int>("ResizedFrameCacheLimit", ref resizedFrameCacheLimit);
    }
    #endregion

    #region public properties
    /// <summary>
    /// Target screen refresh rate for the Engine.  Setting the number
    /// lower allows more time for the processor to perform background
    /// Engine tasks.  Set the value to 0 for no upper limit.
    /// </summary>
    public static int TargetFPS
    {
        get
        {
            if (targetFPS < 0)
                targetFPS = 0;

            return targetFPS;
        }
        set
        {
            if (value < 0) { targetFPS = 0; }
            else { targetFPS = value; }
        }
    }

    /// <summary>
    /// Total number of seconds between Cycles Per Second (CPS) calculation
    /// </summary>
    public static double SamplingTimeForCPS
    {
        get { return cpsSampling; }
        set
        {
            cpsSampling = value;
            SamplingTimeForCPSTicks = (long)(cpsSampling * (double)HighResTimer.TicksPerSecond);
        }
    }

    /// <summary>
    /// Total number of system ticks between each CPS sampling
    /// </summary>
    public static long SamplingTimeForCPSTicks
    {
        get;
        private set;
    }

    /// <summary>
    /// Minimum time (in seconds) allowed between Keyboard events.
    /// </summary>
    public static double TimeBetweenKeyboardEvents
    {
        get { return minKeyboardTime; }
        set { minKeyboardTime = value; }
    }

    /// <summary>
    /// Time in seconds of forced refresh of entire area of all VisibleSurface instances
    /// </summary>
    public static double VisibleSurfaceRefreshTimer
    {
        get { return visibleSurfaceRefreshTimer; }
        set
        {
            visibleSurfaceRefreshTimer = value;
            VisibleSurfaces.ForcedRefreshRate = value;
        }
    }

    /// <summary>
    /// Total number of resized Frame stretched renderings allowed in cache.  Lowering this value may degrade performance, but lessen required system memory.
    /// </summary>
    public static int ResizedFrameCacheLimit
    {
        get { return resizedFrameCacheLimit; }
        set { resizedFrameCacheLimit = value; }
    }
    #endregion

    #region public methods
    /// <summary>
    /// Writes the current configuration settings to the current app.config file.
    /// </summary>
    public static void SaveCurrentSettings()
    {
        XmlDocument configFile = new XmlDocument();
        string configPath = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath;
        configFile.Load(configPath);
        
        // TODO: oops, missed this...
        UpdateSetting(configFile, "default_throttle", targetFPS.ToString());
        UpdateSetting(configFile, "cps_sample_time", cpsSampling.ToString());
        UpdateSetting(configFile, "seconds_between_key_events", minKeyboardTime.ToString());
        UpdateSetting(configFile, "visible_surface_refresh_timer", visibleSurfaceRefreshTimer.ToString());

        configFile.Save(configPath);
    }
    #endregion

    #region private methods
    private static T InitializeSetting<T>(string settingName, ref T settingValue) where T : IConvertible
    {
        T setVal;

        try
        {
            setVal = (T)Convert.ChangeType(ConfigurationManager.AppSettings[settingName], typeof(T));
            return setVal;
        }
        catch (Exception e)
        {
            string err = "Could not find key/value '" + settingName + "' with type "
                + typeof(T).ToString() + " in appSettings section of config file.";
            throw new GondwanaSettingException(err, e);
        }
    }

    private static void UpdateSetting(XmlDocument configFile, string settingName, string settingValue)
    {
        // find the node in the config file that has the settingName
        XmlNode node = configFile.SelectSingleNode(
            "//configuration/appSettings/add[@key = '" + settingName + "']");

        if (node != null)
        {
            // get the Value attribute of the settingName Node
            XmlNode valueAttribute = node.Attributes.GetNamedItem("value");

            // update the actual xml
            valueAttribute.InnerXml = settingValue;
        }

        // update the cached config setting, just in case the consuming app references
        // ConfigurationManager to read the settings instead of this Settings class
        ConfigurationManager.AppSettings.Set(settingName, settingValue);
    }
    #endregion
}
