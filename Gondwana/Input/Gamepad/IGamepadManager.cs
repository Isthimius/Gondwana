namespace Gondwana.Input.Gamepad;

/// <summary>
/// Interface to track a collection of <see cref="IGamepadAdapter" /> gamepad adapters.
/// </summary>
public interface IGamepadManager<out T> where T : IGamepadAdapter
{
    /// <summary>
    /// Gets the list of currently connected gamepad adapters.
    /// </summary>
    IReadOnlyCollection<T> ConnectedAdapters { get; }

    /// <summary>
    /// Refreshes the collection of connected and disconnected gamepad adapters.
    /// </summary>
    /// <remarks>
    /// The engine invokes this operation on the cadence configured by
    /// <see cref="Gondwana.Configuration.EngineConfiguration.GamepadConnectionUpdateFrequencyHz"/>.
    /// Implementations should keep connection discovery separate from live controller-state polling
    /// when the underlying backend permits it.
    /// </remarks>
    void UpdateConnections()
    {
    }

    /// <summary>
    /// Polls the current state of already-connected gamepad adapters.
    /// </summary>
    /// <remarks>
    /// The engine invokes this operation on the cadence configured by
    /// <see cref="Gondwana.Configuration.EngineConfiguration.GamepadPollFrequencyHz"/>.
    /// The default implementation forwards to the legacy <see cref="Update"/> method so custom
    /// managers compiled against the older combined contract continue to refresh state.
    /// </remarks>
#pragma warning disable CS0618 // Compatibility bridge to the legacy combined operation.
    void Poll() => Update();
#pragma warning restore CS0618

    /// <summary>
    /// Refreshes connection and controller state using the legacy combined operation.
    /// </summary>
    /// <remarks>
    /// New implementations should override <see cref="UpdateConnections"/> and <see cref="Poll"/>
    /// separately. This member remains abstract for compatibility with existing custom managers;
    /// the engine no longer calls it directly.
    /// </remarks>
    [Obsolete("Implement UpdateConnections() and Poll() separately. The engine no longer calls Update() directly.")]
    void Update();
}
