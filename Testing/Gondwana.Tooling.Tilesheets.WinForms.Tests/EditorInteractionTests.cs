using System.Reflection;
using System.Runtime.ExceptionServices;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using Gondwana.Tooling.Tilesheets.Editing;
using Gondwana.Tooling.Tilesheets.WinForms;

namespace Gondwana.Tooling.Tilesheets.WinForms.Tests;

/// <summary>Exercise real Windows controls; descriptor-only tests cannot verify editing or focus.</summary>
public sealed class EditorInteractionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void CtrlWheel_UsesOnePointTwoFiveZoomStepsAndPreservesOrdinaryScrolling() => RunSta(() =>
    {
        using var bitmap = new Bitmap(1000, 1000);
        using var viewport = new ImageViewport { Image = bitmap, Size = new Size(300, 300) };
        viewport.CreateControl();
        var point = new Point(100, 100);
        Assert.False(viewport.ZoomWithMouseWheel(point, 120, false));
        Assert.Equal(1f, viewport.Zoom);
        Assert.True(viewport.ZoomWithMouseWheel(point, 120, true));
        Assert.Equal(1.25f, viewport.Zoom, 3);
        Assert.True(viewport.ZoomWithMouseWheel(point, -120, true));
        Assert.Equal(1f, viewport.Zoom);
        viewport.ZoomWithMouseWheel(point, 60, true);
        Assert.Equal(1f, viewport.Zoom);
        viewport.ZoomWithMouseWheel(point, 60, true);
        Assert.Equal(1.25f, viewport.Zoom, 3);
        viewport.ZoomWithMouseWheel(point, -240, true);
        Assert.Equal(.8f, viewport.Zoom, 3);
        Assert.False(viewport.ZoomWithMouseWheel(new Point(-1, -1), 120, true));
        Assert.Equal(.8f, viewport.Zoom, 3);
        viewport.SetZoom(16);
        viewport.ZoomWithMouseWheel(point, 120, true);
        Assert.Equal(16f, viewport.Zoom);
    });

    [Fact]
    public void ToolbarZoomButtons_UseOnePointTwoFiveZoomSteps() => RunSta(() =>
    {
        var document = TilesheetDocument.Create(Path.Combine(AppContext.BaseDirectory, "assets"));
        using var editor = Show(document);
        var viewport = Field<ImageViewport>(editor, "_viewport");
        var buttons = Descendants(editor).OfType<ToolStrip>().SelectMany(strip => strip.Items.Cast<ToolStripItem>()).ToArray();
        var zoomOut = buttons.Single(item => item.Text == "−");
        var zoomIn = buttons.Single(item => item.Text == "+");

        Assert.Equal(1f, viewport.Zoom);
        zoomIn.PerformClick();
        Assert.Equal(1.25f, viewport.Zoom, 3);
        zoomOut.PerformClick();
        Assert.Equal(1f, viewport.Zoom, 3);
        zoomOut.PerformClick();
        Assert.Equal(.8f, viewport.Zoom, 3);
    });

    [Theory]
    [InlineData("forest")]
    [InlineData("ganon")]
    [InlineData("link")]
    [InlineData("lightworld_enemies")]
    [InlineData("npcs")]
    public void LoadedZeldaDefinition_ClickSelectsFramesInOtherRegions(string name) => RunSta(() =>
    {
        var document = TilesheetDocument.Open(Path.Combine(AppContext.BaseDirectory, "assets", name + ".gts"));
        string before = TilesheetDefinitionSerializer.ToJson(document.Definition);
        using var editor = Show(document);
        var definitionGrid = Field<PropertyGrid>(editor, "_definitionProperties");
        var regionGrid = Field<PropertyGrid>(editor, "_regionProperties");
        var frameGrid = Field<PropertyGrid>(editor, "_frameProperties");
        Assert.True(definitionGrid.Visible && regionGrid.Visible && frameGrid.Visible);
        Assert.True(definitionGrid.PointToScreen(Point.Empty).Y < regionGrid.PointToScreen(Point.Empty).Y);
        Assert.True(regionGrid.PointToScreen(Point.Empty).Y < frameGrid.PointToScreen(Point.Empty).Y);
        var viewport = Field<ImageViewport>(editor, "_viewport");
        viewport.Fit();
        var region = document.Definition.Regions.Last();
        var bounds = FrameGeometry.Bounds(region, 0, 0);
        Click(viewport, new PointF(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f));
        Assert.Same(region, viewport.SelectedRegion);
        Assert.Equal(Point.Empty, viewport.SelectedFrame);
        var grid = Field<PropertyGrid>(editor, "_frameProperties");
        Assert.Contains(Items(grid), item => item.PropertyDescriptor?.Name == "XTile");
        Assert.Equal(before, TilesheetDefinitionSerializer.ToJson(document.Definition));
        Assert.False(document.IsDirty);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Inspector_CommitsTextNumbersAndCollisionOverrides_AndKeepsSelectedProperty(bool fromImage) => RunSta(() =>
    {
        var assets = Path.Combine(AppContext.BaseDirectory, "assets");
        var document = fromImage ? TilesheetDocument.Create(assets) : TilesheetDocument.Open(Path.Combine(assets, "forest.gts"));
        using var editor = Show(document);
        if (fromImage)
        {
            editor.ChooseImage(Path.Combine(assets, "forest.png"));
            editor.AddRegion();
        }
        var grid = Field<PropertyGrid>(editor, "_frameProperties");
        grid = Field<PropertyGrid>(editor, "_definitionProperties");
        Edit(grid, "Name", "Edited in the inspector");
        Assert.Equal("Edited in the inspector", document.Definition.Name);
        Edit(grid, "PremultiplyAlpha", "True");
        Assert.True(document.Definition.PremultiplyAlpha);

        grid = Field<PropertyGrid>(editor, "_regionProperties");
        var region = document.Definition.Regions[0];
        Edit(grid, "CollisionAdjust.Left", "-3");
        Assert.Equal(-3, region.CollisionAdjust.Left);
        Assert.Equal("CollisionAdjust.Left", grid.SelectedGridItem?.PropertyDescriptor?.Name);
        Edit(grid, "CollisionAdjust.Left", "-4", enter: false);
        Assert.Equal(-4, region.CollisionAdjust.Left);
        Edit(grid, "Overhang.Top", "2");
        Assert.Equal(2, region.Overhang.Top);

        var viewport = Field<ImageViewport>(editor, "_viewport");
        viewport.Fit();
        var bounds = FrameGeometry.Bounds(region, 0, 0);
        Click(viewport, new PointF(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f));
        grid = Field<PropertyGrid>(editor, "_frameProperties");
        Assert.True(Items(grid).Single(i => i.PropertyDescriptor?.Name == "Left").PropertyDescriptor!.IsReadOnly);
        Edit(grid, "CollisionAdjust mode", "Override");
        Edit(grid, "Left", "-7");
        Edit(grid, "CollisionType", "Blocking");
        var frame = document.FindFrame(region, 0, 0)!;
        Assert.Equal(-7, frame.CollisionAdjust!.Value.Left);
        Assert.Equal(TileCollisionType.Blocking, frame.CollisionType);
        Assert.True(document.IsDirty);

        string output = Path.Combine(Path.GetTempPath(), "GtsInspector_" + Guid.NewGuid().ToString("N") + ".gts");
        try
        {
            document.Save(output, editor.ImageSize);
            var loaded = TilesheetDefinitionSerializer.Load(output);
            Assert.Equal("Edited in the inspector", loaded.Name);
            Assert.Equal(-7, loaded.Regions[0].Frames[0].CollisionAdjust!.Value.Left);
            Assert.Equal(TileCollisionType.Blocking, loaded.Regions[0].Frames[0].CollisionType);
        }
        finally { File.Delete(output); }
    });

    [Fact]
    public void OverlayColors_UpdateOpenDocumentsAndLegendWithoutDirtyingGts() => RunSta(() =>
    {
        string path = Path.Combine(Path.GetTempPath(), "GtsColors_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var settings = OverlaySettings.Load(path);
            var source = Path.Combine(AppContext.BaseDirectory, "assets", "forest.gts");
            using var first = Show(TilesheetDocument.Open(source), settings);
            using var second = Show(TilesheetDocument.Open(source), settings);
            var menu = Descendants(first).OfType<ToolStrip>().SelectMany(strip => strip.Items.Cast<ToolStripItem>())
                .OfType<ToolStripDropDownButton>().Single(item => item.Text == "Overlays / legend");
            Assert.Equal(Enum.GetValues<OverlayKind>().Length, menu.DropDownItems.Count);
            foreach (ToolStripControlHost item in menu.DropDownItems)
            {
                Assert.Single(Descendants(item.Control).OfType<Button>(), button => button.Text == "...");
                Assert.Single(Descendants(item.Control).OfType<CheckBox>());
                var layout = Descendants(item.Control).OfType<TableLayoutPanel>().Single();
                layout.PerformLayout();
                foreach (Control control in layout.Controls)
                    Assert.True(control.Bottom <= layout.Height, "Legend controls must fit inside their row.");
            }
            settings.SetColor(OverlayKind.Collision, Color.Lime);
            Application.DoEvents();
            Assert.Equal(Color.Lime.ToArgb(), Field<ImageViewport>(first, "_viewport").Colors[OverlayKind.Collision].ToArgb());
            Assert.Equal(Color.Lime.ToArgb(), Field<ImageViewport>(second, "_viewport").Colors[OverlayKind.Collision].ToArgb());
            Assert.False(first.Document.IsDirty);
            Assert.False(second.Document.IsDirty);
        }
        finally { File.Delete(path); }
    });

    private static EditorDocument Show(TilesheetDocument document, OverlaySettings? settings = null)
    {
        var editor = new EditorDocument(document, (_, _) => false, settings)
        {
            Opacity = 0, ShowInTaskbar = false, Size = new Size(1200, 800), CloseApproved = true
        };
        editor.Show();
        Application.DoEvents();
        return editor;
    }

    private static void Click(ImageViewport viewport, PointF sourcePoint)
    {
        var position = new Point((int)(sourcePoint.X * viewport.Zoom) + viewport.AutoScrollPosition.X,
            (int)(sourcePoint.Y * viewport.Zoom) + viewport.AutoScrollPosition.Y);
        typeof(ImageViewport).GetMethod("OnMouseDown", PrivateInstance)!.Invoke(viewport,
            [new MouseEventArgs(MouseButtons.Left, 1, position.X, position.Y, 0)]);
        Application.DoEvents();
    }

    private static void Edit(PropertyGrid grid, string property, string text, bool enter = true)
    {
        var item = Items(grid).Single(i => i.PropertyDescriptor?.Name == property);
        Assert.False(item.PropertyDescriptor!.IsReadOnly);
        grid.SelectedGridItem = item;
        grid.Focus();
        Application.DoEvents();
        var textbox = Descendants(grid).OfType<TextBox>().Single(t => t.Visible);
        if (textbox.ReadOnly)
        {
            // Booleans and enums use an exclusive drop-down, not a writable textbox.
            var view = Descendants(grid).Single(control => control.GetType().Name == "PropertyGridView");
            Exception? failure = null;
            view.BeginInvoke(() =>
            {
                try
                {
                    var list = (ListBox)view.GetType().GetProperty("DropDownListBox", PrivateInstance)!.GetValue(view)!;
                    int index = list.Items.Cast<object>().Select((value, i) => (value, i)).Single(pair => pair.value.ToString() == text).i;
                    list.SelectedIndex = index;
                    view.GetType().GetMethod("OnListClick", PrivateInstance)!.Invoke(view, [list, EventArgs.Empty]);
                }
                catch (Exception ex) { failure = ex; }
                finally { view.GetType().GetMethod("CloseDropDown", PrivateInstance)!.Invoke(view, null); }
            });
            view.GetType().GetMethod("F4Selection", PrivateInstance)!.Invoke(view, [false]);
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
            Application.DoEvents();
            return;
        }
        textbox.Focus();
        textbox.Text = text;
        if (enter)
        {
            // Follow the textbox's actual Enter/commit routing, including type conversion.
            Assert.True((bool)textbox.GetType().GetMethod("ProcessDialogKey", PrivateInstance)!.Invoke(textbox, [Keys.Enter])!);
        }
        else
        {
            using var focusTarget = new TextBox();
            grid.FindForm()!.Controls.Add(focusTarget);
            focusTarget.BringToFront();
            focusTarget.Focus();
        }
        Application.DoEvents();
    }

    private static IEnumerable<GridItem> Items(PropertyGrid grid)
    {
        var root = grid.SelectedGridItem!;
        while (root.Parent is not null) root = root.Parent;
        return Walk(root);
        static IEnumerable<GridItem> Walk(GridItem item)
        {
            yield return item;
            foreach (GridItem child in item.GridItems)
                foreach (var descendant in Walk(child)) yield return descendant;
        }
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            yield return control;
            foreach (var descendant in Descendants(control)) yield return descendant;
        }
    }

    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, PrivateInstance)!.GetValue(instance)!;

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
            catch (Exception ex) { error = ex; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Windows interaction test timed out.");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
