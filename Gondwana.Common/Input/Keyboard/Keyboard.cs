using Gondwana.Input.EventArgs;
using Gondwana.Timers;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Gondwana.Input.Keyboard;

public static class Keyboard
{
    #region Win32 p/invoke
    [DllImport("user32.dll")]
    internal static extern short GetKeyState(Keys nVirtKey);

    [DllImport("user32.dll")]
    internal static extern bool GetAsyncKeyState(Keys vKey);
    #endregion

    #region class fields
    private static List<KeyEventConfiguration> keyConfigs = new List<KeyEventConfiguration>();
    private static bool allPause = false;
    #endregion

    #region event declarations
    public delegate void KeyDownEventHandler(KeyDownEventArgs e);
    public static event KeyDownEventHandler KeyDown;
    #endregion

    #region public static properties
    public static long DefaultTicksBetweenKeyEvents { get; set; }
    #endregion

    #region public static ctor
    static Keyboard()
    {
        DefaultTicksBetweenKeyEvents = 0;
    }
    #endregion

    #region utility methods
    public static bool GetKeyToggleState(Keys key)
    {
        return (GetKeyState(key) == 1);
    }

    public static bool GetKeyDownState(Keys key)
    {
        return (GetAsyncKeyState(key));
    }
    #endregion

    #region methods for key event configuration
    public static void StartMonitoringKey(Keys key)
    {
        StartMonitoringKey(key, (double)DefaultTicksBetweenKeyEvents / (double)HighResTimer.TicksPerSecond);
    }

    public static void StartMonitoringKey(Keys key, double timeBetweenEvents)
    {
        StartMonitoringKey(GetNewConfigurationFromKey(key, timeBetweenEvents));
    }

    public static void StartMonitoringKey(KeyEventConfiguration key)
    {
        for (int i = 0; i < keyConfigs.Count; i++)
        {
            // if the current Keys value is being monitored...
            if (keyConfigs[i].Key == key.Key)
            {
                keyConfigs[i] = key;
                return;
            }
        }

        // if code gets here, Keys value not being monitored yet
        keyConfigs.Add(key);
    }

    public static void StartMonitoringKey(List<Keys> keys)
    {
        foreach (Keys key in keys)
            StartMonitoringKey(key);
    }

    public static void StartMonitoringKey(List<KeyEventConfiguration> keys)
    {
        foreach (KeyEventConfiguration key in keys)
            StartMonitoringKey(key);
    }

    public static void StopMonitoringKey(Keys key)
    {
        for (int i = 0; i < keyConfigs.Count; i++)
        {
            if (keyConfigs[i].Key == key)
                keyConfigs.Remove(keyConfigs[i]);
        }
    }

    public static void StopMonitoringAllKeys()
    {
        keyConfigs.Clear();
    }

    public static void SetTimeBetweenEvents(Keys key, double timeBetweenEvents)
    {
        KeyEventConfiguration keycfg;

        for (int i = 0; i < keyConfigs.Count; i++)
        {
            // if the current Keys value is being monitored...
            if (keyConfigs[i].Key == key)
            {
                keycfg = keyConfigs[i];
                keycfg.TimeBetweenEvents = timeBetweenEvents;
                keyConfigs[i] = keycfg;
                return;
            }
        }

        // if code gets here, Keys value not being monitored yet
        keycfg = GetNewConfigurationFromKey(key, timeBetweenEvents);
        keyConfigs.Add(keycfg);
    }

    public static void SetTimeBetweenEvents(KeyEventConfiguration key, double timeBetweenEvents)
    {
        StartMonitoringKey(key);
        SetTimeBetweenEvents(key.Key, timeBetweenEvents);
    }

    public static void SetKeyEventPause(Keys key, bool paused)
    {
        KeyEventConfiguration keycfg;

        for (int i = 0; i < keyConfigs.Count; i++)
        {
            // if the current Keys value is being monitored...
            if (keyConfigs[i].Key == key)
            {
                keycfg = keyConfigs[i];
                keycfg.Paused = paused;
                keyConfigs[i] = keycfg;
                return;
            }
        }

        // if code gets here, Keys value not being monitored yet
        keycfg = GetNewConfigurationFromKey(key, DefaultTicksBetweenKeyEvents);
        keycfg.Paused = paused;
        keyConfigs.Add(keycfg);
    }

    public static void SetKeyEventPause(KeyEventConfiguration key, bool paused)
    {
        StartMonitoringKey(key);
        SetKeyEventPause(key.Key, paused);
    }

    public static bool PauseAllKeyEvents
    {
        get { return allPause; }
        set { allPause = value; }
    }
    #endregion

    #region private methods
    private static KeyEventConfiguration GetNewConfigurationFromKey(Keys key, double timeBetweenEvents)
    {
        return new KeyEventConfiguration(key, timeBetweenEvents, false);
    }
    #endregion

    #region public methods
    public static void RaiseKeyEvents(long tick)
    {
        if (KeyDown != null)
        {
            if (!allPause)
            {
                for (int i = 0; i < keyConfigs.Count; i++)
                {
                    if (keyConfigs[i].ReadyForNextEvent(tick))
                    {
                        if (GetAsyncKeyState(keyConfigs[i].Key))
                        {
                            KeyEventConfiguration key = keyConfigs[i];
                            key.LastKeyEvent = tick;
                            keyConfigs[i] = key;
                            KeyDown(new KeyDownEventArgs(keyConfigs[i]));
                        }
                    }
                }
            }
        }
    }
    #endregion
}
