namespace Gondwana.Widgets.Menus;

/// <summary>
/// Specifies how a menu dropdown is animated when opened or closed.
/// </summary>
public enum MenuDropDownAnimation
{
    /// <summary>No animation is applied.</summary>
    None,

    /// <summary>The dropdown fades between transparent and opaque.</summary>
    Fade,

    /// <summary>
    /// The dropdown fades while each retained drawing is revealed vertically when opening.
    /// </summary>
    FadeAndReveal
}
