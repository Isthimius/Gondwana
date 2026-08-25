namespace Gondwana.Effects;

/// <summary>
/// Describes the current lifecycle state of a display effect.
/// </summary>
public enum EffectStatus
{
    /// <summary>The effect has been created but has not been started.</summary>
    Pending,

    /// <summary>The effect is actively advancing.</summary>
    Running,

    /// <summary>The effect reached its requested duration.</summary>
    Completed,

    /// <summary>The effect was cancelled before completing.</summary>
    Cancelled
}
