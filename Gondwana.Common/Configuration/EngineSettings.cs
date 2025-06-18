using Gondwana.Rendering;
using Gondwana.Timers;
using System.Configuration;

namespace Gondwana.Configuration;

/// <summary>
/// Settings used by the engine when cycling
/// </summary>
public class EngineSettings : ConfigurationElement
{
    #region ctor
    internal EngineSettings()
    {
        SamplingTimeForCPSTicks = (long)(SamplingTimeForCPS * (double)HighResTimer.TicksPerSecond);
    }
    #endregion

    #region public properties
    /// <summary>
    /// Target screen refresh rate for the Engine.  Setting the number
    /// lower allows more time for the processor to perform background
    /// Engine tasks.  Set the value to 0 for no upper limit.
    /// </summary>
    [ConfigurationProperty("TargetFPS", DefaultValue = "60")]
    public int TargetFPS
    {
        get
        {
            int targetFPS = (int)this["TargetFPS"];

            if (targetFPS < 0)
                targetFPS = 0;

            return targetFPS;
        }
        set
        {
            if (value < 0) { this["TargetFPS"] = 0; }
            else { this["TargetFPS"] = value; }
        }
    }

    /// <summary>
    /// Total number of seconds between Cycles Per Second (CPS) calculation
    /// </summary>
    [ConfigurationProperty("SamplingTimeForCPS", DefaultValue = "1.5")]
    public double SamplingTimeForCPS
    {
        get { return (double)this["SamplingTimeForCPS"]; }
        set
        {
            this["SamplingTimeForCPS"] = value;
            SamplingTimeForCPSTicks = (long)(SamplingTimeForCPS * (double)HighResTimer.TicksPerSecond);
        }
    }

    /// <summary>
    /// Total number of system ticks between each CPS sampling
    /// </summary>
    public long SamplingTimeForCPSTicks
    {
        get;
        private set;
    }

    /// <summary>
    /// Minimum time (in seconds) allowed between Keyboard events.
    /// </summary>
    [ConfigurationProperty("TimeBetweenKeyboardEvents", DefaultValue = "0.03")]
    public double TimeBetweenKeyboardEvents
    {
        get { return (double)this["TimeBetweenKeyboardEvents"]; }
        set { this["TimeBetweenKeyboardEvents"] = value; }
    }

    /// <summary>
    /// Time in seconds of forced refresh of entire area of all VisibleSurface instances
    /// </summary>
    [ConfigurationProperty("VisibleSurfaceRefreshTimer", DefaultValue = "1.5")]
    public double VisibleSurfaceRefreshTimer
    {
        get { return (double)this["VisibleSurfaceRefreshTimer"]; }
        set
        {
            this["VisibleSurfaceRefreshTimer"] = value;
            VisibleSurfaces.ForcedRefreshRate = value;
        }
    }

    /// <summary>
    /// Determines whether or not MCI errors from the winmm.dll in the <see cref="Gondwana.Media.MediaPlayer"/> class are swallowed or thrown
    /// </summary>
    [ConfigurationProperty("ThrowExceptionOnMCIError", DefaultValue = "false")]
    public bool MCIErrorsThrowExceptions
    {
        get { return (bool)this["ThrowExceptionOnMCIError"]; }
        set { this["ThrowExceptionOnMCIError"] = value; }
    }

    /// <summary>
    /// Total number of resized Frame stretched renderings allowed in cache.  Lowering this value may degrade performance, but lessen required system memory.
    /// </summary>
    [ConfigurationProperty("ResizedFrameCacheLimit", DefaultValue = "100")]
    public int ResizedFrameCacheLimit
    {
        get { return (int)this["ResizedFrameCacheLimit"]; }
        set { this["ResizedFrameCacheLimit"] = value; }
    }

    #endregion
}
