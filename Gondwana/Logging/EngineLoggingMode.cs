namespace Gondwana.Logging;

/// <summary>
/// Specifies the logging mode for the engine.
/// </summary>
/// <remarks>This enumeration defines whether the engine logs messages synchronously or asynchronously. Use <see
/// cref="Synchronous"/> for immediate logging, which may block the calling thread,  or <see cref="Asynchronous"/> for
/// non-blocking logging that may introduce a slight delay.</remarks>
public enum EngineLoggingMode
{
    /// <summary>
    /// Specifies that logging operations are performed asynchronously.
    /// </summary>
    /// <remarks>This type or member is intended to indicate asynchronous behavior. This makes logging a
    /// "fire-and-forget" operation. May drop log records if under extremely heavy load.</remarks>
    Asynchronous = 0,

    /// <summary>
    /// Specifies that logging operations are performed synchronously.
    /// </summary>
    /// <remarks>This member indicates that the associated operation or functionality is performed
    /// synchronously,  meaning it completes its execution before returning control to the caller.</remarks>
    Synchronous = 1
}
