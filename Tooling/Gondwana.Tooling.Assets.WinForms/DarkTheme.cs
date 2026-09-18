namespace Gondwana.Tooling.Assets.WinForms;

internal static class DarkTheme
{
    public static readonly Color Background = Color.FromArgb(30, 30, 30);
    public static readonly Color Surface = Color.FromArgb(45, 45, 48);
    public static readonly Color Foreground = Color.Gainsboro;
    public static readonly Color Selection = Color.FromArgb(62, 62, 66);

    public static void Apply(Control control)
    {
        control.BackColor = Background;
        control.ForeColor = Foreground;

        switch (control)
        {
            case DataGridView grid:
                grid.BackgroundColor = Background;
                grid.GridColor = Surface;
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Surface;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Foreground;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Surface;
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Foreground;
                grid.DefaultCellStyle.BackColor = Background;
                grid.DefaultCellStyle.ForeColor = Foreground;
                grid.DefaultCellStyle.SelectionBackColor = Selection;
                grid.DefaultCellStyle.SelectionForeColor = Color.White;
                grid.RowHeadersDefaultCellStyle.BackColor = Surface;
                grid.RowHeadersDefaultCellStyle.ForeColor = Foreground;
                break;
            case PropertyGrid propertyGrid:
                propertyGrid.ViewBackColor = Background;
                propertyGrid.ViewForeColor = Foreground;
                propertyGrid.HelpBackColor = Surface;
                propertyGrid.HelpForeColor = Foreground;
                propertyGrid.CategoryForeColor = Foreground;
                propertyGrid.CategorySplitterColor = Surface;
                propertyGrid.LineColor = Surface;
                propertyGrid.DisabledItemForeColor = Color.Silver;
                propertyGrid.ViewBorderColor = Surface;
                break;
            case ToolStrip strip:
                strip.BackColor = Surface;
                strip.ForeColor = Foreground;
                strip.Renderer = new ToolStripProfessionalRenderer(new Palette());
                foreach (ToolStripItem item in strip.Items)
                    item.ForeColor = Foreground;
                break;
            case ComboBox combo:
                combo.BackColor = Surface;
                combo.ForeColor = Foreground;
                combo.FlatStyle = FlatStyle.Flat;
                break;
            case TextBox textBox:
                textBox.BackColor = Surface;
                textBox.ForeColor = Foreground;
                break;
            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Surface;
                break;
            case TreeView tree:
                tree.BackColor = Background;
                tree.ForeColor = Foreground;
                tree.LineColor = Color.DimGray;
                break;
        }

        foreach (Control child in control.Controls)
            Apply(child);
    }

    private sealed class Palette : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color MenuItemSelected => Selection;
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
        public override Color StatusStripGradientBegin => Surface;
        public override Color StatusStripGradientEnd => Surface;
    }
}
