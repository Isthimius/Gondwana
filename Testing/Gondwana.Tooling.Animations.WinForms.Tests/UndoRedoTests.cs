using Gondwana.Drawing.Animation.GANI;
using Gondwana.Tooling.Animations.Editing;

namespace Gondwana.Tooling.Animations.WinForms.Tests;

public sealed partial class AnimationEditorTests
{
    [Fact]
    public void ReplayRefreshesFramesSourcesPropertiesAndPlayback() => RunSta(() =>
    {
        string directory = CreateTempDirectory();
        try
        {
            using var document = AnimationDocument.Create(directory);
            using var host = new Form { Opacity = 0, ShowInTaskbar = false, Size = new Size(1200, 800) };
            using var editor = new AnimationEditorControl(document);
            host.Controls.Add(editor); host.Show(); Application.DoEvents();
            editor.AddTilesheetSource(CreateGts(directory)); Application.DoEvents();
            var tree = Field<TreeView>(editor, "_sourceTree");
            Assert.Single(tree.Nodes);
            document.History.Undo(); Application.DoEvents(); Assert.Empty(tree.Nodes);
            document.History.Redo(); Application.DoEvents(); Assert.Single(tree.Nodes);
            document.History.Clear();
            var first = new AnimationFrameDefinition { Tilesheet = "test-sheet", RegionName = "walk", XTile = 0, YTile = 0 };
            var second = new AnimationFrameDefinition { Tilesheet = "test-sheet", RegionName = "walk", XTile = 1, YTile = 0, DurationSeconds = .25 };
            document.AddFrame(first); document.AddFrame(second); Application.DoEvents();
            var frames = Field<ListView>(editor, "_frames");
            var preview = Field<AnimationPreviewControl>(editor, "_preview");
            Assert.Equal(2, frames.Items.Count);
            preview.Step(); preview.Play();
            document.History.Undo(); Application.DoEvents();
            Assert.Single(frames.Items); Assert.InRange(preview.CurrentFrameIndex, 0, 0);
            document.History.Redo(); Application.DoEvents();
            Assert.Same(second, frames.Items[1].Tag);
            frames.Items[1].Selected = true; Application.DoEvents();
            Assert.Equal(.25, Field<AnimationPropertyAdapter>(editor, "_propertyAdapter").DurationSeconds);
            document.History.Undo(); document.History.Undo(); Application.DoEvents();
            Assert.Empty(frames.Items); Assert.False(preview.IsPlaying); Assert.False(preview.IsShowingSourceFrame);
            document.History.Redo(); Application.DoEvents(); Assert.Same(first, frames.Items[0].Tag);
        }
        finally { Directory.Delete(directory, recursive: true); }
    });
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MemberRefreshDoesNotRecoverRemovedSourceOrDiscardHistory(bool rename) => RunSta(() =>
    {
        string directory = CreateTempDirectory();
        try
        {
            CreateGts(directory);
            using var document = AnimationDocument.Create(directory);
            var original = document.Definition;
            document.AddFrame(new() { Tilesheet = "test-sheet", RegionName = "walk" });
            document.AddDefinition();
            document.SelectDefinition(original);
            using var host = new Form { Opacity = 0, ShowInTaskbar = false, Size = new Size(1200, 800) };
            using var editor = new AnimationEditorControl(document);
            host.Controls.Add(editor); host.Show(); Application.DoEvents();
            var source = Assert.Single(original.TilesheetSources);
            Assert.False(document.History.CanUndo);
            document.History.MarkSaved();
            Assert.True(document.RemoveTilesheetSource("test-sheet"));
            Application.DoEvents();
            var bar = editor.Controls.OfType<ToolStrip>().Single(strip => strip.Items.Find("RenameDefinition", false).Length != 0);
            if (rename)
            {
                bar.Items.OfType<ToolStripTextBox>().Single().Text = "renamed";
                bar.Items.Find("RenameDefinition", false).Single().PerformClick();
            }
            else
            {
                var members = bar.Items.OfType<ToolStripComboBox>().Single();
                members.SelectedIndex = 1;
                members.SelectedIndex = 0;
            }
            Application.DoEvents();
            Assert.Same(original, document.Definition);
            Assert.Empty(original.TilesheetSources);
            Assert.True(document.History.CanUndo);
            Assert.True(document.IsDirty);
            if (rename) document.History.Undo();
            document.History.Undo(); Application.DoEvents();
            Assert.Same(source, Assert.Single(original.TilesheetSources));
            Assert.False(document.IsDirty);
            document.History.Redo(); Application.DoEvents();
            Assert.Empty(original.TilesheetSources);
        }
        finally { Directory.Delete(directory, recursive: true); }
    });
}
