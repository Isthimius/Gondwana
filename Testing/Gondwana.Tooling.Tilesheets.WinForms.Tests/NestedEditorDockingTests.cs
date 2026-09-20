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
                var first = panes[0];
                var last = panes[^1];
                last.Show(first.Pane, first);
                Assert.Same(first.Pane, last.Pane);
                last.Show(first.Pane, DockAlignment.Bottom, .3);
                Assert.NotSame(first.Pane, last.Pane);
                Assert.All(panes, pane => Assert.Same(inner, pane.DockPanel));
                // Follow the caption/tab close button path, which honors HideOnClose.
                last.Pane.CloseActiveContent();
                Assert.True(last.IsHidden);
                Assert.False(last.IsDisposed);
                Assert.False(editor.IsDisposed);
                Assert.False(document.IsDisposed);

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
