using System.Runtime.ExceptionServices;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.WinForms.Theming;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

/// <summary>Checks the shared definition toolbar used by the GTS/GANI/GSCN/GSPR/GSND editors.</summary>
public sealed class DefinitionSelectionBarTests
{
    /// <summary>Definition commands use 32px icons with visible native hover tooltips.</summary>
    [Fact]
    public void CommandsUse32PixelIconsAndShowHoverTooltips() => RunSta(() =>
    {
        var document = SceneDocument.Create(Path.GetTempPath());
        using var editor = new SceneEditorControl(document);
        using var host = new Form { Size = new(1450, 900), Opacity = 0, ShowInTaskbar = false };
        host.Controls.Add(editor);
        host.Show();
        Application.DoEvents();
        ToolingAppearance.Apply(host);

        var bar = Assert.Single(editor.Controls.OfType<ToolStrip>()
            .Where(strip => strip.GetType().Name.StartsWith("DefinitionSelectionBar", StringComparison.Ordinal)));
        Assert.Equal(new Size(32, 32), bar.ImageScalingSize);
        Assert.True(bar.ShowItemToolTips);
        Assert.Single(bar.Items.OfType<ToolStripComboBox>());
        Assert.Single(bar.Items.OfType<ToolStripTextBox>());

        var buttons = bar.Items.OfType<ToolStripButton>().ToArray();
        string[] labels =
        [
            "Add definition",
            "Import definition…",
            "Duplicate definition",
            "Remove definition",
            "Rename"
        ];
        Assert.Equal(labels, buttons.Select(button => button.ToolTipText));

        foreach (var button in buttons)
        {
            Assert.Equal(ToolStripItemDisplayStyle.Image, button.DisplayStyle);
            Assert.False(button.AutoSize);
            Assert.Equal(new Size(32, 32), button.Size);
            Assert.Equal(new Size(32, 32), Assert.IsType<Bitmap>(button.Image).Size);
            Assert.False(button.AutoToolTip);
            Assert.Equal(button.ToolTipText, button.Text);
            Assert.Equal(button.ToolTipText, button.AccessibleName);
        }

        int before = document.Definitions.Count;
        buttons[0].PerformClick();
        Assert.Equal(before + 1, document.Definitions.Count);
    });

    private static void RunSta(Action test)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, threadScope: true);
                test();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Definition toolbar WinForms test timed out.");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
