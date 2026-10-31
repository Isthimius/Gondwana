using System.Drawing.Drawing2D;

namespace Gondwana.Tooling.WinForms;

/// <summary>Converts 96-DPI logical toolbar measurements directly to device pixels.</summary>
public static class ToolbarSizing
{
    public static int Pixels(int logical, int dpi)
    {
        if (logical < 0) throw new ArgumentOutOfRangeException(nameof(logical));
        if (dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi));
        return checked((int)Math.Round(logical * (dpi / 96d)));
    }
}

/// <summary>Draws vector glyph geometry directly into the target-resolution bitmap.</summary>
public static partial class ToolbarGlyphs
{
    public static void Prepare(Graphics graphics, int pixels, int designSize)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.ScaleTransform(pixels / (float)designSize, pixels / (float)designSize);
    }
}

/// <summary>Owns logical image-button metrics and generated images; native text items remain autosized.</summary>
public class DpiToolStrip : ToolStrip
{
    private sealed class Glyph(Func<int, Bitmap> create)
    {
        internal readonly Func<int, Bitmap> Create = create;
        internal Bitmap? Image;
    }

    private readonly Dictionary<ToolStripItem, Glyph> _glyphs = [];
    private readonly Dictionary<ToolStripItem, int> _widths = [];
    private readonly int _buttonSize;
    private readonly int _imageSize;
    private int _imageDpi;
    private Color _imageColor;
    private bool _applying;
    private bool _disposing;

    public DpiToolStrip(int buttonSize, int imageSize)
    {
        if (buttonSize <= 0) throw new ArgumentOutOfRangeException(nameof(buttonSize));
        if (imageSize <= 0) throw new ArgumentOutOfRangeException(nameof(imageSize));
        _buttonSize = buttonSize;
        _imageSize = imageSize;
        ApplyMetrics();
    }

    /// <summary>The effective control DPI, rather than a physical monitor's DPI in SystemAware mode.</summary>
    protected virtual int EffectiveDpi => DeviceDpi;

    /// <summary>Registers a glyph factory. The strip owns every bitmap returned by it.</summary>
    public void SetGlyph(ToolStripItem item, Func<int, Bitmap> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        if (_glyphs.ContainsKey(item)) throw new ArgumentException("The item already has a glyph.", nameof(item));
        _glyphs.Add(item, new Glyph(create));
        ApplyMetrics();
    }

    public void SetLogicalWidth(ToolStripItem item, int width)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        _widths[item] = width;
        ApplyMetrics();
    }

    // WinForms can scale the strip and hosted controls itself. Always replace owned
    // measurements from logical constants after that work; never multiply current bounds.
    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        base.ScaleControl(factor, specified);
        ApplyMetrics();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyMetrics();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyMetrics();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        ApplyMetrics();
        base.OnLayout(e);
    }

    private void ApplyMetrics()
    {
        // ToolStrip's constructor calls virtual layout methods before these fields exist.
        if (_glyphs is null || _buttonSize == 0 || _applying || _disposing || IsDisposed) return;
        _applying = true;
        SuspendLayout();
        try
        {
            int dpi = EffectiveDpi;
            int button = ToolbarSizing.Pixels(_buttonSize, dpi);
            int image = ToolbarSizing.Pixels(_imageSize, dpi);
            ImageScalingSize = new Size(image, image);
            Padding = new Padding(0, 0, ToolbarSizing.Pixels(1, dpi), 0);
            bool rebuild = _imageDpi != dpi || _imageColor != ForeColor;
            foreach (var (item, glyph) in _glyphs)
            {
                if (item.IsDisposed) continue;
                item.AutoSize = false;
                item.Size = new Size(button, button);
                item.Margin = new Padding(0, ToolbarSizing.Pixels(1, dpi), 0, ToolbarSizing.Pixels(2, dpi));
                if (rebuild || glyph.Image is null)
                {
                    var next = glyph.Create(image);
                    var previous = glyph.Image;
                    item.Image = next;
                    glyph.Image = next;
                    previous?.Dispose();
                }
            }
            foreach (var (item, width) in _widths)
            {
                if (item.IsDisposed) continue;
                item.AutoSize = false;
                int height = item is ToolStripControlHost host
                    ? host.Control.GetPreferredSize(Size.Empty).Height : item.Height;
                item.Size = new Size(ToolbarSizing.Pixels(width, dpi), height);
            }
            _imageDpi = dpi;
            _imageColor = ForeColor;
        }
        finally
        {
            ResumeLayout(false);
            _applying = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposing)
        {
            _disposing = true;
            foreach (var (item, glyph) in _glyphs)
            {
                if (!item.IsDisposed) item.Image = null;
                glyph.Image?.Dispose();
            }
            _glyphs.Clear();
            _widths.Clear();
        }
        base.Dispose(disposing);
    }
}
