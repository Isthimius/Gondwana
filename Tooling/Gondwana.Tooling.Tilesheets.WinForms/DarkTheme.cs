namespace Gondwana.Tooling.Tilesheets.WinForms;

internal static class DarkTheme
{
    public static readonly Color Background = Color.FromArgb(30, 30, 30);
    public static readonly Color Surface = Color.FromArgb(45, 45, 48);
    public static readonly Color Foreground = Color.Gainsboro;
    public static void Apply(Control control)
    {
        control.BackColor = Background;
        control.ForeColor = Foreground;
        if (control is PropertyGrid grid)
        {
            grid.ViewBackColor = Background;
            grid.ViewForeColor = Foreground;
            grid.HelpBackColor = Surface;
            grid.HelpForeColor = Foreground;
            grid.CategoryForeColor = Foreground;
            grid.CategorySplitterColor = Surface;
            grid.LineColor = Surface;
            grid.DisabledItemForeColor = Color.Silver;
            grid.ViewBorderColor = Surface;
        }
        if (control is ToolStrip strip) strip.Renderer = new ToolStripProfessionalRenderer(new Palette());
        if (control is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
        if (control is Button button) { button.FlatStyle = FlatStyle.Flat; button.BackColor = Surface; }
        foreach (Control child in control.Controls) Apply(child);
    }

    private sealed class Palette : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color MenuItemSelected => Color.FromArgb(65, 65, 70);
        public override Color MenuItemBorder => Color.DimGray;
        public override Color MenuItemSelectedGradientBegin => Surface;
        public override Color MenuItemSelectedGradientEnd => Surface;
        public override Color MenuItemPressedGradientBegin => Surface;
        public override Color MenuItemPressedGradientEnd => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color ToolStripGradientBegin => Surface;
        public override Color ToolStripGradientMiddle => Surface;
        public override Color ToolStripGradientEnd => Surface;
    }
}
