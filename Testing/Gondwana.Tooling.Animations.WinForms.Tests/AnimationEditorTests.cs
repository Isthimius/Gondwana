using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Animations.Editing;
using Gondwana.Tooling.Animations.WinForms;
using SkiaSharp;

namespace Gondwana.Tooling.Animations.WinForms.Tests;

public sealed class AnimationEditorTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    [Fact]
    public void AnimationDocument_SaveRoundTripsCompleteDefinition()
    {
        string directory = CreateTempDirectory();

        try
        {
            var document = AnimationDocument.Create(directory);
            document.Definition.Key = "actor.walk";
            document.Definition.ThrottleTime = 0.125;
            document.Definition.CycleType = CycleType.PingPong;
            document.Definition.HideTileOnCycleEnd = true;
            document.Definition.NextCycleKey = "actor.idle";
            document.Definition.Frames.Add(
                new AnimationFrameDefinition
                {
                    Tilesheet = "actors",
                    RegionName = "walk",
                    XTile = 2,
                    YTile = 1
                });

            document.MarkChanged();

            string path = Path.Combine(directory, "actor.walk.gani");
            document.Save(path);

            Assert.False(document.IsDirty);
            Assert.Equal(Path.GetFullPath(path), document.FilePath);

            var loaded = AnimationDocument.Open(path);

            Assert.Equal("actor.walk", loaded.Definition.Key);
            Assert.Equal(0.125, loaded.Definition.ThrottleTime);
            Assert.Equal(CycleType.PingPong, loaded.Definition.CycleType);
            Assert.True(loaded.Definition.HideTileOnCycleEnd);
            Assert.Equal("actor.idle", loaded.Definition.NextCycleKey);

            var frame = Assert.Single(loaded.Definition.Frames);
            Assert.Equal("actors", frame.Tilesheet);
            Assert.Equal("walk", frame.RegionName);
            Assert.Equal(2, frame.XTile);
            Assert.Equal(1, frame.YTile);
            Assert.False(loaded.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Editor_LoadsGtsSourceAndAppendsSelectedFrame() =>
        RunSta(() =>
        {
            string directory = CreateTempDirectory();

            try
            {
                string gtsPath = CreateGts(directory);
                var document = AnimationDocument.Create(directory);

                using var form = new Form
                {
                    Opacity = 0,
                    ShowInTaskbar = false,
                    Size = new Size(1200, 800)
                };

                using var editor = new AnimationEditorControl(document)
                {
                    Dock = DockStyle.Fill
                };

                form.Controls.Add(editor);
                form.Show();
                Application.DoEvents();

                editor.AddTilesheetSource(gtsPath);

                var tree = Field<TreeView>(editor, "_sourceTree");
                Assert.Single(tree.Nodes);

                var regionNode = Assert.IsType<TreeNode>(
                    Assert.Single(tree.Nodes[0].Nodes.Cast<TreeNode>()));
                regionNode.Expand();
                Application.DoEvents();

                var rowNode = Assert.IsType<TreeNode>(
                    Assert.Single(regionNode.Nodes.Cast<TreeNode>()));
                rowNode.Expand();
                Application.DoEvents();

                Assert.Equal(2, rowNode.Nodes.Count);
                Assert.NotNull(tree.ImageList);
                Assert.Equal(new Size(32, 32), tree.ImageList.ImageSize);

                foreach (TreeNode frameNode in rowNode.Nodes)
                {
                    Assert.True(frameNode.ImageIndex >= 0);
                    Assert.Equal(
                        frameNode.ImageIndex,
                        frameNode.SelectedImageIndex);

                    Assert.Equal(
                        new Size(32, 32),
                        tree.ImageList.Images[frameNode.ImageIndex].Size);
                }

                var preview = Field<AnimationPreviewControl>(
                    editor,
                    "_preview");

                tree.SelectedNode = rowNode.Nodes[1];
                Application.DoEvents();

                Assert.True(preview.IsShowingSourceFrame);

                typeof(TreeView)
                    .GetMethod(
                        "OnNodeMouseDoubleClick",
                        PrivateInstance)!
                    .Invoke(
                        tree,
                        [
                            new TreeNodeMouseClickEventArgs(
                                rowNode.Nodes[1],
                                MouseButtons.Left,
                                2,
                                0,
                                0)
                        ]);

                Application.DoEvents();

                var firstFrame = Assert.Single(document.Definition.Frames);
                Assert.Equal("test-sheet", firstFrame.Tilesheet);
                Assert.Equal("walk", firstFrame.RegionName);
                Assert.Equal(1, firstFrame.XTile);
                Assert.Equal(0, firstFrame.YTile);

                tree.SelectedNode = rowNode.Nodes[0];
                Application.DoEvents();

                Assert.True(preview.IsShowingSourceFrame);

                typeof(Control)
                    .GetMethod(
                        "OnDoubleClick",
                        PrivateInstance)!
                    .Invoke(preview, [EventArgs.Empty]);

                Application.DoEvents();

                Assert.Equal(2, document.Definition.Frames.Count);
                var secondFrame = document.Definition.Frames[1];
                Assert.Equal("test-sheet", secondFrame.Tilesheet);
                Assert.Equal("walk", secondFrame.RegionName);
                Assert.Equal(0, secondFrame.XTile);
                Assert.Equal(0, secondFrame.YTile);
                Assert.True(document.IsDirty);

                var sourceToolbar = Descendants<ToolStrip>(editor)
                    .Single(bar => bar.Items
                        .Cast<ToolStripItem>()
                        .Any(item => item.Text == "Add GTS…"));

                Assert.Equal(
                    ["Add GTS…", "Add", "Remove"],
                    sourceToolbar.Items
                        .Cast<ToolStripItem>()
                        .Select(item => item.Text)
                        .ToArray());

                var errors = editor.UpdateValidation();
                Assert.Empty(errors);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });

    [Fact]
    public void DuplicateLogicalTilesheetNamesAreRejected() =>
        RunSta(() =>
        {
            string directory = CreateTempDirectory();

            try
            {
                string first = CreateGts(
                    Path.Combine(directory, "one"),
                    "test-sheet");
                string second = CreateGts(
                    Path.Combine(directory, "two"),
                    "test-sheet");

                using var editor = new AnimationEditorControl(
                    AnimationDocument.Create(directory));

                editor.AddTilesheetSource(first);

                var exception = Assert.Throws<InvalidOperationException>(
                    () => editor.AddTilesheetSource(second));

                Assert.Contains(
                    "same name",
                    exception.Message,
                    StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });

    [Fact]
    public void PreviewAdvanceDoesNotPausePlaybackWhenSelectingAdvancedFrame() =>
        RunSta(() =>
        {
            string directory = CreateTempDirectory();

            try
            {
                var document = AnimationDocument.Create(directory);
                document.Definition.ThrottleTime = 0.125;
                document.Definition.Frames.Add(
                    new AnimationFrameDefinition
                    {
                        Tilesheet = "test-sheet",
                        RegionName = "walk",
                        XTile = 0,
                        YTile = 0
                    });
                document.Definition.Frames.Add(
                    new AnimationFrameDefinition
                    {
                        Tilesheet = "test-sheet",
                        RegionName = "walk",
                        XTile = 1,
                        YTile = 0
                    });

                using var form = new Form
                {
                    Opacity = 0,
                    ShowInTaskbar = false,
                    Size = new Size(1200, 800)
                };

                using var editor = new AnimationEditorControl(document)
                {
                    Dock = DockStyle.Fill
                };

                form.Controls.Add(editor);
                form.Show();
                Application.DoEvents();

                var preview = Field<AnimationPreviewControl>(editor, "_preview");
                var frames = Field<ListView>(editor, "_frames");

                preview.Play();

                bool advanced = (bool)preview.GetType()
                    .GetMethod("Advance", PrivateInstance)!
                    .Invoke(preview, null)!;

                Application.DoEvents();

                Assert.True(advanced);
                Assert.True(preview.IsPlaying);
                Assert.Equal(1, preview.CurrentFrameIndex);
                int selectedIndex = Assert.Single(
                    frames.SelectedIndices.Cast<int>());
                Assert.Equal(1, selectedIndex);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });

    private static string CreateGts(
        string directory,
        string name = "test-sheet")
    {
        Directory.CreateDirectory(directory);

        string imagePath = Path.Combine(directory, "sheet.png");
        using (var bitmap = new SKBitmap(32, 16))
        {
            bitmap.Erase(SKColors.White);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(
                SKEncodedImageFormat.Png,
                100);
            File.WriteAllBytes(imagePath, data.ToArray());
        }

        string gtsPath = Path.Combine(directory, "sheet.gts");

        TilesheetDefinitionSerializer.Save(
            gtsPath,
            new TilesheetDefinition
            {
                Name = name,
                Image = new TilesheetImageDefinition
                {
                    FilePath = "sheet.png"
                },
                Regions =
                [
                    new TilesheetRegionDefinition
                    {
                        Name = "walk",
                        Area = new Rectangle(0, 0, 32, 16),
                        TileSize = new Size(16, 16)
                    }
                ]
            });

        return gtsPath;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"GaniEditorTests_{Guid.NewGuid():N}");

        Directory.CreateDirectory(path);
        return path;
    }

    private static IEnumerable<T> Descendants<T>(
        Control root)
        where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match)
                yield return match;

            foreach (var descendant in Descendants<T>(child))
                yield return descendant;
        }
    }

    private static T Field<T>(
        object instance,
        string name) =>
        (T)instance.GetType()
            .GetField(name, PrivateInstance)!
            .GetValue(instance)!;

    private static void RunSta(Action test)
    {
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                Application.SetUnhandledExceptionMode(
                    UnhandledExceptionMode.ThrowException,
                    threadScope: true);

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

        Assert.True(
            thread.Join(TimeSpan.FromSeconds(30)),
            "Windows interaction test timed out.");

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}
