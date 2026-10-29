using System.Runtime.ExceptionServices;
using Gondwana.Tooling.Studio.ViewModels;
using Gondwana.Tooling.Studio.WinForms.Panels;
using Gondwana.Tooling.WinForms.Theming;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

/// <summary>Regression tests for Output severity detection and RichTextBox rendering.</summary>
public sealed class OutputPanelTests
{
    [Theory]
    [InlineData("13:14:15 Error: Cannot save.", OutputSeverity.Error)]
    [InlineData("13:14:15 [ERROR] Cannot save.", OutputSeverity.Error)]
    [InlineData("13:14:15 Fatal: Cannot load.", OutputSeverity.Error)]
    [InlineData("13:14:15 CRITICAL: Cannot load.", OutputSeverity.Error)]
    [InlineData("13:14:15 Warning: Missing reference.", OutputSeverity.Warning)]
    [InlineData("13:14:15 [WARN] Missing reference.", OutputSeverity.Warning)]
    [InlineData("13:14:15   warn: Indented warning.", OutputSeverity.Warning)]
    [InlineData("13:14:15 Info: Validation passed.", OutputSeverity.Important)]
    [InlineData("13:14:15 [IMPORTANT] Working directory changed.", OutputSeverity.Important)]
    [InlineData("13:14:15 Opened errors.gscn", OutputSeverity.Normal)]
    [InlineData("13:14:15 Imported warning-texture.png", OutputSeverity.Normal)]
    [InlineData("13:14:15 Validation passed.", OutputSeverity.Normal)]
    public void RecognizesOnlyLeadingSeverityMarkers(string line, OutputSeverity expected) =>
        Assert.Equal(expected, OutputPanel.Classify(line));

    [Theory]
    [InlineData(ToolingTheme.Dark)]
    [InlineData(ToolingTheme.DarkBlue)]
    [InlineData(ToolingTheme.Light)]
    public void SeverityColorsRemainLegibleInAllThemes(ToolingTheme theme)
    {
        var palette = ToolingPalette.For(theme);
        Assert.Equal(palette.Foreground, OutputPanel.ColorFor(OutputSeverity.Normal, theme));
        foreach (var severity in new[] { OutputSeverity.Warning, OutputSeverity.Error, OutputSeverity.Important })
        {
            var color = OutputPanel.ColorFor(severity, theme);
            Assert.NotEqual(palette.Foreground, color);
            Assert.True(Contrast(color, palette.Background) >= 4.5, $"{theme} {severity} background contrast");
            Assert.True(Contrast(color, palette.Surface) >= 4.5, $"{theme} {severity} surface contrast");
        }
    }

    [Fact]
    public void ExistingAndAppendedLinesGetIndependentColorsAndResetsRebuildThem() => Sta(() =>
    {
        var vm = new OutputViewModel();
        vm.Log("Normal message");
        vm.Log("Warning: Preview incomplete");
        using var panel = new OutputPanel(vm);
        var box = Assert.IsType<RichTextBox>(Assert.Single(panel.Controls));

        AssertLineColor("Normal message", OutputSeverity.Normal);
        AssertLineColor("Warning: Preview incomplete", OutputSeverity.Warning);

        vm.Log("Error: Unable to save");
        vm.Log("Info: Scan completed");
        AssertLineColor("Error: Unable to save", OutputSeverity.Error);
        AssertLineColor("Info: Scan completed", OutputSeverity.Important);
        AssertLineColor("Normal message", OutputSeverity.Normal);

        // An insertion in the middle must repaint subsequent entries instead of appending.
        vm.Lines.Insert(1, "12:00:00 [WARN] Inserted warning");
        AssertLineColor("Inserted warning", OutputSeverity.Warning);
        AssertLineColor("Error: Unable to save", OutputSeverity.Error);

        vm.Lines.Clear();
        vm.Log("Error: Fresh state");
        Assert.DoesNotContain("Preview incomplete", box.Text);
        AssertLineColor("Fresh state", OutputSeverity.Error);

        void AssertLineColor(string needle, OutputSeverity severity)
        {
            int index = box.Text.IndexOf(needle, StringComparison.Ordinal);
            Assert.True(index >= 0, $"Missing: {needle}");
            box.Select(index, needle.Length);
            Assert.Equal(OutputPanel.ColorFor(severity, ToolingAppearance.Current), box.SelectionColor);
        }
    });

    private static double Contrast(Color a, Color b)
    {
        static double L(Color c)
        {
            static double F(byte v)
            {
                double n = v / 255d;
                return n <= .04045 ? n / 12.92 : Math.Pow((n + .055) / 1.055, 2.4);
            }
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
