using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.Slider;

/// <summary>
/// Native window and file-picker boundary. All persistent UI is drawn by
/// Gondwana.Widgets inside the single GPU render surface.
/// </summary>
internal sealed class GameWindow : Form
{
    private readonly WinFormGpuRenderSurfaceControl _surface = new() { Dock = DockStyle.Fill };
    private SliderGameHost? _host;

    internal GameWindow()
    {
        Text = "Gondwana: Slider";
        ClientSize = new Size(1200, 780);
        MinimumSize = new Size(760, 650);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;

        // This is the only child control on the WinForms form.
        Controls.Add(_surface);
        KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Escape && _host?.HasMenuOrDialogOpen != true)
                Close();
        };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _host = new SliderGameHost(_surface);
        _host.OpenPictureRequested += OnOpenPictureRequested;
        _host.SaveGameRequested += OnSaveGameRequested;
        _host.LoadGameRequested += OnLoadGameRequested;
        _host.ExitRequested += OnExitRequested;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            _host!.Initialize(logLevel: LogLevel.Warning);
            _surface.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Slider could not start." + Environment.NewLine + ex.Message,
                "Startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    private void OnOpenPictureRequested(int columns, int rows)
    {
        // Widget callbacks run on Gondwana's engine thread; native dialogs
        // must always be opened on the WinForms UI thread.
        if (Disposing || IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke((Action)(() =>
            {
                if (Disposing || IsDisposed || _host is null)
                    return;

                using var picker = new OpenFileDialog
                {
                    Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*",
                    CheckFileExists = true,
                    Multiselect = false,
                    Title = "Choose a sliding-puzzle picture"
                };

                if (picker.ShowDialog(this) == DialogResult.OK)
                    _host.RequestPuzzle(picker.FileName, columns, rows);

                _surface.Focus();
            }));
        }
        catch (InvalidOperationException) when (Disposing || IsDisposed || !IsHandleCreated)
        {
            // Shutdown may invalidate the window between the check and BeginInvoke.
        }
    }


    private void RunOnUiThread(Action callback)
    {
        if (Disposing || IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke((Action)(() =>
            {
                if (!Disposing && !IsDisposed)
                    callback();
            }));
        }
        catch (InvalidOperationException) when (Disposing || IsDisposed || !IsHandleCreated)
        {
            // The window may close while an engine-thread callback is queued.
        }
    }

    private void OnSaveGameRequested(SliderSave snapshot)
    {
        RunOnUiThread(() =>
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "Slider saved puzzle (*.slider.json)|*.slider.json|JSON files (*.json)|*.json",
                DefaultExt = "slider.json",
                AddExtension = true,
                FileName = "My Puzzle.slider.json",
                Title = "Save Slider puzzle"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                SliderPersistence.Save(dialog.FileName, snapshot);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                MessageBox.Show(this, ex.Message, "Could not save puzzle",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            _surface.Focus();
        });
    }

    private void OnLoadGameRequested()
    {
        RunOnUiThread(() =>
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Slider saved puzzle (*.slider.json)|*.slider.json|JSON files (*.json)|*.json",
                CheckFileExists = true,
                Title = "Load Slider puzzle"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                _host?.RequestRestore(SliderPersistence.Load(dialog.FileName));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                MessageBox.Show(this, ex.Message, "Could not load puzzle",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            _surface.Focus();
        });
    }

    private void OnExitRequested() => RunOnUiThread(Close);

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_host is not null)
        {
            _host.OpenPictureRequested -= OnOpenPictureRequested;
            _host.SaveGameRequested -= OnSaveGameRequested;
            _host.LoadGameRequested -= OnLoadGameRequested;
            _host.ExitRequested -= OnExitRequested;
            _host.Dispose();
            _host = null;
        }

        base.OnFormClosed(e);
    }
}
