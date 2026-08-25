namespace Gondwana.Effects;

/// <summary>
/// Specifies the direction in which a directional display effect travels.
/// </summary>
public enum EffectDirection
{
    /// <summary>No direction is specified.</summary>
    None,

    /// <summary>The effect travels from the left edge toward the right edge.</summary>
    FromLeftToRight,

    /// <summary>The effect travels from the right edge toward the left edge.</summary>
    FromRightToLeft,

    /// <summary>The effect travels from the top edge toward the bottom edge.</summary>
    FromTopToBottom,

    /// <summary>The effect travels from the bottom edge toward the top edge.</summary>
    FromBottomToTop,

    /// <summary>The effect travels from the upper-left corner toward the lower-right corner.</summary>
    FromTopLeftToBottomRight,

    /// <summary>The effect travels from the upper-right corner toward the lower-left corner.</summary>
    FromTopRightToBottomLeft,

    /// <summary>The effect travels from the lower-left corner toward the upper-right corner.</summary>
    FromBottomLeftToTopRight,

    /// <summary>The effect travels from the lower-right corner toward the upper-left corner.</summary>
    FromBottomRightToTopLeft
}
