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
    public int SeparatorHeight { get; init; } = 9;

    public float FontSize { get; init; } = 15f;
    public float MinimumFontSize { get; init; } = 10f;
    public float BorderWidth { get; init; } = 1f;
    public float CornerRadius { get; init; } = 3f;
}
