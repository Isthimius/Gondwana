using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Tilesheets.Editing;

namespace Gondwana.Tooling.Tilesheets.WinForms;

internal sealed class ImageViewport : ScrollableControl, IMessageFilter
{
    private const float ZoomStepFactor = 1.25f;
    private int _zoomWheelDelta;
    public Bitmap? Image { get; set; }
    public TilesheetDefinition? Definition { get; set; }
    public TilesheetRegionDefinition? SelectedRegion { get; set; }
    public Point? SelectedFrame { get; set; }
    public HashSet<string> Overlays { get; } = Enum.GetNames<OverlayKind>().ToHashSet();
    public OverlaySettings Colors { get; set; } = OverlaySettings.Default;
    public event Action<TilesheetRegionDefinition, Point>? FrameSelected;
    public float Zoom { get; private set; } = 1;
    public string Message { get; set; } = "Choose a loose image to preview.";

    public ImageViewport()
    {
        DoubleBuffered = true;
        AutoScroll = true;
        Dock = DockStyle.Fill;
        BackColor = DarkTheme.Background;
        ResizeRedraw = true;
    }

    public void SetZoom(float zoom)
    {
        var center = new PointF((ClientSize.Width / 2f - AutoScrollPosition.X) / Zoom,
            (ClientSize.Height / 2f - AutoScrollPosition.Y) / Zoom);

        Zoom = Math.Clamp(zoom, .05f, 16f);
        UpdateExtent();
        
        AutoScrollPosition = new Point(Math.Max(0, (int)(center.X * Zoom - ClientSize.Width / 2f)),
            Math.Max(0, (int)(center.Y * Zoom - ClientSize.Height / 2f)));
        
        Invalidate();
    }

    internal void ZoomIn() => SetZoom(Zoom * ZoomStepFactor);

    internal void ZoomOut() => SetZoom(Zoom / ZoomStepFactor);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Application.AddMessageFilter(this);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Application.RemoveMessageFilter(this);
        base.OnHandleDestroyed(e);
    }

    bool IMessageFilter.PreFilterMessage(ref Message message)
    {
        const int MouseWheel = 0x020A;
        const int ControlKey = 0x0008;

        if (message.Msg != MouseWheel)
            return false;

        long buttonsAndDelta = message.WParam.ToInt64();
        if ((buttonsAndDelta & ControlKey) == 0)
        {
            _zoomWheelDelta = 0;
            return false;
        }
        
        long position = message.LParam.ToInt64();
        var screenPoint = new Point(unchecked((short)position), unchecked((short)(position >> 16)));
        
        // Wheel messages can target the focused property editor. Route Ctrl+wheel
        // by the window under the pointer without taking focus or committing edits.
        if (!Visible || !IsHandleCreated || WindowFromPoint(screenPoint) != Handle)
        {
            _zoomWheelDelta = 0;
            return false;
        }

        return ZoomWithMouseWheel(PointToClient(screenPoint), unchecked((short)(buttonsAndDelta >> 16)), true);
    }

    internal bool ZoomWithMouseWheel(Point location, int delta, bool controlPressed)
    {
        if (!controlPressed || Image is null || !ClientRectangle.Contains(location))
        {
            _zoomWheelDelta = 0;
            return false;
        }

        _zoomWheelDelta += delta;
        const int WheelNotch = 120;
        while (_zoomWheelDelta >= WheelNotch)
        {
            ZoomIn();
            _zoomWheelDelta -= WheelNotch;
        }
        while (_zoomWheelDelta <= -WheelNotch)
        {
            ZoomOut();
            _zoomWheelDelta += WheelNotch;
        }
        
        return true;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);

    public void Fit()
    {
        if (Image is null)
            return;
        
        SetZoom(Math.Min((ClientSize.Width - 24f) / Image.Width, (ClientSize.Height - 24f) / Image.Height));
        AutoScrollPosition = Point.Empty;
    }

    public void UpdateExtent()
    {
        AutoScrollMinSize = Image is null ? Size.Empty : new Size((int)Math.Ceiling(Image.Width * Zoom), (int)Math.Ceiling(Image.Height * Zoom));
        Invalidate();
    }

    public void RevealSelectedFrame()
    {
        if (SelectedRegion is not { } region || SelectedFrame is not { } frame)
            return;

        Rectangle bounds;
        
        try
        {
            bounds = FrameGeometry.Bounds(region, frame.X, frame.Y);
        }
        catch (OverflowException)
        {
            return;
        }

        float left = bounds.X * Zoom, top = bounds.Y * Zoom;
        float currentX = -AutoScrollPosition.X, currentY = -AutoScrollPosition.Y;
        
        if (left < currentX || left + bounds.Width * Zoom > currentX + ClientSize.Width)
            currentX = left;
        
        if (top < currentY || top + bounds.Height * Zoom > currentY + ClientSize.Height)
            currentY = top;
        
        AutoScrollPosition = new Point((int)Math.Clamp(currentX, 0, int.MaxValue), (int)Math.Clamp(currentY, 0, int.MaxValue));
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (Definition is null || e.Button != MouseButtons.Left)
            return;

        var point = new PointF((e.X - AutoScrollPosition.X) / Zoom, (e.Y - AutoScrollPosition.Y) / Zoom);
        if (FrameGeometry.HitTest(Definition, SelectedRegion, point) is { } hit)
            FrameSelected?.Invoke(hit.Region, hit.Frame);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        
        if (Image is null)
        {
            TextRenderer.DrawText(g, Message, Font, ClientRectangle, Color.Silver,
                TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        
        using (var checker = new HatchBrush(HatchStyle.LargeCheckerBoard, Color.FromArgb(52, 52, 52), Color.FromArgb(40, 40, 40)))
            g.FillRectangle(checker, ClientRectangle);
        
        g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
        g.ScaleTransform(Zoom, Zoom);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SmoothingMode = SmoothingMode.None;
        g.DrawImage(Image, new Rectangle(0, 0, Image.Width, Image.Height));
        
        if (Definition is null)
            return;
        
        if (Overlays.Contains("Regions") || Overlays.Contains("SelectedRegion"))
            foreach (var region in Definition.Regions)
                if (Overlays.Contains(region == SelectedRegion ? "SelectedRegion" : "Regions"))
                    Outline(g, region.Area, Colors[region == SelectedRegion ? OverlayKind.SelectedRegion : OverlayKind.Regions], region == SelectedRegion ? 2 : 1);
        
        if (SelectedRegion is not { } r)
            return;

        if (Overlays.Contains("Margin"))
            Outline(g, RectangleF.FromLTRB((float)r.Area.X + r.RegionMargin.Left, (float)r.Area.Y + r.RegionMargin.Top,
                (float)r.Area.X + r.Area.Width - r.RegionMargin.Right, (float)r.Area.Y + r.Area.Height - r.RegionMargin.Bottom), Colors[OverlayKind.Margin], 1, DashStyle.Dash);

        var (columns, rows) = TilesheetDefinitionValidator.GridSize(r);
        long pitchX = (long)r.TileSize.Width + r.TilePadding.Left + r.TilePadding.Right;
        long pitchY = (long)r.TileSize.Height + r.TilePadding.Top + r.TilePadding.Bottom;
        
        if (columns <= 0 || rows <= 0 || pitchX <= 0 || pitchY <= 0)
            return;

        // Only visit visible cells. Very dense grids are sampled visually; every coordinate
        // remains selectable with the frame navigator and the selected frame is always drawn.
        var left = -AutoScrollPosition.X / Zoom;
        var top = -AutoScrollPosition.Y / Zoom;
        int firstX = (int)Math.Clamp(Math.Floor((left - r.Area.X - r.RegionMargin.Left) / pitchX) - 1, 0, columns - 1);
        int firstY = (int)Math.Clamp(Math.Floor((top - r.Area.Y - r.RegionMargin.Top) / pitchY) - 1, 0, rows - 1);
        int lastX = (int)Math.Clamp(Math.Ceiling((left + ClientSize.Width / Zoom - r.Area.X - r.RegionMargin.Left) / pitchX) + 1, 0, columns - 1);
        int lastY = (int)Math.Clamp(Math.Ceiling((top + ClientSize.Height / Zoom - r.Area.Y - r.RegionMargin.Top) / pitchY) + 1, 0, rows - 1);
        int step = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((long)(lastX - firstX + 1) * (lastY - firstY + 1) / 10000d)));
        var metadata = r.Frames.Where(f => f is not null).GroupBy(f => (f.XTile, f.YTile)).ToDictionary(f => f.Key, f => f.First());

        for (long y = firstY; y <= lastY; y += step)
            for (long x = firstX; x <= lastX; x += step)
                DrawFrame((int)x, (int)y, false);
        
        if (SelectedFrame is { } selected && selected.X < columns && selected.Y < rows)
            DrawFrame(selected.X, selected.Y, true);

        void DrawFrame(int x, int y, bool selected)
        {
            Rectangle bounds;
            try
            {
                bounds = FrameGeometry.Bounds(r, x, y);
            }
            catch (OverflowException)
            {
                return;
            }

            if (Overlays.Contains("Padding"))
                Outline(g, RectangleF.FromLTRB((float)bounds.X - r.TilePadding.Left, (float)bounds.Y - r.TilePadding.Top,
                    (float)bounds.X + bounds.Width + r.TilePadding.Right, (float)bounds.Y + bounds.Height + r.TilePadding.Bottom), Colors[OverlayKind.Padding], 1, DashStyle.Dot);
            
            if (Overlays.Contains("Overhang"))
                Outline(g, RectangleF.FromLTRB((float)bounds.X - r.Overhang.Left, (float)bounds.Y - r.Overhang.Top,
                    (float)bounds.X + bounds.Width + r.Overhang.Right, (float)bounds.Y + bounds.Height + r.Overhang.Bottom), Colors[OverlayKind.Overhang], 1, DashStyle.Dash);
            
            if (Overlays.Contains("Frames")) Outline(g, bounds, Colors[OverlayKind.Frames]);
            
            if (Overlays.Contains("Collision"))
            {
                metadata.TryGetValue((x, y), out var frame);
                Outline(g, (frame?.CollisionAdjust ?? r.CollisionAdjust).ApplyTo(bounds), Colors[OverlayKind.Collision], 1, DashStyle.DashDot);
            }
            
            if (selected && Overlays.Contains("SelectedFrame")) Outline(g, bounds, Colors[OverlayKind.SelectedFrame], 3);
        }
    }

    private void Outline(Graphics graphics, RectangleF rectangle, Color color, float width = 1, DashStyle style = DashStyle.Solid)
    {
        if (rectangle.Width < 0 || rectangle.Height < 0)
            return;

        using var pen = new Pen(color, width / Zoom) { DashStyle = style };
        graphics.DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }
}
