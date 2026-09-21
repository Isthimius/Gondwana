using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Xml.Linq;
using Gondwana.Tooling.Studio.WinForms;
using Gondwana.Tooling.Studio.WinForms.Documents;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed class DockLayoutTests
{
    private static string Root => (string)AppContext.GetData("Gondwana.Tooling.SettingsRoot")!;
    private static string Layout(string profile, string app = "test-host") => Path.Combine(Root, app, "Docking", profile + ".xml");

    [Theory]
    [InlineData(typeof(Gondwana.Tooling.Assets.WinForms.AssetEditorControl))]
    [InlineData(typeof(Gondwana.Tooling.Tilesheets.WinForms.TilesheetEditorControl))]
    [InlineData(typeof(Gondwana.Tooling.Animations.WinForms.AnimationEditorControl))]
    [InlineData(typeof(Gondwana.Tooling.Audio.WinForms.AudioEditorControl))]
    [InlineData(typeof(Gondwana.Tooling.Scenes.WinForms.SceneEditorControl))]
    [InlineData(typeof(Gondwana.Tooling.Sprites.WinForms.SpriteEditorControl))]
    public void StandaloneShellRestoresHiddenToolAndViewResetsIt(Type editorType) => Sta(() =>
    {
        Form Open()
        {
            var type = editorType.Assembly.GetType(editorType.Namespace + ".MainForm")!;
            var form = (Form)Activator.CreateInstance(type, nonPublic: true)!;
            form.Opacity = 0;
            form.ShowInTaskbar = false;
            form.Show();
            Application.DoEvents();
            return form;
        }
        using (var first = Open())
        {
            var dock = first.Controls.OfType<DockPanel>().Single();
            var tool = dock.Contents.OfType<DockContent>().Single(p => p.HideOnClose);
            tool.Show(dock, DockState.DockRight);
            dock.DockRightPortion = .42;
            tool.Hide();
        }
        using (var second = Open())
        {
            var dock = second.Controls.OfType<DockPanel>().Single();
            var tool = dock.Contents.OfType<DockContent>().Single(p => p.HideOnClose);
            Assert.True(tool.IsHidden);
            Assert.Equal(DockState.DockRight, tool.Pane.DockState);
            Assert.InRange(dock.DockRightPortion, .41, .43);
            var view = second.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "&View");
            view.ShowDropDown();
            var item = view.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == tool.Text);
            Assert.False(item.Checked);
            item.PerformClick();
            Assert.False(tool.IsHidden);
            Assert.Equal(DockState.DockRight, tool.DockState);
            view.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "Reset application layout").PerformClick();
            view.HideDropDown();
            Assert.Equal(DockState.DockLeft, tool.DockState);
            Assert.False(File.Exists(Layout("shell")));
        }
        using (var third = Open())
        {
            var tool = third.Controls.OfType<DockPanel>().Single().Contents.OfType<DockContent>().Single(p => p.HideOnClose);
            Assert.False(tool.IsHidden);
            Assert.Equal(DockState.DockLeft, tool.DockState);
        }
    });

    [Fact]
    public void UnwritablePreferenceLocationDoesNotBreakAuthoringOrReset() => Sta(() =>
    {
        // A regular file in place of the application's directory deterministically denies writes.
        File.WriteAllText(Path.Combine(Root, "test-host"), "occupied");
        using var editor = Editor("gts");
        Panes(editor.Model).Single(p => p.Text == "Validation").Hide();
        editor.Model.ResetLayout();
        Assert.True(editor.Model.IsPaneVisible("Validation"));
        Panes(editor.Model).Single(p => p.Text == "Validation").Hide();
    });

    [Theory]
    [InlineData("gaf", 2)]
    [InlineData("gts", 5)]
    [InlineData("gani", 5)]
    [InlineData("gsnd", 3)]
    [InlineData("gscn", 7)]
    [InlineData("gspr", 6)]
    public void EditorSplitTabsSizingVisibilityRecoveryAndResetSurviveNewInstances(string kind, int count) => Sta(() =>
    {
        string[] names;
        DockContent[] owned;
        int splitWidth;
        using (var editor = Editor(kind))
        {
            var panes = Panes(editor.Model);
            Assert.Equal(count, panes.Length);
            Assert.All(panes, pane => Assert.False(pane.IsHidden));
            names = panes.Select(pane => pane.Text).ToArray();
            panes[1].Show(panes[0].Pane, DockAlignment.Right, .62);
            Application.DoEvents();
            splitWidth = panes[1].Width;
            panes[1].Hide();
            owned = panes;
        }
        Assert.All(owned, pane => Assert.True(pane.IsDisposed));
        Assert.True(File.Exists(Layout(kind)));
        using (var editor = Editor(kind))
        {
            var panes = names.Select(name => Panes(editor.Model).Single(p => p.Text == name)).ToArray();
            Assert.True(panes[1].IsHidden);
            Assert.False(panes[1].IsDisposed);
            Assert.NotSame(panes[0].Pane, panes[1].Pane);
            Assert.Same(panes[0].Pane, panes[1].Pane.NestedDockingStatus.PreviousPane);
            Assert.Equal(DockAlignment.Right, panes[1].Pane.NestedDockingStatus.Alignment);
            Assert.InRange(panes[1].Pane.NestedDockingStatus.Proportion, .60, .64);
            Assert.True(editor.Model.ShowPane(names[1]));
            Application.DoEvents();
            Assert.False(panes[1].IsHidden);
            Assert.InRange(panes[1].Width, splitWidth - 10, splitWidth + 10);
            Assert.All(panes, pane =>
            {
                Assert.False(pane.DockHandler.IsDockStateValid(DockState.Float));
                Assert.False(pane.DockHandler.IsDockStateValid(DockState.DockLeftAutoHide));
            });
            panes[1].DockHandler.DockTo(panes[0].Pane, DockStyle.Fill, -1);
            panes[1].Hide();
        }
        using (var editor = Editor(kind))
        {
            var panes = names.Select(name => Panes(editor.Model).Single(p => p.Text == name)).ToArray();
            Assert.Same(panes[0].Pane, panes[1].Pane);
            Assert.True(panes[1].IsHidden);
            editor.Model.ShowAllPanes();
            Assert.All(panes, pane => Assert.False(pane.IsHidden));
            editor.Model.ResetLayout();
            Assert.False(File.Exists(Layout(kind)));
            Assert.Equal(count, Panes(editor.Model).Select(p => p.Pane).Distinct().Count());
        }
        using (var editor = Editor(kind))
        {
            Assert.All(Panes(editor.Model), pane => Assert.False(pane.IsHidden));
            Assert.Equal(count, Panes(editor.Model).Select(p => p.Pane).Distinct().Count());
        }
    });

    [Fact]
    public void ProfilesAreIsolatedAndUnchangedSiblingCannotOverwriteLatestChange() => Sta(() =>
    {
        using (var unchanged = Editor("gts"))
        {
            using var changed = Editor("gts");
            Panes(changed.Model).Single(p => p.Text == "Validation").Hide();
            // The timer persists while the editor is still open, after the layout settles.
            PumpUntil(() => File.Exists(Layout("gts")) && XDocument.Load(Layout("gts")).Root!.Element("Contents")!.Elements().Any(e => (string?)e.Attribute("PersistString") == "gts.validation" && (string?)e.Attribute("IsHidden") == "True"));
            using var next = Editor("gts");
            Assert.False(next.Model.IsPaneVisible("Validation"));
        }
        using (var next = Editor("gts")) Assert.False(next.Model.IsPaneVisible("Validation"));
        using (var other = Editor("gani")) Assert.True(other.Model.IsPaneVisible("Validation"));
        AppContext.SetData("Gondwana.Tooling.ApplicationId", "standalone-gscn");
        using (var editor = Editor("gscn")) Panes(editor.Model).Single(p => p.Text == "Validation").Hide();
        AppContext.SetData("Gondwana.Tooling.ApplicationId", "studio");
        using (var editor = Editor("gscn")) Assert.True(editor.Model.IsPaneVisible("Validation"));
        AppContext.SetData("Gondwana.Tooling.ApplicationId", "standalone-gscn");
        using (var editor = Editor("gscn")) Assert.False(editor.Model.IsPaneVisible("Validation"));
    });

    [Theory]
    [InlineData("malformed", "gscn")]
    [InlineData("malformed", "gspr")]
    [InlineData("unknown", "gscn")]
    [InlineData("unknown", "gspr")]
    [InlineData("partial-load-failure", "gscn")]
    [InlineData("partial-load-failure", "gspr")]
    public void BadAndStalePreferencesDoNotPreventEditorStartup(string fault, string kind) => Sta(() =>
    {
        using (var editor = Editor(kind))
            Panes(editor.Model).Single(p => p.Text == "Validation").Hide();
        if (fault == "malformed") File.WriteAllText(Layout(kind), "<broken");
        else
        {
            var xml = XDocument.Load(Layout(kind));
            if (fault == "unknown")
                xml.Root!.Element("Contents")!.Elements().Single(e => (string?)e.Attribute("PersistString") == (kind == "gspr" ? "gspr.scene-sources" : "gscn.gani-animations"))
                    .SetAttributeValue("PersistString", "plugin.removed");
            else xml.Root!.Element("Panes")!.Elements().First().SetAttributeValue("ActiveContent", "9999");
            xml.Save(Layout(kind));
        }
        using var restored = Editor(kind);
        Assert.Equal(kind == "gspr" ? 6 : 7, Panes(restored.Model).Length);
        Assert.True(restored.Model.IsPaneVisible(kind == "gspr" ? "GSCN scene/layer sources" : "GANI animations"));
        Assert.Equal(fault != "unknown", restored.Model.IsPaneVisible("Validation"));
        restored.Model.ShowAllPanes();
        Assert.All(Panes(restored.Model), pane => Assert.False(pane.IsHidden));
    });

    [Fact]
    public void StudioToolsRestoreWithoutDocumentsAndViewAndResetRemainAvailable() => Sta(() =>
    {
        using (var studio = Studio())
        {
            var working = Tool(studio, "Working directory");
            var output = Tool(studio, "Output");
            working.Show(studio.Workspace, DockState.DockRight);
            studio.Workspace.DockRightPortion = .35;
            output.Show(studio.Workspace, DockState.DockLeft);
            working.Hide();
            studio.NewDocument("gts");
        }
        var persisted = File.ReadAllText(Layout("shell"));
        Assert.DoesNotContain(Root, persisted);
        Assert.Contains("ignored-document", persisted);
        using (var studio = Studio())
        {
            Assert.Empty(studio.Documents);
            Assert.Equal(2, studio.Workspace.Contents.Count);
            var working = Tool(studio, "Working directory");
            Assert.True(working.IsHidden);
            Assert.Equal(DockState.DockRight, working.Pane.DockState);
            Assert.Equal(DockState.DockLeft, Tool(studio, "Output").DockState);
            Assert.InRange(studio.Workspace.DockRightPortion, .34, .36);
            studio.RebuildViewMenu();
            var view = (ToolStripMenuItem)studio.MainMenuStrip!.Items[1];
            var item = view.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "Working directory");
            Assert.False(item.Checked);
            item.PerformClick();
            Assert.False(working.IsHidden);
            var document = studio.NewDocument("gscn");
            view.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "Reset application layout").PerformClick();
            Assert.False(document.IsDisposed);
            Assert.True(document.Document.Dirty());
            Assert.Equal(DockState.DockLeft, working.DockState);
            Assert.Equal(DockState.DockBottom, Tool(studio, "Output").DockState);
        }
        using (var studio = Studio())
        {
            Assert.Empty(studio.Documents);
            Assert.False(Tool(studio, "Working directory").IsHidden);
            Assert.Equal(DockState.DockBottom, Tool(studio, "Output").DockState);
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StudioIgnoresMissingPluginsAndCorruptPreferences(bool corrupt) => Sta(() =>
    {
        using (var studio = Studio()) Tool(studio, "Output").Hide();
        if (corrupt) File.WriteAllText(Layout("shell"), "not XML");
        else File.WriteAllText(Layout("shell"), File.ReadAllText(Layout("shell")).Replace("shell.working-directory", "plugin.uninstalled"));
        using var restored = Studio();
        Assert.Equal(2, restored.Workspace.Contents.Count);
        Assert.False(Tool(restored, "Working directory").IsHidden);
        Assert.Equal(!corrupt, Tool(restored, "Output").IsHidden);
    });

    private sealed class EditorHost : IDisposable
    {
        internal StudioDocument Model { get; }
        private readonly Form _host = new() { Size = new Size(1400, 900), Opacity = 0, ShowInTaskbar = false };
        internal EditorHost(string kind)
        {
            Model = StudioDocument.Create(kind, Root, kind == "gaf" ? Path.Combine(Root, Guid.NewGuid() + ".gaf") : null);
            _host.Controls.Add(Model.Editor);
            _host.Show();
            Application.DoEvents();
        }
        public void Dispose() { Model.Dispose(); _host.Dispose(); }
    }
    private static EditorHost Editor(string kind) => new(kind);
    private static DockContent[] Panes(StudioDocument editor) => Descendants(editor.Editor).OfType<DockPanel>().Single().Contents.Cast<DockContent>().ToArray();
    private static DockContent Tool(MainForm studio, string name) => studio.Workspace.Contents.OfType<DockContent>().Single(p => p.Text == name);
    private static MainForm Studio()
    {
        var studio = new MainForm(loadPlugins: false) { Opacity = 0, ShowInTaskbar = false };
        studio.SetWorkingDirectory(Root);
        studio.Show();
        Application.DoEvents();
        return studio;
    }
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void PumpUntil(Func<bool> done)
    {
        var elapsed = Stopwatch.StartNew();
        while (!done() && elapsed.Elapsed < TimeSpan.FromSeconds(5)) { Application.DoEvents(); Thread.Sleep(20); }
        Assert.True(done(), "Debounced layout save did not complete.");
    }
    private static void Sta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, threadScope: true);
                action();
            }
            catch (Exception ex) { error = ex; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Layout interaction timed out.");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
