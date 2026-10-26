using System.Runtime.ExceptionServices;
using Gondwana.Tooling.WinForms.Theming;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed class ThemeTests
{
    [Fact]
    public void BuiltInsHaveStableNamesAndLegiblePalettes()
    {
        Assert.Equal(new[] { "Dark", "Dark Blue", "Light" }, Enum.GetValues<ToolingTheme>().Select(ToolingPalette.Name));
        foreach (var theme in Enum.GetValues<ToolingTheme>())
        {
            var p = ToolingPalette.For(theme);
            Assert.True(Contrast(p.Foreground, p.Background) >= 7);
            Assert.True(Contrast(p.Foreground, p.Surface) >= 7);
            Assert.True(Contrast(p.SelectionForeground, p.Selection) >= 4.5);
            Assert.True(Contrast(p.DisabledForeground, p.Surface) >= 3);
        }
        Assert.True(ToolingPalette.For(ToolingTheme.DarkBlue).Surface.GetBrightness() < .25);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Theme\":\"unknown\"}")]
    [InlineData("{\"Theme\":999}")]
    [InlineData("{\"Theme\":\"999\"}")]
    public void MissingInvalidAndCorruptPreferencesFallBackToDark(string? json)
    {
        var store = new ThemePreferenceStore();
        if (json is not null) File.WriteAllText(store.FilePath, json);
        Assert.Equal(ToolingTheme.Dark, store.Read());
    }

    [Theory]
    [InlineData(ToolingTheme.Dark)]
    [InlineData(ToolingTheme.DarkBlue)]
    [InlineData(ToolingTheme.Light)]
    public void PreferenceIsSharedAcrossApplicationIds(ToolingTheme theme)
    {
        var studio = new ThemePreferenceStore();
        Assert.True(studio.Write(theme));
        foreach (var name in new[] { "Assets", "Tilesheets", "Animations", "Audio", "Scenes", "Sprites", "Studio" })
        {
            AppContext.SetData("Gondwana.Tooling.ApplicationId", name);
            var standalone = new ThemePreferenceStore();
            Assert.Equal(studio.FilePath, standalone.FilePath);
            Assert.Equal(theme, standalone.Read());
        }
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(studio.FilePath)!, "*.tmp"));
    }

    [Fact]
    public void UnwritableStoreReportsFailureWithoutThrowing()
    {
        var root = (string)AppContext.GetData("Gondwana.Tooling.SettingsRoot")!;
        var file = Path.Combine(root, "not-a-directory");
        File.WriteAllText(file, "blocked");
        var messages = new List<string>();
        Assert.False(new ThemePreferenceStore(file, messages.Add).Write(ToolingTheme.Light));
        Assert.NotEmpty(messages);
    }

    [Fact]
    public void MenuSelectionIsExclusiveAndDoesNotChangeLiveDockOrEditor() => Sta(() =>
    {
        using var owner = new Form();
        using var dockTheme = ToolingAppearance.CreateDockTheme();
        using var dock = new DockPanel { Theme = dockTheme, DocumentStyle = DocumentStyle.DockingWindow };
        owner.Controls.Add(dock);
        using var document = new DockContent { Text = "Unsaved *" };
        using var editor = new TextBox { Text = "unsaved content" };
        document.Controls.Add(editor);
        document.Show(dock, DockState.Document);
        var notices = new List<string>();
        using var menu = ToolingAppearance.CreateThemeMenu(owner, notify: notices.Add);
        foreach (ToolStripMenuItem item in menu.DropDownItems)
        {
            item.PerformClick();
            Assert.Single(menu.DropDownItems.Cast<ToolStripMenuItem>(), x => x.Checked);
            Assert.True(item.Checked);
            Assert.Same(dockTheme, dock.Theme);
            Assert.False(document.IsDisposed);
            Assert.Equal("unsaved content", editor.Text);
            Assert.Equal("Unsaved *", document.Text);
        }
        Assert.Contains(notices, n => n.Contains("Restart"));
    });

    [Fact]
    public void NestedAndDynamicControlsAreStyledWithoutChangingValues() => Sta(() =>
    {
        using var root = new UserControl();
        using var pane = new Panel();
        root.Controls.Add(pane);
        ToolingAppearance.Apply(root);
        var text = new TextBox { Text = "unchanged" };
        pane.Controls.Add(text);
        var grid = new PropertyGrid();
        pane.Controls.Add(grid);
        var context = new ContextMenuStrip();
        text.ContextMenuStrip = context;
        context.Items.Add("dynamic item");
        Assert.Equal(ToolingAppearance.Palette.Surface, text.BackColor);
        Assert.Equal("unchanged", text.Text);
        Assert.Equal(ToolingAppearance.Palette.Selection, grid.SelectedItemWithFocusBackColor);
        Assert.Equal(ToolingAppearance.Palette.Surface, context.BackColor);
        Assert.Equal(ToolingAppearance.Palette.Foreground, context.Items[0].ForeColor);
        ToolingAppearance.Apply(root); // Idempotent, no duplicate handlers.
        using var canvas = new Panel { BackColor = Color.Magenta };
        ToolingAppearance.PreserveColors(canvas);
        pane.Controls.Add(canvas);
        Assert.Equal(Color.Magenta, canvas.BackColor);
        context.Dispose();
    });

    [Fact]
    public void MessageDialogPreservesSafeDefaultAndCancelSemantics() => Sta(() =>
    {
        using var form = ThemedMessageBox.Create("Save changes?", "Unsaved changes", MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question, MessageBoxDefaultButton.Button3);
        Assert.Equal(DialogResult.Cancel, Assert.IsType<Button>(form.AcceptButton).DialogResult);
        Assert.Equal(DialogResult.Cancel, Assert.IsType<Button>(form.CancelButton).DialogResult);
        Assert.Equal(ToolingAppearance.Palette.Background, form.BackColor);
        using var yesNo = ThemedMessageBox.Create("Proceed?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        Assert.False(yesNo.ControlBox);
        Assert.Null(yesNo.CancelButton);
        using var ok = ThemedMessageBox.Create("Saved", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Assert.Equal(DialogResult.OK, Assert.IsType<Button>(ok.CancelButton).DialogResult);
    });

    [Theory]
    [InlineData(ToolingTheme.Dark)]
    [InlineData(ToolingTheme.DarkBlue)]
    [InlineData(ToolingTheme.Light)]
    public void DockThemesAreIndependentlyOwnedAndSupportFloatingAndDisposal(ToolingTheme theme) => Sta(() =>
    {
        using var first = ToolingAppearance.CreateDockTheme(theme);
        using var second = ToolingAppearance.CreateDockTheme(theme);
        Assert.NotSame(first, second);
        using var form = new Form();
        using var dock = new DockPanel { Theme = first, DocumentStyle = DocumentStyle.DockingWindow };
        form.Controls.Add(dock);
        using var pane = new DockContent();
        pane.Show(dock, DockState.Document);
        pane.DockState = DockState.Float;
        Assert.Single(dock.FloatWindows);
        pane.DockState = DockState.Document;
        Assert.Same(first, dock.Theme);
        pane.Close();
        Assert.Empty(dock.Contents);
    });

    private static double Contrast(Color a, Color b)
    {
        static double L(Color c)
        {
            static double F(byte b) { double v = b / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
            return .2126 * F(c.R) + .7152 * F(c.G) + .0722 * F(c.B);
        }
        return (Math.Max(L(a), L(b)) + .05) / (Math.Min(L(a), L(b)) + .05);
    }

    private static void Sta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "UI test timed out.");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
