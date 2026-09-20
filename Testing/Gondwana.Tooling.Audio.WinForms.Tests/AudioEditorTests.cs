using System.Runtime.ExceptionServices;
using Gondwana.Audio.GSND;
using Gondwana.Tooling.Audio.Editing;
using Gondwana.Tooling.Audio.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Audio.WinForms.Tests;

public sealed class AudioEditorTests
{
    [Fact]
    public void DocumentSaveAsRebasesLooseAudioReferences()
    {
        var root = Path.Combine(Path.GetTempPath(), "GondwanaGsndEditor-" + Guid.NewGuid());
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");
        var media = Path.Combine(root, "media");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        Directory.CreateDirectory(media);

        try
        {
            var audioPath = Path.Combine(media, "tone.wav");
            File.WriteAllBytes(audioPath, [1, 2, 3]);

            var document = AudioDocument.Create(first);
            document.AddLooseFile(audioPath);
            var firstPath = Path.Combine(first, "audio.gsnd");
            document.Save(firstPath);

            var firstReference = Assert.Single(document.Definition.Resources).FilePath!;
            Assert.False(Path.IsPathRooted(firstReference));
            Assert.Equal(
                Path.GetFullPath(audioPath),
                Path.GetFullPath(firstReference, first));

            var secondPath = Path.Combine(second, "copy.gsnd");
            document.Save(secondPath);

            var secondReference = Assert.Single(document.Definition.Resources).FilePath!;
            Assert.False(Path.IsPathRooted(secondReference));
            Assert.Equal(
                Path.GetFullPath(audioPath),
                Path.GetFullPath(secondReference, second));

            Assert.False(document.IsDirty);
            Assert.Equal(Path.GetFullPath(secondPath), document.FilePath);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EditorUsesLocalDockingAndAddsLooseFiles() => RunSta(() =>
    {
        var root = Path.Combine(Path.GetTempPath(), "GondwanaGsndEditor-" + Guid.NewGuid());
        Directory.CreateDirectory(root);

        try
        {
            var audioPath = Path.Combine(root, "tone.wav");
            File.WriteAllBytes(audioPath, [1, 2, 3]);

            var document = AudioDocument.Create(root);
            using var editor = new AudioEditorControl(document);
            using var host = new Form
            {
                Size = new Size(1100, 760),
                Opacity = 0,
                ShowInTaskbar = false
            };
            host.Controls.Add(editor);
            host.Show();
            Application.DoEvents();

            var dock = Assert.Single(Descendants(editor).OfType<DockPanel>());
            var panes = dock.Contents.Cast<DockContent>().ToArray();
            Assert.Equal(
                ["Audio resources", "Properties", "Validation"],
                panes.Select(pane => pane.Text).Order().ToArray());
            Assert.All(panes, pane =>
            {
                Assert.Same(dock, pane.DockPanel);
                Assert.True(pane.HideOnClose);
                Assert.False(pane.DockHandler.IsDockStateValid(DockState.Float));
            });

            editor.AddLooseFiles([audioPath]);
            var resource = Assert.Single(document.Definition.Resources);
            Assert.Equal("tone", resource.Key);
            Assert.Equal(AudioResourceSourceKind.LooseFile, resource.SourceKind);
            Assert.True(document.IsDirty);

            var resourcesPane = panes.Single(pane => pane.Text == "Audio resources");
            var propertiesPane = panes.Single(pane => pane.Text == "Properties");
            propertiesPane.Show(resourcesPane.Pane, DockAlignment.Bottom, .4);
            Application.DoEvents();
            Assert.Same(dock, propertiesPane.DockPanel);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    });

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
            "GSND WinForms test timed out.");

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}
