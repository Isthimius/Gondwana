using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gondwana.Demos.Spot;

internal sealed class SpotSplashForm : Form
{
    private const int FadeInDurationMs = 450;
    private const int FadeOutDurationMs = 450;
    private const int HoldBeforeInitMs = 300;
    private const int HoldAfterInitMs = 250;

    private readonly PictureBox _logoPictureBox;
    private readonly Label _titleLabel;
    private readonly Form _owner;

    internal SpotSplashForm(Form owner)
    {
        _owner = owner;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        Opacity = 0;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(40)
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));

        _logoPictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0)
        };

        _titleLabel = new Label
        {
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.TopCenter,
            Text = "Gondwana",
            ForeColor = Color.Black,
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 28, FontStyle.Bold)
        };

        layout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.White }, 0, 0);
        layout.Controls.Add(_logoPictureBox, 0, 1);
        layout.Controls.Add(_titleLabel, 0, 2);
        Controls.Add(layout);

        LoadLogo();
    }

    internal async Task ShowDuringInitializationAsync(Action initializationAction)
    {
        SyncToOwnerBounds();
        Show(_owner);
        BringToFront();
        await AnimateOpacityAsync(from: 0, to: 1, FadeInDurationMs);
        await Task.Delay(HoldBeforeInitMs);

        initializationAction();

        await Task.Delay(HoldAfterInitMs);
        await AnimateOpacityAsync(from: 1, to: 0, FadeOutDurationMs);
        Close();
    }

    private void SyncToOwnerBounds()
    {
        Bounds = _owner.Bounds;
    }

    private void LoadLogo()
    {
        var logoPath = Path.Combine(AppContext.BaseDirectory, "assets", "gondwana-logo.png");
        if (!File.Exists(logoPath))
            return;

        using var stream = File.OpenRead(logoPath);
        using var image = Image.FromStream(stream);
        _logoPictureBox.Image = new Bitmap(image);
    }

    private Task AnimateOpacityAsync(double from, double to, int durationMs)
    {
        Opacity = from;
        if (durationMs <= 0 || Math.Abs(to - from) < double.Epsilon)
        {
            Opacity = to;
            return Task.CompletedTask;
        }

        const int intervalMs = 16;
        var steps = Math.Max(1, durationMs / intervalMs);
        var step = (to - from) / steps;
        var currentStep = 0;
        var tcs = new TaskCompletionSource();
        var timer = new System.Windows.Forms.Timer { Interval = intervalMs };

        timer.Tick += (_, _) =>
        {
            currentStep++;
            if (currentStep >= steps)
            {
                timer.Stop();
                timer.Dispose();
                Opacity = to;
                tcs.TrySetResult();
                return;
            }

            Opacity = Math.Clamp(Opacity + step, 0, 1);
        };

        timer.Start();
        return tcs.Task;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _logoPictureBox.Image?.Dispose();
            _titleLabel.Font?.Dispose();
        }

        base.Dispose(disposing);
    }
}
