namespace Gondwana.Tooling.WinForms.Theming;

internal sealed class ThemeStripRenderer(ToolingPalette palette) : ToolStripProfessionalRenderer(new ThemeColorTable(palette))
{
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = !e.Item.Enabled ? palette.DisabledForeground : palette.Foreground;
        base.OnRenderItemText(e);
    }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == false ? palette.DisabledForeground : palette.Foreground;
        base.OnRenderArrow(e);
    }
}

internal sealed class ThemeColorTable(ToolingPalette p) : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => p.Surface;
    public override Color ToolStripBorder => p.Border;
    public override Color MenuBorder => p.Border;
    public override Color MenuItemBorder => p.Accent;
    public override Color MenuItemSelected => p.Hover;
    public override Color MenuItemSelectedGradientBegin => p.Hover;
    public override Color MenuItemSelectedGradientEnd => p.Hover;
    public override Color MenuItemPressedGradientBegin => p.Hover;
    public override Color MenuItemPressedGradientMiddle => p.Hover;
    public override Color MenuItemPressedGradientEnd => p.Hover;
    public override Color MenuStripGradientBegin => p.Surface;
    public override Color MenuStripGradientEnd => p.Surface;
    public override Color ImageMarginGradientBegin => p.Surface;
    public override Color ImageMarginGradientMiddle => p.Surface;
    public override Color ImageMarginGradientEnd => p.Surface;
    public override Color ToolStripGradientBegin => p.Surface;
    public override Color ToolStripGradientMiddle => p.Surface;
    public override Color ToolStripGradientEnd => p.Surface;
    public override Color ToolStripPanelGradientBegin => p.Surface;
    public override Color ToolStripPanelGradientEnd => p.Surface;
    public override Color StatusStripGradientBegin => p.Surface;
    public override Color StatusStripGradientEnd => p.Surface;
    public override Color ButtonSelectedHighlight => p.Hover;
    public override Color ButtonSelectedBorder => p.Accent;
    public override Color ButtonSelectedGradientBegin => p.Hover;
    public override Color ButtonSelectedGradientMiddle => p.Hover;
    public override Color ButtonSelectedGradientEnd => p.Hover;
    public override Color ButtonPressedGradientBegin => p.Hover;
    public override Color ButtonPressedGradientMiddle => p.Hover;
    public override Color ButtonPressedGradientEnd => p.Hover;
    public override Color ButtonCheckedGradientBegin => p.Hover;
    public override Color ButtonCheckedGradientMiddle => p.Hover;
    public override Color ButtonCheckedGradientEnd => p.Hover;
    public override Color ButtonCheckedHighlight => p.Hover;
    public override Color ButtonCheckedHighlightBorder => p.Accent;
    public override Color CheckBackground => p.Hover;
    public override Color CheckSelectedBackground => p.Hover;
    public override Color CheckPressedBackground => p.Hover;
    public override Color SeparatorDark => p.Border;
    public override Color SeparatorLight => p.Surface;
    public override Color GripDark => p.Border;
    public override Color GripLight => p.Surface;
    public override Color OverflowButtonGradientBegin => p.Surface;
    public override Color OverflowButtonGradientMiddle => p.Surface;
    public override Color OverflowButtonGradientEnd => p.Surface;
}
