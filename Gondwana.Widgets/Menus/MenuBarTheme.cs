using System.Drawing;

namespace Gondwana.Widgets.Menus;

/// <summary>
/// Defines the dimensions and colors used by menu bar widgets.
/// </summary>
public sealed record MenuBarTheme
{
    /// <summary>Gets the default Gondwana menu theme.</summary>
    public static MenuBarTheme Default { get; } = new();

    /// <summary>
    /// Gets or sets the bar background color.
    /// </summary>
    public Color BarBackgroundColor { get; init; } = Color.FromArgb(255, 44, 44, 52);
    /// <summary>
    /// Gets or sets the bar border color.
    /// </summary>
    public Color BarBorderColor { get; init; } = Color.FromArgb(255, 92, 92, 104);

    /// <summary>
    /// Gets or sets the menu header color in its normal state.
    /// </summary>
    public Color HeaderNormalColor { get; init; } = Color.Transparent;
    /// <summary>
    /// Gets or sets the menu header color while the pointer is over it.
    /// </summary>
    public Color HeaderHoverColor { get; init; } = Color.FromArgb(255, 68, 68, 80);
    /// <summary>
    /// Gets or sets the menu header color while pressed.
    /// </summary>
    public Color HeaderPressedColor { get; init; } = Color.FromArgb(255, 52, 52, 62);
    /// <summary>
    /// Gets or sets the menu header color while its menu is open.
    /// </summary>
    public Color HeaderOpenColor { get; init; } = Color.FromArgb(255, 76, 76, 90);

    /// <summary>
    /// Gets or sets the drop down background color.
    /// </summary>
    public Color DropDownBackgroundColor { get; init; } = Color.FromArgb(250, 40, 40, 48);
    /// <summary>
    /// Gets or sets the drop down border color.
    /// </summary>
    public Color DropDownBorderColor { get; init; } = Color.FromArgb(255, 116, 116, 132);

    /// <summary>
    /// Gets or sets the menu item color in its normal state.
    /// </summary>
    public Color ItemNormalColor { get; init; } = Color.Transparent;
    /// <summary>
    /// Gets or sets the menu item color while the pointer is over it.
    /// </summary>
    public Color ItemHoverColor { get; init; } = Color.FromArgb(255, 72, 72, 88);
    /// <summary>
    /// Gets or sets the menu item color while pressed.
    /// </summary>
    public Color ItemPressedColor { get; init; } = Color.FromArgb(255, 54, 54, 66);

    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public Color TextColor { get; init; } = Color.White;
    /// <summary>
    /// Gets or sets the disabled text color.
    /// </summary>
    public Color DisabledTextColor { get; init; } = Color.FromArgb(255, 138, 138, 148);
    /// <summary>
    /// Gets or sets the shortcut text color.
    /// </summary>
    public Color ShortcutTextColor { get; init; } = Color.FromArgb(255, 194, 194, 204);
    /// <summary>
    /// Gets or sets the separator color.
    /// </summary>
    public Color SeparatorColor { get; init; } = Color.FromArgb(255, 92, 92, 104);

    /// <summary>
    /// Gets or sets the minimum header width.
    /// </summary>
    public int MinimumHeaderWidth { get; init; } = 48;
    /// <summary>
    /// Gets or sets the header horizontal padding.
    /// </summary>
    public int HeaderHorizontalPadding { get; init; } = 14;
    /// <summary>
    /// Gets or sets the glyph width estimate used for menu sizing.
    /// </summary>
    public float EstimatedGlyphWidth { get; init; } = 8.5f;

    /// <summary>
    /// Gets or sets the default drop down width.
    /// </summary>
    public int DefaultDropDownWidth { get; init; } = 220;
    /// <summary>
    /// Gets or sets the minimum drop down width.
    /// </summary>
    public int MinimumDropDownWidth { get; init; } = 140;
    /// <summary>
    /// Gets or sets the drop down horizontal padding.
    /// </summary>
    public int DropDownHorizontalPadding { get; init; } = 5;
    /// <summary>
    /// Gets or sets the drop down vertical padding.
    /// </summary>
    public int DropDownVerticalPadding { get; init; } = 5;

    /// <summary>
    /// Gets or sets the item height.
    /// </summary>
    public int ItemHeight { get; init; } = 28;
    /// <summary>
    /// Gets or sets the item horizontal padding.
    /// </summary>
    public int ItemHorizontalPadding { get; init; } = 10;
    /// <summary>
    /// Gets or sets the space between a menu label and its shortcut text.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the separator height.
    /// </summary>
    public int SeparatorHeight { get; init; } = 9;

    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    public float FontSize { get; init; } = 15f;
    /// <summary>
    /// Gets or sets the minimum font size.
    /// </summary>
    public float MinimumFontSize { get; init; } = 10f;
    /// <summary>
    /// Gets or sets the border width.
    /// </summary>
    public float BorderWidth { get; init; } = 1f;
    /// <summary>
    /// Gets or sets the corner radius.
    /// </summary>
    public float CornerRadius { get; init; } = 3f;
}
