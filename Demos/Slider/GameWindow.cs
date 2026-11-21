using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.Slider;

/// <summary>WinForms shell: settings and file selection remain outside the engine.</summary>
internal sealed class GameWindow : Form
{
    private readonly WinFormGpuRenderSurfaceControl _surface = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _columns = new() { Minimum = 3, Maximum = 20, Value = 4, Width = 160 };
    private readonly NumericUpDown _rows = new() { Minimum = 3, Maximum = 20, Value = 3, Width = 160 };
    private readonly CheckBox _grid = new() { Text = "Show piece outlines", Checked = true, AutoSize = true };
    private readonly Label _status = new()
    {
        Text = "Starting...",
        AutoSize = false,
        Width = 210,
        Height = 135,
        Font = new Font(SystemFonts.DefaultFont.FontFamily, 11f)
    };

    private SliderGameHost? _host;
    private string? _picturePath;

    internal GameWindow()
    {
        Text = "Gondwana: Slider";
        ClientSize = new Size(1200, 780);
        MinimumSize = new Size(760, 510);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;

        var sidebar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(238, 241, 245)
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.Controls.Add(sidebar, 0, 0);
        layout.Controls.Add(_surface, 1, 0);
        Controls.Add(layout);

        sidebar.Controls.Add(new Label
        {
            Text = "SLIDER",
            Width = 205,
            Height = 42,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 20f, FontStyle.Bold)
        });
        sidebar.Controls.Add(new Label { Text = "Columns", AutoSize = true, Margin = new Padding(4, 12, 4, 2) });
        sidebar.Controls.Add(_columns);
        sidebar.Controls.Add(new Label { Text = "Rows", AutoSize = true, Margin = new Padding(4, 12, 4, 2) });
        sidebar.Controls.Add(_rows);

        var newPuzzle = MakeButton("New puzzle");
        newPuzzle.Click += (_, _) =>
        {
            _host?.RequestPuzzle(_picturePath, (int)_columns.Value, (int)_rows.Value);
            _surface.Focus();
        };
        sidebar.Controls.Add(newPuzzle);

        var openPicture = MakeButton("Open picture...");
        openPicture.Click += (_, _) => OpenPicture();
        sidebar.Controls.Add(openPicture);

        var shuffle = MakeButton("Shuffle");
        shuffle.Click += (_, _) =>
        {
            _host?.RequestShuffle();
            _surface.Focus();
        };
        sidebar.Controls.Add(shuffle);

        _grid.CheckedChanged += (_, _) =>
        {
            _host?.RequestGrid(_grid.Checked);
            _surface.Focus();
        };
        _grid.Margin = new Padding(4, 14, 4, 8);
        sidebar.Controls.Add(_grid);

        sidebar.Controls.Add(new Label
        {
            Text = "Arrow keys or click a tile beside the empty square.",
            Width = 205,
            Height = 54,
            Margin = new Padding(4, 12, 4, 8)
        });
        sidebar.Controls.Add(_status);

        KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Escape)
                Close();
        };
    }

    private static Button MakeButton(string text) => new()
    {
        Text = text,
        Width = 196,
        Height = 36,
        Margin = new Padding(4, 10, 4, 0)
    };

    private void OpenPicture()
    {
        using var chooser = new OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*",
            CheckFileExists = true,
            Multiselect = false,
            Title = "Choose a sliding-puzzle picture"
        };

        if (chooser.ShowDialog(this) != DialogResult.OK)
            return;

        _picturePath = chooser.FileName;
        _host?.RequestPuzzle(_picturePath, (int)_columns.Value, (int)_rows.Value);
        _surface.Focus();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _host = new SliderGameHost(_surface);
        _host.StatusChanged += OnStatusChanged;
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

    private void OnStatusChanged(string status)
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke((Action)(() =>
            {
                if (!IsDisposed && !Disposing)
                    _status.Text = status;
            }));
        }
        catch (InvalidOperationException) when (IsDisposed || Disposing || !IsHandleCreated)
        {
            // Shutdown can invalidate the WinForms handle after the initial check.
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_host is not null)
        {
            _host.StatusChanged -= OnStatusChanged;
            _host.Dispose();
            _host = null;
        }

        base.OnFormClosed(e);
    }
}
