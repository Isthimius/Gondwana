namespace Gondwana.Extensibility;

public enum InitTiming
{
    /// <summary>
    /// The extension is initialized before the standard engine initialization.
    /// </summary>
    PreInit,

    /// <summary>
    /// The extension is initialized after the engine initialization.
    /// </summary>
    PostInit
}
