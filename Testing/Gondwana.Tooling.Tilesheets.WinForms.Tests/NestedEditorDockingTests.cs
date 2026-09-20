using System.Runtime.ExceptionServices;
using Gondwana.Assets;
using Gondwana.Tooling.Animations.Editing;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Assets.WinForms;
using Gondwana.Tooling.Tilesheets.Editing;
using Gondwana.Tooling.Tilesheets.WinForms;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2015;

namespace Gondwana.Tooling.Tilesheets.WinForms.Tests;

// Run all three public controls through the same embedding contract in the
// existing Windows CI suite. Production editors do not reference one another.
[Collection("WinForms interaction")]
public sealed class NestedEditorDockingTests
{
    [Theory]
    [InlineData("GTS")]
    [InlineData("GAF")]
    [InlineData("GANI")]
    public void PanesStayLocalAndHiddenContentsAreDisposedWithOuterDocument(string kind) => RunSta(() =>
    {
        string directory = Path.Combine(Path.GetTempPath(), "GondwanaDocking-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            using var assets = AssetsFile.LoadOrCreate(Path.Combine(directory, "test.gaf"));
            var tilesheet = TilesheetDocument.Create(directory);
            var animation = AnimationDocument.Create(directory);
            UserControl CreateEditor() => kind switch
            {
                "GTS" => new TilesheetEditorControl(tilesheet),
                "GAF" => new AssetEditorControl(assets),
                _ => new AnimationEditorControl(animation)
            };
            string[] expected = kind switch
            {
                "GTS" => ["Image", "Definition", "Region", "Frame", "Validation"],
                "GAF" => ["Assets", "Status"],
                _ => ["GTS frame sources", "Preview", "Animation frames", "Animation properties", "Validation"]
            };

            // Construction/disposal must also work without ever attaching a host.
            using (var unhosted = CreateEditor())
                Assert.Equal(expected.Length, InnerDock(unhosted).Contents.Count);

            using var theme = new VS2015DarkTheme();
            using var host = new Form { Size = new Size(1400, 900), Opacity = 0, ShowInTaskbar = false };
            using var outer = new DockPanel
            {
                Dock = DockStyle.Fill, Theme = theme, DocumentStyle = DocumentStyle.DockingWindow
            };
            host.Controls.Add(outer);
            using var browser = new DockContent { Text = "Working directory" };
            browser.Show(outer, DockState.DockLeft);
            using var sibling = new DockContent { Text = "Another document" };
            using var siblingEditor = CreateEditor();
            sibling.Controls.Add(siblingEditor);
            sibling.Show(outer, DockState.Document);
            host.Show();

            // Reopen the same model in a fresh editor: hidden/rearranged panes
            // must not leak into the next instance or a sibling document.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var document = new DockContent { Text = kind };
                using var editor = CreateEditor();
                document.Controls.Add(editor);
                document.Show(outer, DockState.Document);
                Application.DoEvents();
                var inner = InnerDock(editor);
                var panes = inner.Contents.Cast<DockContent>().ToArray();
                Assert.Equal(expected.Order(), panes.Select(pane => pane.Text).Order());
                Assert.NotSame(outer, inner);
                Assert.NotSame(InnerDock(siblingEditor), inner);
                Assert.Equal(3, outer.Contents.Count);
                Assert.All(panes, pane =>
                {
                    Assert.Same(inner, pane.DockPanel);
                    Assert.DoesNotContain(pane, outer.Contents.Cast<IDockContent>());
                    Assert.True(pane.Visible);
                    Assert.True(pane.HideOnClose);
                    Assert.False(pane.DockHandler.IsDockStateValid(DockState.Float));
                    Assert.True(pane.AllowEndUserDocking);
                    Assert.True(pane.ClientSize.Width > 0 && pane.ClientSize.Height > 0);
                });
                Assert.Empty(inner.FloatWindows);
                AssertDefaultPlacement(kind, panes);

                var children = panes.SelectMany(pane => Descendants(pane)).ToArray();
                // Exercise the same fill-drop path as dragging onto each pane,
                // including GANI sources, GTS inspectors, and GAF status.
                foreach (var target in panes.Reverse())
                {
                    var source = panes.First(pane => pane != target);
                    source.DockHandler.DockTo(target.Pane, DockStyle.Fill, -1);
                    Application.DoEvents();
                    Assert.Same(target.Pane, source.Pane);
                    Assert.Equal(2, target.Pane.DisplayingContents.Count);
                    Assert.False(target.IsHidden);
                    Assert.False(source.IsHidden);
                    Assert.Equal(DockPane.AppearanceStyle.Document, target.Pane.Appearance);
                    var strip = Assert.Single(target.Pane.Controls.OfType<DockPaneStripBase>());
                    Assert.True(strip.Visible && strip.Height > 0);
                    Assert.True(strip.Bottom <= source.Top, "Merged panes must expose their tabs above the content.");
                    target.Activate();
                    Assert.Same(target, target.Pane.ActiveContent);
                    Assert.True(target.Visible);
                    source.Activate();
                    Assert.Same(source, source.Pane.ActiveContent);
                    Assert.True(source.Visible);
                    source.Show(target.Pane, DockAlignment.Bottom, .3);
                }
                var first = panes[0];
                var last = panes[^1];
                last.Show(first.Pane, first);
                Assert.Same(first.Pane, last.Pane);
                last.Show(first.Pane, DockAlignment.Bottom, .3);
                Assert.NotSame(first.Pane, last.Pane);
                Assert.All(panes, pane => Assert.Same(inner, pane.DockPanel));
                Assert.Throws<ArgumentException>(() => last.DockHandler.DockTo(outer, DockStyle.Right));
                Assert.Throws<ArgumentException>(() => last.DockHandler.DockTo(InnerDock(siblingEditor), DockStyle.Right));
                last.Activate();
                Application.DoEvents();
                Assert.Same(document, outer.ActiveDocument);
                // Follow the caption/tab close button path, which honors HideOnClose.
                var hiddenPane = last.Pane;
                last.Pane.CloseActiveContent();
                Assert.True(last.IsHidden);
                Assert.False(last.IsDisposed);
                Assert.False(editor.IsDisposed);
                Assert.False(document.IsDisposed);

                bool IsPaneVisible(string paneName) => kind switch
                {
                    "GTS" => ((TilesheetEditorControl)editor).IsPaneVisible(paneName),
                    "GAF" => ((AssetEditorControl)editor).IsPaneVisible(paneName),
                    _ => ((AnimationEditorControl)editor).IsPaneVisible(paneName)
                };

                bool ShowPane(string paneName) => kind switch
                {
                    "GTS" => ((TilesheetEditorControl)editor).ShowPane(paneName),
                    "GAF" => ((AssetEditorControl)editor).ShowPane(paneName),
                    _ => ((AnimationEditorControl)editor).ShowPane(paneName)
                };

                void ShowAllPanes()
                {
                    switch (kind)
                    {
                        case "GTS":
                            ((TilesheetEditorControl)editor).ShowAllPanes();
                            break;
                        case "GAF":
                            ((AssetEditorControl)editor).ShowAllPanes();
                            break;
                        default:
                            ((AnimationEditorControl)editor).ShowAllPanes();
                            break;
                    }
                }

                Assert.False(IsPaneVisible(last.Text));
                Assert.True(ShowPane(last.Text));
                Application.DoEvents();
                Assert.False(last.IsHidden);
                Assert.True(IsPaneVisible(last.Text));
                Assert.Same(hiddenPane, last.Pane);
                Assert.False(ShowPane("Not a real pane"));

                first.Activate();
                first.Pane.CloseActiveContent();
                Application.DoEvents();
                Assert.True(first.IsHidden);

                ShowAllPanes();
                Application.DoEvents();
                Assert.All(panes, pane => Assert.False(pane.IsHidden));

                document.Close();
                Application.DoEvents();
                Assert.True(editor.IsDisposed);
                Assert.True(inner.IsDisposed);
                Assert.All(panes, pane => Assert.True(pane.IsDisposed));
                Assert.All(children, child => Assert.True(child.IsDisposed));
                Assert.Equal(2, outer.Contents.Count);
                Assert.False(siblingEditor.IsDisposed);
                Assert.Equal(expected.Length, InnerDock(siblingEditor).Contents.Count);
                // Closed controls must have unsubscribed from their model.
                tilesheet.MarkChanged();
                animation.MarkChanged();
                Application.DoEvents();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void AssetFiltersSelectionAndSaveWorkAfterRearrangingPanes() => RunSta(() =>
    {
        string directory = Path.Combine(Path.GetTempPath(), "GondwanaDocking-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "test.gaf");
            using (var assets = AssetsFile.LoadOrCreate(path))
            {
                assets.Add(AssetTypes.Misc, "notes", new MemoryStream([1, 2, 3]));
                assets.Add(AssetTypes.Image, "sprite", new MemoryStream([4, 5]));
                using var host = new Form { Size = new Size(1100, 750), Opacity = 0, ShowInTaskbar = false };
                using var editor = new AssetEditorControl(assets);
                host.Controls.Add(editor);
                host.Show();
                Application.DoEvents();
                var grid = Assert.Single(Descendants(editor).OfType<DataGridView>());
                var items = Descendants(editor).OfType<ToolStrip>().SelectMany(strip => strip.Items.Cast<ToolStripItem>()).ToArray();
                var type = Assert.Single(items.OfType<ToolStripComboBox>());
                var search = Assert.Single(items.OfType<ToolStripTextBox>());
                Assert.Equal(2, grid.Rows.Count);
                var dock = InnerDock(editor);
                var status = dock.Contents.Cast<DockContent>().Single(pane => pane.Text == "Status");
                var entries = dock.Contents.Cast<DockContent>().Single(pane => pane.Text == "Assets");
                status.Show(entries.Pane, DockAlignment.Right, .25);
                type.SelectedItem = AssetTypes.Misc;
                Assert.Single(grid.Rows.Cast<DataGridViewRow>());
                Assert.Equal("notes", grid.Rows[0].Cells[1].Value);
                type.SelectedIndex = 0;
                search.Text = "sprite";
                Assert.Single(grid.Rows.Cast<DataGridViewRow>());
                grid.Rows[0].Selected = true;
                Assert.True(items.Single(item => item.Text == "Replace").Enabled);
                search.Text = "missing";
                Assert.Empty(grid.Rows.Cast<DataGridViewRow>());
                Assert.False(items.Single(item => item.Text == "Replace").Enabled);
                search.Text = "";
                int changed = 0;
                editor.WorkspaceChanged = () => changed++;
                items.Single(item => item.Text == "Save").PerformClick();
                Assert.Equal(1, changed);
                Assert.Contains("Saved:", Assert.Single(items.OfType<ToolStripStatusLabel>()).Text);
            }
            using var reopened = AssetsFile.LoadOrCreate(path);
            using var reopenedEditor = new AssetEditorControl(reopened);
            Assert.Equal(2, Assert.Single(Descendants(reopenedEditor).OfType<DataGridView>()).Rows.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    private static void AssertDefaultPlacement(string kind, DockContent[] panes)
    {
        Point At(string name) => panes.Single(pane => pane.Text == name).PointToScreen(Point.Empty);
        if (kind == "GTS")
        {
            Assert.True(At("Definition").X > At("Image").X);
            Assert.True(At("Definition").Y < At("Region").Y);
            Assert.True(At("Region").Y < At("Frame").Y);
            Assert.True(At("Validation").Y > At("Image").Y);
        }
        else if (kind == "GANI")
        {
            Assert.True(At("GTS frame sources").X < At("Preview").X);
            Assert.True(At("Preview").Y < At("Animation frames").Y);
            Assert.True(At("Animation frames").X < At("Animation properties").X);
            Assert.True(At("Animation properties").Y < At("Validation").Y);
        }
        else
            Assert.True(At("Assets").Y < At("Status").Y);
    }

    private static DockPanel InnerDock(Control editor) => Assert.Single(Descendants(editor).OfType<DockPanel>());

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

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
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Nested docking test timed out.");
        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}
