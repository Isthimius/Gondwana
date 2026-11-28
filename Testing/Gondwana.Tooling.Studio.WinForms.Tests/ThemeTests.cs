using System.Runtime.ExceptionServices;
using Gondwana.Tooling.Studio.Shared.WinForms.Theming;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed class ThemeTests
{
    [Fact]
    public void BuiltInsHaveStableNamesAndLegiblePalettes()
    {
        Assert.Equal(
            new[] { "Dark", "Dark Blue", "Light" },
            Enum.GetValues<ToolingTheme>().Select(ToolingPalette.Name));

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

        if (json is not null)
            File.WriteAllText(store.FilePath, json);

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
        using var form = ThemedMessageBox.Create(
            "Save changes?",
            "Unsaved changes",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button3);

        Assert.Equal(DialogResult.Cancel, Assert.IsType<Button>(form.AcceptButton).DialogResult);
        Assert.Equal(DialogResult.Cancel, Assert.IsType<Button>(form.CancelButton).DialogResult);
        Assert.Equal(ToolingAppearance.Palette.Background, form.BackColor);

        using var yesNo = ThemedMessageBox.Create(
            "Proceed?",
            "Confirm",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        Assert.False(yesNo.ControlBox);
        Assert.Null(yesNo.CancelButton);

        using var ok = ThemedMessageBox.Create(
            "Saved",
            "Information",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        Assert.Equal(DialogResult.OK, Assert.IsType<Button>(ok.CancelButton).DialogResult);
    });

    [Theory]
    [InlineData(16, 16)]
    [InlineData(22, 22)]
    [InlineData(24, 32)]
    [InlineData(32, 32)]
    [InlineData(48, 48)]
    [InlineData(64, 48)]
    public void OxygenNewSelectsNativeResolution(int pixels, int native)
    {
        var path = ToolingIconResources.FindOxygenIcon("document-new.png", pixels);
        Assert.NotNull(path);
        Assert.Equal($"{native}x{native}", Path.GetFileName(Path.GetDirectoryName(path)));

        using var image = ToolingIconResources.TryCreateOxygenBitmap("document-new.png", pixels);
        Assert.NotNull(image);
        Assert.Equal(new Size(pixels, pixels), image.Size);
        bool hasVisiblePixels = false;
        bool hasTransparentPixels = false;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                byte alpha = image.GetPixel(x, y).A;
                hasVisiblePixels |= alpha > 200;
                hasTransparentPixels |= alpha < 255;
            }
        }

        Assert.True(hasVisiblePixels);
        Assert.True(hasTransparentPixels);
    }

    [Fact]
    public void OxygenThemeOverridesAreIndependentAndFallBackToCommonArtwork()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            const string fileName = "document-save.png";

            string AddArtwork(string relative)
            {
                string path = Path.Combine(root, relative, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "test");
                return path;
            }

            string common16 = AddArtwork("16x16");
            string common32 = AddArtwork("32x32");
            string dark22 = AddArtwork(Path.Combine("themes", "Dark", "22x22"));
            string dark48 = AddArtwork(Path.Combine("themes", "Dark", "48x48"));
            string light32 = AddArtwork(Path.Combine("themes", "Light", "32x32"));

            Assert.Equal(dark22, ToolingIconResources.FindOxygenIcon(fileName, 16, ToolingTheme.Dark, root));
            Assert.Equal(dark22, ToolingIconResources.FindOxygenIcon(fileName, 22, ToolingTheme.Dark, root));
            Assert.Equal(dark48, ToolingIconResources.FindOxygenIcon(fileName, 24, ToolingTheme.Dark, root));
            Assert.Equal(dark48, ToolingIconResources.FindOxygenIcon(fileName, 64, ToolingTheme.Dark, root));
            Assert.Equal(light32, ToolingIconResources.FindOxygenIcon(fileName, 16, ToolingTheme.Light, root));
            Assert.Equal(common16, ToolingIconResources.FindOxygenIcon(fileName, 16, ToolingTheme.DarkBlue, root));
            Assert.Equal(common32, ToolingIconResources.FindOxygenIcon(fileName, 24, ToolingTheme.DarkBlue, root));
            Assert.Null(ToolingIconResources.FindOxygenIcon("missing.png", 16, ToolingTheme.Dark, root));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("open", "document-open.png")]
    [InlineData("save", "document-save.png")]
    [InlineData("save-all", "document-save-all.png")]
    [InlineData("save-as", "document-save-as.png")]
    [InlineData("folder", "folder-activities.png")]
    public void FileOperationGlyphUsesNativeArtworkAndPreservesText(string kind, string fileName) => Sta(() =>
    {
        using var item = new ToolStripMenuItem("Command");
        Gondwana.Tooling.Studio.Shared.WinForms.Dpi.ToolbarGlyphs.SetFileOperationGlyph(item, kind);
        Assert.Equal("Command", item.Text);
        using var expected = ToolingIconResources.TryCreateOxygenBitmap(fileName, 16);
        Assert.NotNull(expected);
        var actual = Assert.IsType<Bitmap>(item.Image);
        Assert.Equal(expected.Size, actual.Size);

        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
                Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
        }
    });

    [Theory]
    [InlineData(Gondwana.Tooling.Studio.Shared.WinForms.Dpi.ToolbarGlyphs.EditingCommand.Add, "list-add.png")]
    [InlineData(Gondwana.Tooling.Studio.Shared.WinForms.Dpi.ToolbarGlyphs.EditingCommand.Remove, "list-remove.png")]
    [InlineData(Gondwana.Tooling.Studio.Shared.WinForms.Dpi.ToolbarGlyphs.EditingCommand.Delete, "edit-delete.png")]
    [InlineData(Gondwana.Tooling.Studio.Shared.WinForms.Dpi.ToolbarGlyphs.EditingCommand.Duplicate, "edit-copy.png")]
    [InlineData(Gondwana.Tooling.Studio.Shared.WinForms.Dpi.ToolbarGlyphs.EditingCommand.Rename, "document-edit.png")]
    [InlineData(Gondwana.Tooling.Studio.Shared.WinForms.Dpi.ToolbarGlyphs.EditingCommand.Replace, "edit-find-replace.png")]
    [InlineData(Gondwana.Tooling.Studio.Shared.WinForms.Dpi.ToolbarGlyphs.EditingCommand.Prune, "edit-clear.png")]
    [InlineData(Gondwana.Tooling.Studio.Shared.WinForms.Dpi.ToolbarGlyphs.EditingCommand.Apply, "dialog-ok-apply.png")]
    [InlineData(Gondwana.Tooling.Studio.Shared.WinForms.Dpi.ToolbarGlyphs.EditingCommand.Cancel, "dialog-cancel.png")]
    public void EditingGlyphUsesAssignedArtwork(Gondwana.Tooling.Studio.Shared.WinForms.Dpi.ToolbarGlyphs.EditingCommand command, string fileName)
    {
        using var actual = Gondwana.Tooling.Studio.Shared.WinForms.Dpi.ToolbarGlyphs.CreateEditingBitmap(command, 32);
        using var expected = ToolingIconResources.TryCreateOxygenBitmap(fileName, 32);
        Assert.NotNull(actual);
        Assert.NotNull(expected);
        Assert.Equal(expected.Size, actual.Size);

        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
                Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
        }
    }

    [Fact]
    public void AboutMenuAndDialogExposeSharedArtworkCredits() => Sta(() =>
    {
        using var owner = new Form { Text = "Gondwana Studio" };
        using var help = ToolingAbout.CreateHelpMenu(owner);
        Assert.Equal("&Help", help.Text);
        Assert.Equal("About", Assert.Single(help.DropDownItems.Cast<ToolStripItem>()).Name);

        using var dialog = ToolingAbout.CreateDialog(owner.Text);
        Assert.Equal("About Gondwana Studio", dialog.Text);
        var body = Assert.Single(dialog.Controls.OfType<Panel>(), panel => panel is not FlowLayoutPanel);
        var message = Assert.Single(body.Controls.OfType<TextBox>());
        Assert.Contains("MIT License", message.Text, StringComparison.Ordinal);
        Assert.Contains("Icons8", message.Text, StringComparison.Ordinal);
        Assert.Contains("KDE Oxygen", message.Text, StringComparison.Ordinal);
        Assert.Contains("LGPL v3 or later", message.Text, StringComparison.Ordinal);
        Assert.Contains("THIRD-PARTY-NOTICES.md", message.Text, StringComparison.Ordinal);
        Assert.Equal(DialogResult.OK, Assert.IsType<Button>(dialog.AcceptButton).DialogResult);
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
            static double F(byte b)
            {
                double v = b / 255d;
                return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4);
            }

            return .2126 * F(c.R) + .7152 * F(c.G) + .0722 * F(c.B);
        }

        return (Math.Max(L(a), L(b)) + .05) / (Math.Min(L(a), L(b)) + .05);
    }

    private static void Sta(Action action)
    {
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "UI test timed out.");

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}
