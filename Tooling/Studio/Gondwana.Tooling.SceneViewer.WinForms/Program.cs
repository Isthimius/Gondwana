using System.Diagnostics;

namespace Gondwana.Tooling.SceneViewer.WinForms;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        SceneViewerSceneOptions? options = null;
        try
        {
            var stress = SceneViewerArguments.ParseStress(args);
            SceneViewerForm form;

            if (stress is not null)
            {
                form = new SceneViewerForm(stress);
            }
            else
            {
                options = SceneViewerArguments.ParseScene(args, Environment.CurrentDirectory);
                var path = options?.ScenePath;
                if (path is null)
                {
                    using var dialog = new OpenFileDialog
                    {
                        Filter = "Gondwana scenes (*.gscn)|*.gscn",
                        CheckFileExists = true
                    };
                    if (dialog.ShowDialog() != DialogResult.OK)
                        return 0;
                    path = SceneViewerArguments.Normalize(
                        dialog.FileName,
                        Environment.CurrentDirectory);
                }

                form = new SceneViewerForm(path, options?.SceneId, options?.ContentRoot);
            }

            using (form)
                Application.Run(form);
            return Environment.ExitCode;
        }
        catch (Exception ex)
        {
            ReportError(ex);
            return 1;
        }
        finally
        {
            // Also covers a render-control failure before the host was constructed.
            Engine.Instance.Dispose();
            if (options?.TemporaryScene == true)
            {
                // The viewer owns cleanup even if the Studio process exits first.
                try { File.Delete(options.ScenePath); }
                catch (IOException) { /* Best effort; abnormal exits may leave snapshots. */ }
                catch (UnauthorizedAccessException) { /* Best effort; abnormal exits may leave snapshots. */ }
            }
        }
    }

    internal static void ReportError(Exception exception)
    {
        Environment.ExitCode = 1;
        Trace.TraceError(exception.ToString());
        MessageBox.Show($"Unable to view scene.\n\n{exception.Message}", "Gondwana Scene Viewer",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
