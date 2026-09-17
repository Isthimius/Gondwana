using System.Drawing;

namespace Gondwana.Widgets.Menus;

/// <summary>
/// Defines the dimensions and colors used by menu bar widgets.
/// </summary>
public sealed record MenuBarTheme
{
    /// <summary>Gets the default Gondwana menu theme.</summary>
    public static MenuBarTheme Default { get; } = new();

    public Color BarBackgroundColor { get; init; } = Color.FromArgb(255, 44, 44, 52);
    public Color BarBorderColor { get; init; } = Color.FromArgb(255, 92, 92, 104);

    public Color HeaderNormalColor { get; init; } = Color.Transparent;
    public Color HeaderHoverColor { get; init; } = Color.FromArgb(255, 68, 68, 80);
    public Color HeaderPressedColor { get; init; } = Color.FromArgb(255, 52, 52, 62);
    public Color HeaderOpenColor { get; init; } = Color.FromArgb(255, 76, 76, 90);

    public Color DropDownBackgroundColor { get; init; } = Color.FromArgb(250, 40, 40, 48);
    public Color DropDownBorderColor { get; init; } = Color.FromArgb(255, 116, 116, 132);

    public Color ItemNormalColor { get; init; } = Color.Transparent;
    public Color ItemHoverColor { get; init; } = Color.FromArgb(255, 72, 72, 88);
    public Color ItemPressedColor { get; init; } = Color.FromArgb(255, 54, 54, 66);

    public Color TextColor { get; init; } = Color.White;
    public Color DisabledTextColor { get; init; } = Color.FromArgb(255, 138, 138, 148);
    public Color ShortcutTextColor { get; init; } = Color.FromArgb(255, 194, 194, 204);
    public Color SeparatorColor { get; init; } = Color.FromArgb(255, 92, 92, 104);

    public int MinimumHeaderWidth { get; init; } = 48;
    public int HeaderHorizontalPadding { get; init; } = 14;
    public float EstimatedGlyphWidth { get; init; } = 8.5f;

    public int DefaultDropDownWidth { get; init; } = 220;
    public int MinimumDropDownWidth { get; init; } = 140;
    public int DropDownHorizontalPadding { get; init; } = 5;
    public int DropDownVerticalPadding { get; init; } = 5;

    public int ItemHeight { get; init; } = 28;
    public int ItemHorizontalPadding { get; init; } = 10;
    public int ShortcutGap { get; init; } = 24;
    /// <summary>Gets the check/radio column width when any row is checkable.</summary>
    public int MarkerColumnWidth { get; init; } = 24;
    /// <summary>Gets the icon size in pixels.</summary>
    public int IconSize { get; init; } = 18;
    /// <summary>Gets the gap after the icon column.</summary>
    public int IconGap { get; init; } = 6;
    /// <summary>Gets the submenu arrow column width.</summary>
    public int SubMenuArrowWidth { get; init; } = 20;
    /// <summary>Gets spacing between parent and child popup edges.</summary>
    public int SubMenuGap { get; init; } = 0;
    /// <summary>Gets the size of check/radio/arrow geometry in pixels.</summary>
    public int IndicatorSize { get; init; } = 14;
    /// <summary>Gets the stroke width of checkmarks and submenu arrows.</summary>
    public float IndicatorStrokeWidth { get; init; } = 1.8f;
    /// <summary>Gets the enabled check/radio/arrow color.</summary>
    public Color IndicatorColor { get; init; } = Color.White;

    public int SeparatorHeight { get; init; } = 9;

    public float FontSize { get; init; } = 15f;
    public float MinimumFontSize { get; init; } = 10f;
    public float BorderWidth { get; init; } = 1f;
    public float CornerRadius { get; init; } = 3f;
}
