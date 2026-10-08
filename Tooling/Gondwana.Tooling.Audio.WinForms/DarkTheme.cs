namespace Gondwana.Tooling.Audio.WinForms;

internal static class DarkTheme
{
    /// <summary>
    /// The background.
    /// </summary>
    public static readonly Color Background = Color.FromArgb(30, 30, 30);
    /// <summary>
    /// The surface.
    /// </summary>
    public static readonly Color Surface = Color.FromArgb(45, 45, 48);
    /// <summary>
    /// The foreground.
    /// </summary>
    public static readonly Color Foreground = Color.Gainsboro;

    /// <summary>
    /// Applies the editor color theme to the control and its children.
    /// </summary>
    /// <param name="control">The control.</param>
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

        if (control is ToolStrip strip)
            strip.Renderer = new ToolStripProfessionalRenderer(new Palette());

        if (control is Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Surface;
        }

        foreach (Control child in control.Controls)
            Apply(child);
    }

    private sealed class Palette : ProfessionalColorTable
    {
        /// <inheritdoc/>
        public override Color ToolStripDropDownBackground => Surface;
        /// <inheritdoc/>
        public override Color MenuItemSelected => Color.FromArgb(65, 65, 70);
        /// <inheritdoc/>
        public override Color MenuItemBorder => Color.DimGray;
        /// <inheritdoc/>
        public override Color MenuItemSelectedGradientBegin => Surface;
        /// <inheritdoc/>
        public override Color MenuItemSelectedGradientEnd => Surface;
        /// <inheritdoc/>
        public override Color MenuItemPressedGradientBegin => Surface;
        /// <inheritdoc/>
        public override Color MenuItemPressedGradientEnd => Surface;
        /// <inheritdoc/>
        public override Color ImageMarginGradientBegin => Surface;
        /// <inheritdoc/>
        public override Color ImageMarginGradientMiddle => Surface;
        /// <inheritdoc/>
        public override Color ImageMarginGradientEnd => Surface;
        /// <inheritdoc/>
        public override Color ToolStripGradientBegin => Surface;
        /// <inheritdoc/>
        public override Color ToolStripGradientMiddle => Surface;
        /// <inheritdoc/>
        public override Color ToolStripGradientEnd => Surface;
    }
}
