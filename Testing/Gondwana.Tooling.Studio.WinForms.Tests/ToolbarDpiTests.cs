using System.Drawing.Drawing2D;
using Gondwana.Tooling.WinForms;
using Gondwana.Tooling.WinForms.Theming;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Theory]
    [InlineData(96, 48, 24, 32)]
    [InlineData(120, 60, 30, 40)]
    [InlineData(144, 72, 36, 48)]
    [InlineData(192, 96, 48, 64)]
    public void ToolbarMetricsUseLogicalSizes(int dpi, int studioButton, int studioImage, int editorButton)
    {
        Assert.Equal(studioButton, ToolbarSizing.Pixels(48, dpi));
        Assert.Equal(studioImage, ToolbarSizing.Pixels(24, dpi));
        Assert.Equal(editorButton, ToolbarSizing.Pixels(32, dpi));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToolbarSizing.Pixels(32, 0));
    }

    [Theory]
    [InlineData(48, 24)]
    [InlineData(32, 32)]
    public void ToolbarTransitionsRestoreAbsoluteMetricsAndReleaseImages(int logicalButton, int logicalImage) => RunSta(_ =>
    {
        using var host = new Form { Width = 1200, Opacity = 0, ShowInTaskbar = false };
        using var strip = new TestDpiToolStrip(logicalButton, logicalImage) { Dock = DockStyle.Top };
        var button = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = "Keep command" };
        var selector = new ToolStripComboBox { Width = 230 };
        strip.Items.AddRange([button, selector]);
        int draws = 0;
        strip.SetGlyph(button, pixels =>
        {
            draws++;
            var bitmap = new Bitmap(pixels, pixels);
            using var graphics = Graphics.FromImage(bitmap);
            ToolbarGlyphs.Prepare(graphics, pixels, 24);
            using var pen = new Pen(strip.ForeColor, 2) { LineJoin = LineJoin.Round };
            graphics.DrawRectangle(pen, 4, 4, 16, 16);
            return bitmap;
        });
        strip.SetLogicalWidth(selector, 230);
        host.Controls.Add(strip);
        host.Show();
        Application.DoEvents();
        foreach (int dpi in new[] { 96, 120, 144, 192, 144, 96 })
        {
            var previous = button.Image!;
            bool changes = strip.TestDpi != dpi;
            strip.ChangeDpi(dpi);
            if (changes) Assert.Throws<ArgumentException>(() => previous.Width);
            int expectedButton = ToolbarSizing.Pixels(logicalButton, dpi);
            int expectedImage = ToolbarSizing.Pixels(logicalImage, dpi);
            Assert.Equal(new Size(expectedButton, expectedButton), button.Size);
            Assert.Equal(new Size(expectedImage, expectedImage), button.Image!.Size);
            Assert.Equal(button.Image.Size, strip.ImageScalingSize);
            Assert.Equal(ToolbarSizing.Pixels(230, dpi), selector.Width);
            var current = button.Image;
            int before = draws;
            // Repeated notifications, parent autoscaling, and handle recreation must not
            // accumulate scale or allocate new images at the same DPI/palette.
            strip.ChangeDpi(dpi);
            strip.Scale(new SizeF(1.5f, 1.5f));
            strip.Recreate();
            strip.PerformLayout();
            Assert.Equal(new Size(expectedButton, expectedButton), button.Size);
            Assert.Equal(ToolbarSizing.Pixels(230, dpi), selector.Width);
            Assert.Same(current, button.Image);
            Assert.Equal(before, draws);
            Assert.True(button.Bounds.Bottom <= strip.ClientSize.Height);
            Assert.Equal("Keep command", button.ToolTipText);
        }
        var last = button.Image!;
        strip.Dispose();
        Assert.Throws<ArgumentException>(() => last.Width);
    });

    [Theory]
    [InlineData(ToolingTheme.Light)]
    [InlineData(ToolingTheme.Dark)]
    [InlineData(ToolingTheme.DarkBlue)]
    public void GlyphRebuildUsesCurrentColorsAndDisposesPreviousBitmap(ToolingTheme theme) => RunSta(_ =>
    {
        using var strip = new TestDpiToolStrip(32, 32);
        var button = new ToolStripButton();
        strip.Items.Add(button);
        strip.SetGlyph(button, pixels =>
        {
            var image = new Bitmap(pixels, pixels);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(strip.ForeColor);
            return image;
        });
        var original = button.Image!;
        var color = ToolingPalette.For(theme).Foreground;
        strip.ForeColor = color;
        strip.ChangeDpi(144);
        Assert.Equal(color.ToArgb(), ((Bitmap)button.Image!).GetPixel(10, 10).ToArgb());
        Assert.Throws<ArgumentException>(() => original.Width);
    });

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(192)]
    public void RealGlyphsRenderInsideTargetBitmapsInEveryTheme(int dpi)
    {
        foreach (var theme in Enum.GetValues<ToolingTheme>())
        {
            var palette = ToolingPalette.For(theme);
            foreach (var command in new[] { "new", "open", "folder", "save", "save-all", "refresh", "preview", "validate" })
            {
                using var image = ToolbarGlyphs.Studio(command, ToolbarSizing.Pixels(24, dpi), palette);
                AssertGlyph(image, palette.Foreground);
            }
            foreach (var command in Enum.GetValues<ToolbarGlyphs.DefinitionCommand>())
            {
                using var image = ToolbarGlyphs.Definition(command, ToolbarSizing.Pixels(32, dpi), palette);
                AssertGlyph(image, palette.Foreground);
            }
        }
    }

    private static void AssertGlyph(Bitmap image, Color foreground)
    {
        bool hasForeground = false;
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image.GetPixel(x, y);
                hasForeground |= pixel.A > 200 && Math.Abs(pixel.R - foreground.R) < 3
                    && Math.Abs(pixel.G - foreground.G) < 3 && Math.Abs(pixel.B - foreground.B) < 3;
                if (x == 0 || y == 0 || x == image.Width - 1 || y == image.Height - 1)
                    Assert.True(pixel.A < 250, "A solid glyph stroke reaches the bitmap edge.");
            }
        Assert.True(hasForeground);
    }

    private sealed class TestDpiToolStrip(int button, int image) : DpiToolStrip(button, image)
    {
        internal int TestDpi { get; private set; } = 96;
        protected override int EffectiveDpi => TestDpi == 0 ? 96 : TestDpi;
        internal void ChangeDpi(int dpi)
        {
            TestDpi = dpi;
            OnDpiChangedAfterParent(EventArgs.Empty);
            PerformLayout();
        }
        internal void Recreate() => RecreateHandle();
    }
}

