using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Scenes.Editing;

namespace Gondwana.Tooling.Scenes.WinForms;

/// <summary>
/// Lightweight definition-driven GSCN preview. It uses SceneLayer's coordinate
/// conversion implementation but does not create or register a runtime Scene.
/// </summary>
internal sealed class ScenePreviewControl : UserControl, IMessageFilter
{
    private const float ZoomStepFactor = 1.25f;
    private const float PreviewMargin = 28f;
    private SceneDefinition? _definition;
    private Func<string, SceneTilesheetSource?>? _findTilesheet;
    private Func<string, SceneAnimationSource?>? _findAnimation;
    private SceneLayerDefinition? _selectedLayer;
    private int _selectedX;
    private int _selectedY;
    private int _zoomWheelDelta;
    private bool _fitToWindow;

    private RectangleF _worldBounds = RectangleF.Empty;
    private PointF _offset;
    private readonly Dictionary<SceneLayerDefinition, ProjectionCache> _projections = [];

    public float Zoom { get; private set; } = 1f;
    public bool ShowGridLines { get; set; } = true;

    public event Action<SceneLayerDefinition, int, int>? TileSelected;

    public ScenePreviewControl()
    {
        DoubleBuffered = true;
        AutoScroll = true;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(24, 24, 24);
        ForeColor = Color.Gainsboro;
        ResizeRedraw = true;
        Resize += (_, _) => UpdateExtent();
    }

    public void Configure(
        SceneDefinition definition,
        Func<string, SceneTilesheetSource?> findTilesheet,
        Func<string, SceneAnimationSource?> findAnimation)
    {
        _definition = definition;
        _findTilesheet = findTilesheet;
        _findAnimation = findAnimation;

        var currentLayers = definition.Layers.ToHashSet();
        foreach (var stale in _projections.Keys
                     .Where(layer => !currentLayers.Contains(layer))
                     .ToArray())
        {
            _projections[stale].Layer.Dispose();
            _projections.Remove(stale);
        }

        UpdateSceneBounds();
        UpdateExtent();
    }

    public void SetZoom(float zoom)
    {
        PointF? centerWorld = _worldBounds.IsEmpty || Zoom <= 0
            ? null
            : new PointF(
                (ClientSize.Width / 2f - _offset.X) / Zoom,
                (ClientSize.Height / 2f - _offset.Y) / Zoom);

        _fitToWindow = false;
        Zoom = Math.Clamp(zoom, .05f, 16f);
        UpdateExtent();

        if (centerWorld is { } center)
        {
            float contentX =
                PreviewMargin +
                (center.X - _worldBounds.Left) * Zoom;
            float contentY =
                PreviewMargin +
                (center.Y - _worldBounds.Top) * Zoom;

            AutoScrollPosition = new Point(
                Math.Max(
                    0,
                    (int)Math.Round(
                        contentX - ClientSize.Width / 2f)),
                Math.Max(
                    0,
                    (int)Math.Round(
                        contentY - ClientSize.Height / 2f)));

            UpdateOffset();
        }

        Invalidate();
    }

    internal void ZoomIn() =>
        SetZoom(Zoom * ZoomStepFactor);

    internal void ZoomOut() =>
        SetZoom(Zoom / ZoomStepFactor);

    public void Fit()
    {
        _fitToWindow = true;
        AutoScrollPosition = Point.Empty;
        UpdateExtent();
    }

    internal bool ZoomWithMouseWheel(
        Point location,
        int delta,
        bool controlPressed)
    {
        if (!controlPressed ||
            _worldBounds.IsEmpty ||
            !ClientRectangle.Contains(location))
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
        var screenPoint = new Point(
            unchecked((short)position),
            unchecked((short)(position >> 16)));

        if (!Visible ||
            !IsHandleCreated ||
            WindowFromPoint(screenPoint) != Handle)
        {
            _zoomWheelDelta = 0;
            return false;
        }

        return ZoomWithMouseWheel(
            PointToClient(screenPoint),
            unchecked((short)(buttonsAndDelta >> 16)),
            controlPressed: true);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        UpdateOffset();
        Invalidate();
    }

    public void SetSelection(
        SceneLayerDefinition? layer,
        int x,
        int y)
    {
        _selectedLayer = layer;
        _selectedX = x;
        _selectedY = y;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.Clear(BackColor);
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.SmoothingMode = SmoothingMode.None;

        if (_definition is null || _definition.Layers.Count == 0)
        {
            DrawCenteredMessage(e.Graphics, "Add a SceneLayer to begin editing.");
            return;
        }

        var visibleLayers = _definition.Layers
            .Where(layer => layer.Visible && layer.Columns > 0 && layer.Rows > 0)
            .OrderBy(layer => layer.ZOrder)
            .ToList();

        if (visibleLayers.Count == 0)
        {
            DrawCenteredMessage(e.Graphics, "No visible SceneLayers.");
            return;
        }

        if (_worldBounds.Width <= 0 || _worldBounds.Height <= 0)
        {
            DrawCenteredMessage(e.Graphics, "Scene bounds are empty.");
            return;
        }

        UpdateOffset();

        foreach (var layer in visibleLayers)
            DrawLayer(e.Graphics, layer);

        using var labelBrush = new SolidBrush(ForeColor);
        e.Graphics.DrawString(
            $"{visibleLayers.Count} visible layer(s) — click the selected layer to choose a tile",
            Font,
            labelBrush,
            new PointF(8, 8));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || _definition is null)
            return;

        var layer = _selectedLayer ??
            _definition.Layers
                .Where(candidate => candidate.Visible)
                .OrderByDescending(candidate => candidate.ZOrder)
                .FirstOrDefault();

        if (layer is null ||
            layer.Columns <= 0 ||
            layer.Rows <= 0 ||
            Zoom <= 0)
        {
            return;
        }

        float worldX = (e.X - _offset.X) / Zoom;
        float worldY = (e.Y - _offset.Y) / Zoom;

        var projection = GetProjection(layer);
        PointF grid = projection.WorldPxToGrid(new PointF(worldX, worldY));

        int x = (int)Math.Floor(grid.X + 0.0001f);
        int y = (int)Math.Floor(grid.Y + 0.0001f);

        if (x < 0 || y < 0 || x >= layer.Columns || y >= layer.Rows)
            return;

        _selectedLayer = layer;
        _selectedX = x;
        _selectedY = y;
        TileSelected?.Invoke(layer, x, y);
        Invalidate();
    }

    private RectangleF CalculateWorldBounds(
        IReadOnlyList<SceneLayerDefinition> layers)
    {
        RectangleF bounds = RectangleF.Empty;
        bool hasBounds = false;

        foreach (var layer in layers)
        {
            var projection = GetProjection(layer);
            var rect = projection.GetLayerBoundsPx();
            if (rect.IsEmpty)
                continue;

            bounds = hasBounds
                ? RectangleF.Union(bounds, rect)
                : rect;
            hasBounds = true;
        }

        if (!hasBounds)
            return RectangleF.Empty;

        bounds.Inflate(8, 8);
        return bounds;
    }

    private void UpdateSceneBounds()
    {
        if (_definition is null)
        {
            _worldBounds = RectangleF.Empty;
            return;
        }

        var visibleLayers = _definition.Layers
            .Where(layer =>
                layer.Visible &&
                layer.Columns > 0 &&
                layer.Rows > 0)
            .OrderBy(layer => layer.ZOrder)
            .ToList();

        _worldBounds = CalculateWorldBounds(visibleLayers);
    }

    private float CalculateFitZoom()
    {
        if (_worldBounds.IsEmpty)
            return 1f;

        float availableWidth =
            Math.Max(1, ClientSize.Width - PreviewMargin * 2);
        float availableHeight =
            Math.Max(1, ClientSize.Height - PreviewMargin * 2);

        return Math.Clamp(
            Math.Min(
                availableWidth / Math.Max(1, _worldBounds.Width),
                availableHeight / Math.Max(1, _worldBounds.Height)),
            .05f,
            16f);
    }

    private void UpdateExtent()
    {
        if (_worldBounds.IsEmpty)
        {
            AutoScrollMinSize = Size.Empty;
            AutoScrollPosition = Point.Empty;
            _offset = Point.Empty;
            Invalidate();
            return;
        }

        if (_fitToWindow)
        {
            Zoom = CalculateFitZoom();
            AutoScrollPosition = Point.Empty;
        }

        AutoScrollMinSize = new Size(
            Math.Max(
                1,
                (int)Math.Ceiling(
                    _worldBounds.Width * Zoom +
                    PreviewMargin * 2)),
            Math.Max(
                1,
                (int)Math.Ceiling(
                    _worldBounds.Height * Zoom +
                    PreviewMargin * 2)));

        UpdateOffset();
        Invalidate();
    }

    private void UpdateOffset()
    {
        if (_worldBounds.IsEmpty)
        {
            _offset = Point.Empty;
            return;
        }

        float renderedWidth = _worldBounds.Width * Zoom;
        float renderedHeight = _worldBounds.Height * Zoom;

        float baseX =
            renderedWidth + PreviewMargin * 2 <= ClientSize.Width
                ? (ClientSize.Width - renderedWidth) / 2f
                : PreviewMargin;

        float baseY =
            renderedHeight + PreviewMargin * 2 <= ClientSize.Height
                ? (ClientSize.Height - renderedHeight) / 2f
                : PreviewMargin;

        _offset = new PointF(
            baseX +
            AutoScrollPosition.X -
            _worldBounds.Left * Zoom,
            baseY +
            AutoScrollPosition.Y -
            _worldBounds.Top * Zoom);
    }

    private void DrawLayer(Graphics graphics, SceneLayerDefinition layer)
    {
        var projection = GetProjection(layer);

        using var gridPen = new Pen(
            ReferenceEquals(layer, _selectedLayer)
                ? Color.FromArgb(115, 160, 190)
                : Color.FromArgb(65, 65, 70),
            1f);

        foreach (var tile in layer.Tiles)
        {
            if (!tile.Visible)
                continue;

            if (tile.X < 0 || tile.Y < 0 || tile.X >= layer.Columns || tile.Y >= layer.Rows)
                continue;

            var frame = ResolvePreviewFrame(tile);
            if (frame is null)
                continue;

            var source = ResolveTilesheetSource(frame, tile.AnimationKey);
            if (source?.Image is null ||
                !source.TryResolve(frame, out _, out var sourceBounds))
            {
                DrawMissingFrame(graphics, projection, layer, tile.X, tile.Y);
                continue;
            }

            var world = TileBounds(projection, layer, tile.X, tile.Y);
            var screen = ToScreen(world);

            graphics.DrawImage(
                source.Image,
                Rectangle.Round(screen),
                sourceBounds,
                GraphicsUnit.Pixel);
        }

        if (ShowGridLines &&
            (layer.ShowGridLines || ReferenceEquals(layer, _selectedLayer)))
        {
            int maxCells = 20000;
            int count = 0;
            for (int y = 0; y < layer.Rows && count < maxCells; y++)
            for (int x = 0; x < layer.Columns && count < maxCells; x++, count++)
            {
                var points = TileOutline(projection, layer, x, y)
                    .Select(ToScreen)
                    .ToArray();

                if (points.Length >= 3)
                    graphics.DrawPolygon(gridPen, points);
                else
                    graphics.DrawRectangle(gridPen, Rectangle.Round(ToScreen(
                        TileBounds(projection, layer, x, y))));
            }
        }

        if (ReferenceEquals(layer, _selectedLayer) &&
            _selectedX >= 0 &&
            _selectedY >= 0 &&
            _selectedX < layer.Columns &&
            _selectedY < layer.Rows)
        {
            using var selectedPen = new Pen(Color.Gold, 2f);
            var points = TileOutline(
                    projection,
                    layer,
                    _selectedX,
                    _selectedY)
                .Select(ToScreen)
                .ToArray();

            if (points.Length >= 3)
                graphics.DrawPolygon(selectedPen, points);
            else
                graphics.DrawRectangle(
                    selectedPen,
                    Rectangle.Round(ToScreen(
                        TileBounds(
                            projection,
                            layer,
                            _selectedX,
                            _selectedY))));
        }
    }

    private SceneFrameDefinition? ResolvePreviewFrame(
        SceneLayerTileDefinition tile)
    {
        if (!string.IsNullOrWhiteSpace(tile.AnimationKey))
        {
            var animation = _findAnimation?.Invoke(tile.AnimationKey);
            var frame = animation?.FirstPreviewFrame();
            if (frame is not null)
                return frame;
        }

        return tile.Frame;
    }

    private SceneTilesheetSource? ResolveTilesheetSource(
        SceneFrameDefinition frame,
        string? animationKey)
    {
        var source = _findTilesheet?.Invoke(frame.Tilesheet);
        if (source is not null)
            return source;

        if (string.IsNullOrWhiteSpace(animationKey))
            return null;

        return _findAnimation?
            .Invoke(animationKey)?
            .FindTilesheet(frame.Tilesheet);
    }

    private void DrawMissingFrame(
        Graphics graphics,
        ProjectionLayer projection,
        SceneLayerDefinition layer,
        int x,
        int y)
    {
        var screen = Rectangle.Round(ToScreen(
            TileBounds(projection, layer, x, y)));

        using var brush = new SolidBrush(Color.FromArgb(55, 80, 80, 80));
        using var pen = new Pen(Color.DarkGray);
        graphics.FillRectangle(brush, screen);
        graphics.DrawRectangle(pen, screen);
    }

    private static RectangleF TileBounds(
        ProjectionLayer projection,
        SceneLayerDefinition definition,
        int x,
        int y)
    {
        var tile = projection[x, y];
        return tile is null
            ? RectangleF.Empty
            : tile.DrawLocationWorld;
    }

    private static PointF[] TileOutline(
        ProjectionLayer projection,
        SceneLayerDefinition definition,
        int x,
        int y)
    {
        var tile = projection[x, y];
        return tile is null
            ? []
            : tile.OutlinePointsWorld
                .Select(point => new PointF(point.X, point.Y))
                .ToArray();
    }

    private ProjectionLayer GetProjection(SceneLayerDefinition definition)
    {
        var fingerprint = new ProjectionFingerprint(
            definition.Columns,
            definition.Rows,
            definition.TileWidth,
            definition.TileHeight,
            definition.CoordinateSystemType,
            definition.OriginPx);

        if (_projections.TryGetValue(definition, out var cached) &&
            cached.Fingerprint == fingerprint)
        {
            return cached.Layer;
        }

        if (cached is not null)
            cached.Layer.Dispose();

        var projection = ProjectionLayer.Create(definition);
        _projections[definition] = new ProjectionCache(
            fingerprint,
            projection);
        return projection;
    }

    private RectangleF ToScreen(RectangleF world) =>
        new(
            world.X * Zoom + _offset.X,
            world.Y * Zoom + _offset.Y,
            world.Width * Zoom,
            world.Height * Zoom);

    private PointF ToScreen(PointF world) =>
        new(
            world.X * Zoom + _offset.X,
            world.Y * Zoom + _offset.Y);

    private void DrawCenteredMessage(Graphics graphics, string text)
    {
        using var brush = new SolidBrush(ForeColor);
        var size = graphics.MeasureString(text, Font);
        graphics.DrawString(
            text,
            Font,
            brush,
            new PointF(
                (ClientSize.Width - size.Width) / 2f,
                (ClientSize.Height - size.Height) / 2f));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var cached in _projections.Values)
                cached.Layer.Dispose();
            _projections.Clear();
        }

        base.Dispose(disposing);
    }

    private sealed record ProjectionCache(
        ProjectionFingerprint Fingerprint,
        ProjectionLayer Layer);

    private readonly record struct ProjectionFingerprint(
        int Columns,
        int Rows,
        int TileWidth,
        int TileHeight,
        CoordinateSystemTypes CoordinateSystemType,
        Point OriginPx);

    private sealed class ProjectionLayer : SceneLayer
    {
        private ProjectionLayer(
            int columns,
            int rows,
            int width,
            int height,
            CoordinateSystemTypes coordinateSystem)
            : base(
                Math.Max(1, columns),
                Math.Max(1, rows),
                width,
                height,
                1f,
                coordinateSystem)
        {
        }

        public static ProjectionLayer Create(SceneLayerDefinition definition)
        {
            var layer = new ProjectionLayer(
                definition.Columns,
                definition.Rows,
                Math.Max(1, definition.TileWidth),
                Math.Max(1, definition.TileHeight),
                definition.CoordinateSystemType)
            {
                OriginPx = definition.OriginPx
            };
            return layer;
        }
    }
}
