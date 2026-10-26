using System.Runtime.CompilerServices;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.WinForms.Theming;

/// <summary>One immutable appearance per process. Preference changes only affect the next launch.</summary>
public static class ToolingAppearance
{
    static ToolingAppearance() { } // Read preferences on first use, not eager beforefieldinit.
    public static ToolingTheme Current { get; } = new ThemePreferenceStore().Read();
    public static ToolingPalette Palette { get; } = ToolingPalette.For(Current);
    private static readonly ConditionalWeakTable<Control, object> Applied = new();
    private static readonly ConditionalWeakTable<Control, object> Preserved = new();
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        Application.Idle += OnIdle;
        Application.ApplicationExit += (_, _) => Application.Idle -= OnIdle;
    }

    private static void OnIdle(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms.Cast<Form>().ToArray()) Apply(form);
    }

    /// <summary>Opt out rendering canvases whose colors represent assets rather than UI.</summary>
    public static void PreserveColors(Control control) => Preserved.GetValue(control, _ => new());

    public static ThemeBase CreateDockTheme() => CreateDockTheme(Current);

    public static ThemeBase CreateDockTheme(ToolingTheme theme)
    {
        if (theme == ToolingTheme.Light) return new VS2015LightTheme();
        return theme == ToolingTheme.DarkBlue ? new DarkBlueDockTheme() : new VS2015DarkTheme();
    }

    internal static void ConfigureDarkBlue(ThemeBase result)
    {
        var p = ToolingPalette.For(ToolingTheme.DarkBlue);
        var d = result.ColorPalette;
        d.MainWindowActive.Background = p.Background;
        d.TabSelectedActive.Background = p.Selection;
        d.TabSelectedActive.Text = p.SelectionForeground;
        d.TabSelectedInactive.Background = p.Surface;
        d.TabSelectedInactive.Text = p.Foreground;
        d.TabUnselected.Background = p.Background;
        d.TabUnselected.Text = p.MutedForeground;
        d.TabUnselectedHovered.Background = p.Hover;
        d.TabUnselectedHovered.Text = p.Foreground;
        d.ToolWindowCaptionActive.Background = p.Selection;
        d.ToolWindowCaptionActive.Text = p.SelectionForeground;
        d.ToolWindowCaptionInactive.Background = p.Surface;
        d.ToolWindowCaptionInactive.Text = p.Foreground;
        d.ToolWindowTabSelectedActive.Background = p.Selection;
        d.ToolWindowTabSelectedActive.Text = p.SelectionForeground;
        d.ToolWindowTabSelectedInactive.Background = p.Surface;
        d.ToolWindowTabSelectedInactive.Text = p.Foreground;
        d.ToolWindowTabUnselected.Background = p.Background;
        d.ToolWindowTabUnselected.Text = p.MutedForeground;
        d.ToolWindowTabUnselectedHovered.Background = p.Hover;
        d.ToolWindowTabUnselectedHovered.Text = p.Foreground;
        d.ToolWindowBorder = p.Border;
        d.ToolWindowSeparator = p.Border;
        d.AutoHideStripDefault.Background = p.Background;
        d.AutoHideStripDefault.Text = p.MutedForeground;
        d.AutoHideStripHovered.Background = p.Hover;
        d.AutoHideStripHovered.Text = p.Foreground;
        d.DockTarget.Background = p.Surface;
        d.DockTarget.Border = p.Border;
        d.DockTarget.ButtonBackground = p.Selection;
        d.DockTarget.ButtonBorder = p.Accent;
    }

    public static ToolStripMenuItem CreateThemeMenu(IWin32Window owner,
        ThemePreferenceStore? store = null, Action<string>? notify = null)
    {
        store ??= new ThemePreferenceStore();
        notify ??= message => ThemedMessageBox.Show(owner, message, "Gondwana theme", MessageBoxButtons.OK, MessageBoxIcon.Information);
        var menu = new ToolStripMenuItem("Theme");
        void RefreshChecks()
        {
            var selected = store.Read();
            foreach (ToolStripMenuItem item in menu.DropDownItems) item.Checked = Equals(item.Tag, selected);
        }
        foreach (var theme in Enum.GetValues<ToolingTheme>())
        {
            var item = new ToolStripMenuItem(ToolingPalette.Name(theme)) { Tag = theme };
            item.Click += (_, _) =>
            {
                if (!store.Write(theme)) { notify("The theme preference could not be saved. Check your settings folder permissions."); return; }
                RefreshChecks();
                notify(theme == Current ? "The selected theme is already active in this application."
                    : "Theme saved. Restart this application to apply it. Other Gondwana tools will use it on their next launch. Your open editors and unsaved changes have not been changed.");
            };
            menu.DropDownItems.Add(item);
        }
        menu.DropDownOpening += (_, _) => RefreshChecks();
        RefreshChecks();
        Apply(menu.DropDown);
        return menu;
    }

    public static void Apply(Control control)
    {
        if (control.IsDisposed || Preserved.TryGetValue(control, out _) || Applied.TryGetValue(control, out _)) return;
        Applied.Add(control, new());
        // DockPanel Suite owns its chrome; never overwrite its internal controls or change a live theme.
        if (control is DockPanel dock)
        {
            foreach (var content in dock.Contents.OfType<Control>()) Apply(content);
            dock.ContentAdded += (_, e) => { if (e.Content is Control child) Apply(child); };
            return;
        }
        control.ControlAdded += (_, e) => { if (e.Control is { } child) Apply(child); };
        control.ContextMenuStripChanged += (_, _) => { if (control.ContextMenuStrip is { } context) Apply(context); };
        control.EnabledChanged += (_, _) => control.ForeColor = control.Enabled ? Palette.Foreground : Palette.DisabledForeground;
        control.BackColor = Palette.Background;
        control.ForeColor = control.Enabled ? Palette.Foreground : Palette.DisabledForeground;
        if (control.ContextMenuStrip is { } context) Apply(context);
        switch (control)
        {
            case PropertyGrid grid:
                grid.ViewBackColor = Palette.Background;
                grid.ViewForeColor = Palette.Foreground;
                grid.HelpBackColor = Palette.Surface;
                grid.HelpForeColor = Palette.Foreground;
                grid.HelpBorderColor = Palette.Border;
                grid.CategoryForeColor = Palette.Foreground;
                grid.CategorySplitterColor = Palette.Surface;
                grid.LineColor = Palette.Surface;
                grid.DisabledItemForeColor = Palette.DisabledForeground;
                grid.ViewBorderColor = Palette.Border;
                grid.SelectedItemWithFocusBackColor = Palette.Selection;
                grid.SelectedItemWithFocusForeColor = Palette.SelectionForeground;
                grid.CommandsBackColor = Palette.Surface;
                grid.CommandsForeColor = Palette.Foreground;
                grid.CommandsBorderColor = Palette.Border;
                break;
            case DataGridView grid:
                grid.BackgroundColor = Palette.Background;
                grid.GridColor = Palette.Border;
                grid.EnableHeadersVisualStyles = false;
                foreach (var style in new[] { grid.DefaultCellStyle, grid.RowsDefaultCellStyle, grid.AlternatingRowsDefaultCellStyle,
                    grid.ColumnHeadersDefaultCellStyle, grid.RowHeadersDefaultCellStyle })
                {
                    style.BackColor = Palette.Background;
                    style.ForeColor = Palette.Foreground;
                    style.SelectionBackColor = Palette.Selection;
                    style.SelectionForeColor = Palette.SelectionForeground;
                }
                grid.ColumnHeadersDefaultCellStyle.BackColor = Palette.Surface;
                grid.RowHeadersDefaultCellStyle.BackColor = Palette.Surface;
                grid.EditingControlShowing += (_, e) => Apply(e.Control);
                break;
            case ToolStrip strip:
                strip.BackColor = Palette.Surface;
                strip.Renderer = new ThemeStripRenderer(Palette);
                strip.ItemAdded += (_, e) => ApplyItem(e.Item);
                foreach (ToolStripItem item in strip.Items) ApplyItem(item);
                if (strip.ShowItemToolTips)
                {
                    strip.ShowItemToolTips = false;
                    AttachToolTip(strip, point => strip.GetItemAt(point)?.ToolTipText);
                }
                break;
            case TextBoxBase or UpDownBase:
                control.BackColor = Palette.Surface;
                if (control is TextBoxBase text) text.BorderStyle = BorderStyle.FixedSingle;
                if (control is UpDownBase number) number.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox combo:
                combo.BackColor = Palette.Surface;
                combo.FlatStyle = FlatStyle.Flat;
                ComboBoxChrome.Attach(combo);
                if (combo.DrawMode == DrawMode.Normal)
                {
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.DrawItem += DrawComboItem;
                }
                break;
            case Button button:
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Palette.Surface;
                button.FlatAppearance.BorderColor = Palette.Border;
                button.FlatAppearance.MouseOverBackColor = Palette.Hover;
                button.FlatAppearance.MouseDownBackColor = Palette.Selection;
                button.GotFocus += (_, _) => button.FlatAppearance.BorderColor = Palette.Accent;
                button.LostFocus += (_, _) => button.FlatAppearance.BorderColor = Palette.Border;
                break;
            case CheckBox check:
                check.FlatStyle = FlatStyle.Flat;
                check.UseVisualStyleBackColor = false;
                break;
            case RadioButton radio:
                radio.FlatStyle = FlatStyle.Flat;
                radio.UseVisualStyleBackColor = false;
                break;
            case TreeView tree:
                tree.BorderStyle = BorderStyle.FixedSingle;
                tree.LineColor = Palette.Border;
                if (tree.ShowNodeToolTips)
                {
                    tree.ShowNodeToolTips = false;
                    AttachToolTip(tree, point => tree.GetNodeAt(point)?.ToolTipText);
                }
                if (tree.DrawMode == TreeViewDrawMode.Normal)
                {
                    tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
                    tree.DrawNode += (_, e) =>
                    {
                        if (e.Node is null) return;
                        bool selected = e.Node == tree.SelectedNode && (tree.Focused || !tree.HideSelection);
                        using var brush = new SolidBrush(selected ? Palette.Selection : Palette.Background);
                        e.Graphics.FillRectangle(brush, e.Bounds);
                        TextRenderer.DrawText(e.Graphics, e.Node.Text, tree.Font, e.Bounds,
                            selected ? Palette.SelectionForeground : tree.ForeColor, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
                        if (selected && tree.Focused) ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, Palette.SelectionForeground, Palette.Selection);
                    };
                }
                break;
            case ListView list when !list.OwnerDraw:
                list.BorderStyle = BorderStyle.FixedSingle;
                list.OwnerDraw = true;
                list.DrawColumnHeader += (_, e) =>
                {
                    using var brush = new SolidBrush(Palette.Surface);
                    e.Graphics.FillRectangle(brush, e.Bounds);
                    TextRenderer.DrawText(e.Graphics, e.Header?.Text, list.Font, e.Bounds, Palette.Foreground);
                };
                list.DrawItem += (_, e) =>
                {
                    if (list.View == View.Details) return;
                    DrawListCell(e.Graphics, e.Bounds, e.Item.Text, list, e.Item.Selected, e.Item);
                    e.DrawFocusRectangle();
                };
                list.DrawSubItem += (_, e) => DrawListCell(e.Graphics, e.Bounds, e.SubItem?.Text, list,
                    e.Item?.Selected == true, e.ColumnIndex == 0 ? e.Item : null);
                break;
            case ListBox list when list is not CheckedListBox && list.DrawMode == DrawMode.Normal:
                list.BorderStyle = BorderStyle.FixedSingle;
                list.DrawMode = DrawMode.OwnerDrawFixed;
                list.DrawItem += (_, e) => DrawTextItem(e, e.Index < 0 ? "" : list.GetItemText(list.Items[e.Index]), list.Enabled);
                break;
            case TabControl tabs when tabs.DrawMode == TabDrawMode.Normal:
                tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabs.DrawItem += (_, e) => DrawTextItem(e, tabs.TabPages[e.Index].Text, tabs.Enabled);
                break;
        }
        // PropertyGrid owns specialized internal colors; its own properties above style them.
        if (control is PropertyGrid)
        {
            foreach (var toolbar in control.Controls.OfType<ToolStrip>()) Apply(toolbar);
            return;
        }
        foreach (Control child in control.Controls) Apply(child);
    }

    private static void ApplyItem(ToolStripItem? item)
    {
        if (item is null) return;
        item.ForeColor = Palette.Foreground;
        item.BackColor = Palette.Surface;
        if (item is ToolStripControlHost host) Apply(host.Control);
        if (item is ToolStripDropDownItem drop) Apply(drop.DropDown);
    }

    private static void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        var combo = (ComboBox)sender!;
        DrawTextItem(e, e.Index < 0 ? combo.Text : combo.GetItemText(combo.Items[e.Index]), combo.Enabled);
    }

    private static void DrawListCell(Graphics graphics, Rectangle bounds, string? text, ListView list, bool selected, ListViewItem? item)
    {
        selected &= list.Focused || !list.HideSelection;
        using var brush = new SolidBrush(selected ? Palette.Selection : Palette.Background);
        graphics.FillRectangle(brush, bounds);
        if (item is not null)
        {
            if (list.CheckBoxes)
            {
                var check = new Rectangle(bounds.X + 2, bounds.Y + (bounds.Height - 14) / 2, 14, 14);
                ControlPaint.DrawCheckBox(graphics, check, item.Checked ? ButtonState.Checked : ButtonState.Normal);
                bounds.X += 18;
                bounds.Width -= 18;
            }
            var images = list.View == View.LargeIcon ? list.LargeImageList : list.SmallImageList;
            int index = string.IsNullOrEmpty(item.ImageKey) ? item.ImageIndex : images?.Images.IndexOfKey(item.ImageKey) ?? -1;
            if (images is not null && index >= 0 && index < images.Images.Count)
            {
                images.Draw(graphics, bounds.X, bounds.Y, index);
                bounds.X += images.ImageSize.Width + 4;
                bounds.Width -= images.ImageSize.Width + 4;
            }
        }
        TextRenderer.DrawText(graphics, text, list.Font, bounds, !list.Enabled ? Palette.DisabledForeground
            : selected ? Palette.SelectionForeground : Palette.Foreground, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    public static void Apply(ToolTip tooltip)
    {
        tooltip.BackColor = Palette.Surface;
        tooltip.ForeColor = Palette.Foreground;
        tooltip.OwnerDraw = true;
        tooltip.Draw += (_, e) =>
        {
            using var brush = new SolidBrush(Palette.Surface);
            using var border = new Pen(Palette.Border);
            e.Graphics.FillRectangle(brush, e.Bounds);
            e.Graphics.DrawRectangle(border, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, e.Font, Rectangle.Inflate(e.Bounds, -3, -2), Palette.Foreground);
        };
    }

    private static void AttachToolTip(Control control, Func<Point, string?> textAt)
    {
        var tooltip = new ToolTip();
        Apply(tooltip);
        string? previous = null;
        control.MouseMove += (_, e) =>
        {
            string? text = textAt(e.Location);
            if (text == previous) return;
            previous = text;
            tooltip.SetToolTip(control, text);
        };
        control.MouseLeave += (_, _) => { previous = null; tooltip.SetToolTip(control, null); };
        control.Disposed += (_, _) => tooltip.Dispose();
    }

    private static void DrawTextItem(DrawItemEventArgs e, string? text, bool enabled)
    {
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using var brush = new SolidBrush(selected ? Palette.Selection : Palette.Surface);
        e.Graphics.FillRectangle(brush, e.Bounds);
        TextRenderer.DrawText(e.Graphics, text, e.Font, e.Bounds,
            !enabled ? Palette.DisabledForeground : selected ? Palette.SelectionForeground : Palette.Foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        e.DrawFocusRectangle();
    }
}
