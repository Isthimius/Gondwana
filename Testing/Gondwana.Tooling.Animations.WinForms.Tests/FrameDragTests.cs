using System.Text;
using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Animations.Editing;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Animations.WinForms.Tests;

public sealed partial class AnimationEditorTests
{
    [Fact]
    public void StandaloneSourcePaneDragInsertsAndAppendsWithoutRewritingExistingSource() => RunSta(() =>
    {
        string directory = CreateTempDirectory();
        try
        {
            string gts = CreateGts(directory);
            using var host = ShowFrameDragEditor(directory);
            var document = host.Document;
            var editor = host.Editor;
            editor.AddTilesheetSource(gts);
            var source = Assert.Single(document.Definition.TilesheetSources);
            var first = new AnimationFrameDefinition { Tilesheet = "test-sheet", RegionName = "walk", DurationSeconds = .2 };
            var last = new AnimationFrameDefinition { Tilesheet = "test-sheet", RegionName = "walk", DurationSeconds = .4 };
            document.AddFrame(first);
            document.AddFrame(last);
            document.History.ResetSaved();
            Application.DoEvents();

            var tree = Field<TreeView>(editor, "_sourceTree");
            var root = tree.Nodes[0];
            var region = root.Nodes[0];
            region.Expand();
            region.Nodes[0].Expand();
            Assert.Null(AnimationEditorControl.CreateSourceFrameDrag(root));
            Assert.Null(AnimationEditorControl.CreateSourceFrameDrag(region));
            var drag = Assert.IsType<TilesheetFrameDrag>(AnimationEditorControl.CreateSourceFrameDrag(region.Nodes[0].Nodes[1]));
            Assert.Equal(new ResourceAddress(new AssetLocation(AssetTypes.TilesheetDefinition, gts), "test-sheet"), drag.Address);
            Assert.Equal(("walk", 1, 0), (drag.RegionName, drag.X, drag.Y));

            var frames = Field<FrameSequenceListView>(editor, "_frames");
            Rectangle firstRow = frames.GetItemRect(0);
            Rectangle lastRow = frames.GetItemRect(1);
            var insert = DragAt(frames, new ResourceDragData<TilesheetFrameDrag>(drag), firstRow.Top + 1);
            RaiseDrag(frames, "Enter", insert);
            Assert.Equal(DragDropEffects.Copy, insert.Effect);
            Assert.Equal(0, frames.InsertionIndex);
            var between = DragAt(frames, insert.Data!, lastRow.Top + 1);
            RaiseDrag(frames, "Over", between);
            Assert.Equal(1, frames.InsertionIndex);
            RaiseDrag(frames, "Drop", between);
            Assert.Equal(DragDropEffects.Copy, between.Effect);
            Assert.Equal(-1, frames.InsertionIndex);
            Application.DoEvents();

            var inserted = document.Definition.Frames[1];
            Assert.Equal(new[] { first, inserted, last }, document.Definition.Frames);
            Assert.Equal(("test-sheet", "walk", 1, 0), (inserted.Tilesheet, inserted.RegionName, inserted.XTile, inserted.YTile));
            Assert.Null(inserted.DurationSeconds);
            Assert.Equal(.2, first.DurationSeconds);
            Assert.Equal(.4, last.DurationSeconds);
            Assert.Same(source, Assert.Single(document.Definition.TilesheetSources));
            Assert.Equal("sheet.gts", source.GtsPath);
            Assert.Equal(1, Assert.Single(frames.SelectedIndices.Cast<int>()));
            Assert.True(document.IsDirty);

            document.History.Undo();
            Application.DoEvents();
            Assert.Equal(new[] { first, last }, document.Definition.Frames);
            Assert.Same(source, Assert.Single(document.Definition.TilesheetSources));
            Assert.False(document.History.CanUndo);
            Assert.False(document.IsDirty);
            document.History.Redo();
            Application.DoEvents();
            Assert.Same(inserted, document.Definition.Frames[1]);

            var append = DragAt(frames, insert.Data!, frames.GetItemRect(2).Bottom + 2);
            RaiseDrag(frames, "Enter", append);
            Assert.Equal(3, frames.InsertionIndex);
            RaiseDrag(frames, "Drop", append);
            Assert.Equal(DragDropEffects.Copy, append.Effect);
            Assert.Equal(new[] { first, inserted, last }, document.Definition.Frames.Take(3));
            Assert.Equal(1, document.Definition.Frames[3].XTile);
        }
        finally { Directory.Delete(directory, recursive: true); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DropAddsOnlyExactMemberAndReplaysReferenceAcrossSaveAs(bool packed) => RunSta(() =>
    {
        string directory = CreateTempDirectory();
        try
        {
            var drag = CreateMultiMemberDrag(directory, packed);
            using var host = ShowFrameDragEditor(directory);
            var document = host.Document;
            var original = document.Definition;
            var target = document.AddDefinition();
            document.History.ResetSaved();
            Application.DoEvents();

            var frames = Field<FrameSequenceListView>(host.Editor, "_frames");
            if (packed)
            {
                var basename = new AssetLocation(AssetTypes.TilesheetDefinition, drag.Address.Location.Path, "sheet.gts");
                var incomplete = drag with { Address = new ResourceAddress(basename, drag.Address.Member) };
                var rejected = DragAt(frames, new ResourceDragData<TilesheetFrameDrag>(incomplete), 50);
                RaiseDrag(frames, "Enter", rejected);
                Assert.Equal(DragDropEffects.None, rejected.Effect);
                Assert.False(document.IsDirty);
                Assert.False(document.History.CanUndo);
            }

            var drop = DragAt(frames, new ResourceDragData<TilesheetFrameDrag>(drag), 50);
            RaiseDrag(frames, "Enter", drop);
            Assert.Equal(0, frames.InsertionIndex);
            Assert.Empty(target.Frames);
            Assert.False(document.IsDirty);
            RaiseDrag(frames, "Drop", drop);
            Assert.Equal(DragDropEffects.Copy, drop.Effect);
            Assert.Empty(original.Frames);
            Assert.Empty(original.TilesheetSources);
            var source = Assert.Single(target.TilesheetSources);
            var frame = Assert.Single(target.Frames);
            Assert.Equal(("second", "run", 1, 1), (frame.Tilesheet, frame.RegionName, frame.XTile, frame.YTile));
            Assert.Null(frame.DurationSeconds);
            Assert.Equal(drag.Address, SourceAddress(source, document.BaseDirectory));
            Assert.Equal("Drop tilesheet frame", document.History.UndoDescription);
            Assert.True(document.IsDirty);

            var shortcut = typeof(AnimationEditorControl).GetMethod("ProcessCmdKey", PrivateInstance)!;
            Assert.True((bool)shortcut.Invoke(host.Editor, [new Message(), Keys.Control | Keys.Z])!);
            Assert.Empty(target.Frames);
            Assert.Empty(target.TilesheetSources);
            Assert.False(document.History.CanUndo);
            Assert.False(document.IsDirty);

            // Save As while the dependency is retained only by redo.
            var saveAs = Path.Combine(directory, "exports", "walk.gani");
            document.Save(saveAs, allowInvalid: true);
            Assert.True((bool)shortcut.Invoke(host.Editor, [new Message(), Keys.Control | Keys.Y])!);
            Assert.Same(frame, Assert.Single(target.Frames));
            Assert.Same(source, Assert.Single(target.TilesheetSources));
            Assert.Equal(drag.Address, SourceAddress(source, document.BaseDirectory));
            Assert.True(document.IsDirty);
            document.Save(saveAs, allowInvalid: true);
            document.History.Undo();
            Assert.True(document.IsDirty);
            document.History.Redo();
            Assert.False(document.IsDirty);

            using var reloaded = AnimationDocument.Open(saveAs);
            var saved = reloaded.Definitions[1];
            Assert.Equal(drag.Address, SourceAddress(Assert.Single(saved.TilesheetSources), reloaded.BaseDirectory));
            var savedFrame = Assert.Single(saved.Frames);
            Assert.Equal((frame.Tilesheet, frame.RegionName, frame.XTile, frame.YTile, frame.DurationSeconds),
                (savedFrame.Tilesheet, savedFrame.RegionName, savedFrame.XTile, savedFrame.YTile, savedFrame.DurationSeconds));
        }
        finally { Directory.Delete(directory, recursive: true); }
    });

    [Fact]
    public void RejectedAndCanceledDropsPreserveSavePointAndRedo() => RunSta(() =>
    {
        string directory = CreateTempDirectory();
        try
        {
            string gts = CreateGts(directory);
            string conflicting = CreateGts(Path.Combine(directory, "other"));
            string malformed = Path.Combine(directory, "malformed.gts");
            string duplicate = Path.Combine(directory, "duplicate.gts");
            File.WriteAllText(malformed, "invalid JSON");
            var definition = TilesheetDefinitionSerializer.ToJson(TilesheetDefinitionSerializer.Load(gts));
            File.WriteAllText(duplicate, "{\"Definitions\":[" + definition + "," + definition + "]}");
            using var host = ShowFrameDragEditor(directory);
            var document = host.Document;
            document.SetLooseTilesheetSource("test-sheet", gts);
            document.History.ResetSaved();
            var reference = Assert.Single(document.Definition.TilesheetSources);
            document.AddFrame(new() { Tilesheet = "test-sheet", RegionName = "walk" });
            document.History.Undo();
            Application.DoEvents();

            var frames = Field<FrameSequenceListView>(host.Editor, "_frames");
            TilesheetFrameDrag Drag(string path, string? member = "test-sheet", AssetTypes type = AssetTypes.TilesheetDefinition) =>
                new(new ResourceAddress(new AssetLocation(type, path), member), "walk", 0, 0);

            foreach (var invalid in new[]
            {
                Drag(conflicting),
                Drag(gts, null),
                Drag(gts, type: AssetTypes.AnimationDefinition),
                Drag(gts, "not-a-member"),
                Drag(gts) with { RegionName = "unknown" },
                Drag(gts) with { X = 2 },
                Drag(Path.Combine(directory, "missing.gts"), "unregistered"),
                Drag(malformed, "unregistered"),
                Drag(duplicate, "unregistered")
            })
            {
                var args = DragAt(frames, new ResourceDragData<TilesheetFrameDrag>(invalid), 50);
                RaiseDrag(frames, "Enter", args);
                Assert.Equal(-1, frames.InsertionIndex);
                RaiseDrag(frames, "Drop", args);
                Assert.Equal(DragDropEffects.None, args.Effect);
                AssertBaseline();
            }

            // An external data object with matching format text is not the in-process transport.
            var external = DragAt(frames, new DataObject(typeof(TilesheetFrameDrag).FullName!, Drag(gts)), 50);
            RaiseDrag(frames, "Enter", external);
            Assert.Equal(DragDropEffects.None, external.Effect);

            var canceled = DragAt(frames, new ResourceDragData<TilesheetFrameDrag>(Drag(gts)), 50);
            RaiseDrag(frames, "Enter", canceled);
            Assert.Equal(DragDropEffects.Copy, canceled.Effect);
            RaiseDrag(frames, "Leave", EventArgs.Empty);
            Assert.Equal(-1, frames.InsertionIndex);
            AssertBaseline();

            RaiseDrag(frames, "Enter", canceled);
            File.Delete(gts);
            RaiseDrag(frames, "Drop", canceled);
            Assert.Equal(DragDropEffects.None, canceled.Effect);
            Assert.Contains("Frame drop rejected", Field<TextBox>(host.Editor, "_validation").Text);
            AssertBaseline();

            void AssertBaseline()
            {
                Assert.Empty(document.Definition.Frames);
                Assert.Same(reference, Assert.Single(document.Definition.TilesheetSources));
                Assert.Equal("sheet.gts", reference.GtsPath);
                Assert.False(document.IsDirty);
                Assert.False(document.History.CanUndo);
                Assert.True(document.History.CanRedo);
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    });

    private static AnimationEditorDocument ShowFrameDragEditor(string directory)
    {
        var host = new AnimationEditorDocument(AnimationDocument.Create(directory), (_, _) => false)
        {
            CloseApproved = true,
            Opacity = 0,
            ShowInTaskbar = false,
            Size = new Size(1200, 800)
        };
        host.Show();
        Application.DoEvents();
        return host;
    }

    private static DragEventArgs DragAt(ListView frames, IDataObject data, int y)
    {
        var point = frames.PointToScreen(new Point(15, y));
        return new(data, 0, point.X, point.Y, DragDropEffects.Copy, DragDropEffects.None);
    }

    private static void RaiseDrag(Control target, string phase, EventArgs args) =>
        typeof(Control).GetMethod("OnDrag" + phase, PrivateInstance)!.Invoke(target, [args]);

    private static ResourceAddress SourceAddress(AnimationTilesheetSourceDefinition source, string directory) =>
        new(AuthoringResourceResolver.TilesheetSource(source, directory), source.Tilesheet);

    private static TilesheetFrameDrag CreateMultiMemberDrag(string directory, bool packed)
    {
        string path = CreateGts(directory, "first");
        using var image = new Bitmap(32, 32);
        using var png = new MemoryStream();
        image.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        byte[] imageBytes = png.ToArray();
        File.WriteAllBytes(Path.Combine(directory, "sheet.png"), imageBytes);

        var first = TilesheetDefinitionSerializer.Load(path);
        var second = new TilesheetDefinition
        {
            Name = "second",
            Image = new() { FilePath = "sheet.png" },
            Regions = [new() { Name = "run", Area = new(0, 0, 32, 32), TileSize = new(16, 16) }]
        };
        string? entry = null;
        if (packed)
        {
            path = Path.Combine(directory, "pack.gaf");
            entry = "nested/sheet.gts";
            first.Image = second.Image = new() { AssetEntryName = "images/sheet.png" };
            using var archive = AssetsFile.LoadOrCreate(path, password: null, encrypt: false, register: false);
            using var imageData = new MemoryStream(imageBytes);
            archive.Add(AssetTypes.Image, "images/sheet.png", imageData);
            using var decoy = new MemoryStream(Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToDocumentJson(new() { Definitions = [first] })));
            archive.Add(AssetTypes.TilesheetDefinition, "decoy/sheet.gts", decoy);
            using var data = new MemoryStream(Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToDocumentJson(new() { Definitions = [first, second] })));
            archive.Add(AssetTypes.TilesheetDefinition, entry, data);
            archive.Save();
        }
        else
        {
            TilesheetDefinitionSerializer.SaveAll(path, [first, second]);
        }

        return new(new ResourceAddress(new AssetLocation(AssetTypes.TilesheetDefinition, path, entry), "second"), "run", 1, 1);
    }
}
