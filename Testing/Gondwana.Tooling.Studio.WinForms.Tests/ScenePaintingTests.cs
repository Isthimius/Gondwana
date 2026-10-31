using System.Reflection;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Scenes.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Fact]
    public void StudioScenePaintingAndEraseUseSharedEditorHistory() => RunSta(root =>
    {
        using var studio = Host(root);
        var child = studio.NewDocument("gscn");
        var editor = Assert.IsType<SceneEditorControl>(child.Document.Editor);
        var document = editor.Document;
        var layer = document.AddLayer();
        string gts = Path.Combine(root, "paint.gts");
        using (var image = new Bitmap(16, 16)) image.Save(Path.Combine(root, "paint.png"));
        TilesheetDefinitionSerializer.Save(gts, new TilesheetDefinition
        {
            Name = "terrain",
            Image = new() { FilePath = "paint.png" },
            Regions = [new() { Name = "default", Area = new(0, 0, 16, 16), TileSize = new(16, 16) }]
        });
        editor.AddTilesheetSources([gts]); Application.DoEvents();
        var source = Descendants(editor).OfType<TreeView>().Single(tree =>
            tree.Nodes.Count > 0 && tree.Nodes[0].Text.StartsWith("terrain", StringComparison.Ordinal));
        var region = source.Nodes[0].Nodes[0]; region.Expand();
        var row = region.Nodes[0]; row.Expand();
        source.SelectedNode = row.Nodes[0];
        var buttons = Descendants(editor).OfType<ToolStrip>()
            .SelectMany(bar => bar.Items.OfType<ToolStripButton>()).ToArray();
        ToolStripButton Tool(string name) => buttons.Single(button => button.Text == name);
        Assert.True(Tool("Select").Checked);
        Tool("Paint").PerformClick(); Assert.False(Tool("Select").Checked);
        var preview = Descendants(editor).Single(control => control.GetType().Name == "ScenePreviewControl");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        preview.GetType().GetMethod("UpdateOffset", flags)!.Invoke(preview, null);
        var offset = (PointF)preview.GetType().GetField("_offset", flags)!.GetValue(preview)!;
        float zoom = (float)preview.GetType().GetProperty("Zoom")!.GetValue(preview)!;
        void Click()
        {
            foreach (var method in new[] { "OnMouseDown", "OnMouseUp" })
                preview.GetType().GetMethod(method, flags)!.Invoke(preview,
                    [new MouseEventArgs(MouseButtons.Left, 1, (int)(offset.X + layer.TileWidth * 1.5f * zoom),
                        (int)(offset.Y + layer.TileHeight * 1.5f * zoom), 0)]);
        }
        Click(); Application.DoEvents();
        var tile = Assert.Single(layer.Tiles);
        Assert.Equal("terrain", tile.Frame!.Tilesheet);
        Assert.Equal("default", tile.Frame.RegionName);
        tile.Nickname = "original";
        Tool("Erase").PerformClick(); Assert.False(Tool("Paint").Checked);
        Click(); Application.DoEvents();
        Assert.Empty(layer.Tiles);
        Assert.EndsWith(" *", child.Text);
        var edit = studio.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "&Edit");
        edit.DropDownItems[0].PerformClick(); Application.DoEvents();
        Assert.Equal("original", Assert.Single(layer.Tiles).Nickname);
        edit.DropDownItems[1].PerformClick(); Application.DoEvents();
        Assert.Empty(layer.Tiles);

        // The same shared canvas supports rectangle commands and grouped drag strokes in Studio.
        Point Cell(int x, int y) => new((int)(offset.X + layer.TileWidth * (x + .5f) * zoom),
            (int)(offset.Y + layer.TileHeight * (y + .5f) * zoom));
        void Pointer(string method, Point point) => preview.GetType().GetMethod(method, flags)!.Invoke(preview,
            [new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)]);
        void Command(string text) => Descendants(editor).OfType<ToolStrip>().SelectMany(bar => bar.Items.Cast<ToolStripItem>())
            .Single(item => item.Text == text).PerformClick();
        Tool("Select").PerformClick();
        Pointer("OnMouseDown", Cell(1, 1)); Pointer("OnMouseMove", Cell(3, 2)); Pointer("OnMouseUp", Cell(3, 2));
        Command("Apply to selection"); Application.DoEvents();
        Assert.Equal(6, layer.Tiles.Count);
        edit.DropDownItems[0].PerformClick(); Application.DoEvents(); Assert.Empty(layer.Tiles);
        edit.DropDownItems[1].PerformClick(); Application.DoEvents(); Assert.Equal(6, layer.Tiles.Count);
        Command("Erase selection"); Application.DoEvents(); Assert.Empty(layer.Tiles);
        edit.DropDownItems[0].PerformClick(); Application.DoEvents(); Assert.Equal(6, layer.Tiles.Count);
        edit.DropDownItems[1].PerformClick(); Application.DoEvents(); Assert.Empty(layer.Tiles);
        Tool("Paint").PerformClick();
        Pointer("OnMouseDown", Cell(1, 1)); Pointer("OnMouseMove", Cell(5, 1)); Pointer("OnMouseUp", Cell(5, 1));
        Application.DoEvents(); Assert.Equal(5, layer.Tiles.Count);
        Assert.Equal("Paint stroke", document.History.UndoDescription);
        Assert.False(document.History.InTransaction);
        edit.DropDownItems[0].PerformClick(); Application.DoEvents(); Assert.Empty(layer.Tiles);
        edit.DropDownItems[1].PerformClick(); Application.DoEvents(); Assert.Equal(5, layer.Tiles.Count);
        Tool("Rectangle Erase").PerformClick();
        Pointer("OnMouseDown", Cell(5, 1)); Pointer("OnMouseMove", Cell(1, 1));
        Assert.Equal(5, layer.Tiles.Count);
        Pointer("OnMouseUp", Cell(1, 1)); Assert.Empty(layer.Tiles);
        edit.DropDownItems[0].PerformClick(); Application.DoEvents(); Assert.Equal(5, layer.Tiles.Count);
        Tool("Rectangle Paint").PerformClick();
        Pointer("OnMouseDown", Cell(1, 2)); Pointer("OnMouseUp", Cell(3, 3));
        Assert.Equal(11, layer.Tiles.Count);
        edit.DropDownItems[0].PerformClick(); Application.DoEvents(); Assert.Equal(5, layer.Tiles.Count);
        Tool("Fill").PerformClick();
        Pointer("OnMouseDown", Cell(1, 2)); Pointer("OnMouseUp", Cell(1, 2));
        Assert.Equal(layer.Columns * layer.Rows, layer.Tiles.Count);
        edit.DropDownItems[0].PerformClick(); Application.DoEvents(); Assert.Equal(5, layer.Tiles.Count);
    });
}
