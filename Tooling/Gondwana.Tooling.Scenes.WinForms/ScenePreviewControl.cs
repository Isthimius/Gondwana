using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Gondwana.Drawing;
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
    internal bool RequireSelectedLayer { get; set; }

    internal event Func<SceneLayerDefinition, int, int, bool>? GestureStarting;
    internal event Action<bool>? GestureEnded;
    private SceneLayerDefinition? _gestureLayer;
    private PointF _previousWorld;
    private Rectangle _selectedCells;
    private Point? _lastGestureCell;
    internal bool HasGesture => _gestureLayer is not null;

    internal void SetSelectedCells(Rectangle cells)
    {
        _selectedCells = cells;
        Invalidate();
    }

    internal void CancelGesture() => FinishGesture(cancel: true);

    private const float ZoomStepFactor = 1.25f;
    private const float PreviewMargin = 28f;
    private SceneDefinition? _definition;
    private Func<string, SceneTilesheetSource?>? _findTilesheet;
    private Func<string, SceneAnimationSource?>? _findAnimation;
    private Func<SceneLayerDefinition, bool>? _showLayer;
    private SceneLayerDefinition? _selectedLayer;
    private int _selectedX;
    private int _selectedY;
    private int _zoomWheelDelta;
    private bool _fitToWindow;

    private RectangleF _worldBounds = RectangleF.Empty;
    private PointF _offset;
    private readonly Dictionary<SceneLayerDefinition, ProjectionCache> _projections = [];

    /// <summary>Gets displayed pixels per world pixel.</summary>
    public float Zoom { get; private set; } = 1f;
    /// <summary>Gets whether automatic fitting is active.</summary>
    internal bool IsFit => _fitToWindow;
    /// <summary>Occurs when magnification or Fit state changes.</summary>
    internal event Action? ZoomChanged;
    /// <summary>Gets or sets whether cell outlines are drawn during the next repaint.</summary>
    public bool ShowGridLines { get; set; } = true;

    /// <summary>Occurs on a successful cell hit, supplying its layer, zero-based column, and zero-based row.</summary>
    public event Action<SceneLayerDefinition, int, int>? TileSelected;

    /// <summary>Creates a scrollable scene preview with a one-to-one initial scale.</summary>
    public ScenePreviewControl()
    {
        ToolingAppearance.PreserveColors(this);
        DoubleBuffered = true;
        AutoScroll = true;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(24, 24, 24);
        ForeColor = Color.Gainsboro;
        ResizeRedraw = true;
        Resize += (_, _) => UpdateExtent();
    }

    /// <summary>Assigns preview content, releases projections for removed layers, and recalculates the display extent.</summary>
    /// <param name="definition">The mutable scene definition to preview without runtime scene registration.</param>
    /// <param name="findTilesheet">Resolves a logical tilesheet name to a borrowed preview source, or null.</param>
    /// <param name="findAnimation">Resolves an animation key to a borrowed preview source, or null.</param>
    /// <param name="showLayer">Optional preview-only visibility filter; without one, uses the authored Visible property.</param>
    public void Configure(
        SceneDefinition definition,
        Func<string, SceneTilesheetSource?> findTilesheet,
        Func<string, SceneAnimationSource?> findAnimation,
        Func<SceneLayerDefinition, bool>? showLayer = null)
    {
        if (HasGesture) CancelGesture();
        _definition = definition;
        _findTilesheet = findTilesheet;
        _findAnimation = findAnimation;
        _showLayer = showLayer;

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

    /// <summary>Changes magnification while preserving the viewport center as far as scrolling permits.</summary>
    /// <param name="zoom">Pixels per source pixel, clamped to the range 0.05 through 16.</param>
    public void SetZoom(float zoom)
    {
        CancelGesture();
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
        ZoomChanged?.Invoke();
    }

    internal void ZoomIn() =>
        SetZoom(Zoom * ZoomStepFactor);

    internal void ZoomOut() =>
        SetZoom(Zoom / ZoomStepFactor);

    /// <summary>
    /// Adjusts the preview zoom to fit its content inside the viewport.
    /// </summary>
    public void Fit()
    {
        CancelGesture();
        _fitToWindow = true;
        AutoScrollPosition = Point.Empty;
        UpdateExtent();
        ZoomChanged?.Invoke();
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

    /// <inheritdoc/>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Application.AddMessageFilter(this);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        UpdateOffset();
        Invalidate();
    }

    /// <inheritdoc/>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // ScrollableControl moves the painted pixels for wheel input without
        // raising OnScroll. The fixed-position overlay must be repainted too,
        // or its old pixels remain visible in the scrolled scene.
        var previousPosition = AutoScrollPosition;
        base.OnMouseWheel(e);
        if (AutoScrollPosition != previousPosition)
        {
            UpdateOffset();
            Invalidate();
        }
    }

    /// <summary>Updates the highlighted scene cell and requests a repaint.</summary>
    /// <param name="layer">Layer containing the selected cell, or null to clear the highlight.</param>
    /// <param name="x">Zero-based cell column.</param>
    /// <param name="y">Zero-based cell row.</param>
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

    private bool IsLayerDisplayed(SceneLayerDefinition layer) =>
        _showLayer?.Invoke(layer) ?? layer.Visible;

    /// <inheritdoc/>
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
            .Where(layer => IsLayerDisplayed(layer) && layer.Columns > 0 && layer.Rows > 0)
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
            $"Active layer: {_selectedLayer?.ID ?? "(none)"} - {visibleLayers.Count} visible layer(s)\n" +
            $"{(long)_selectedCells.Width * _selectedCells.Height} cell(s) selected - Tile Properties edits active cell ({_selectedX},{_selectedY}) only",
            Font,
            labelBrush,
            new PointF(8, 8));
    }

    /// <inheritdoc/>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || _definition is null)
            return;

        UpdateOffset();
        if (RequireSelectedLayer && (_selectedLayer is null ||
            !_definition.Layers.Contains(_selectedLayer) || !IsLayerDisplayed(_selectedLayer))) return;

        var layer = _selectedLayer is not null && _definition.Layers.Contains(_selectedLayer) && IsLayerDisplayed(_selectedLayer)
            ? _selectedLayer
            : _definition.Layers
                .Where(candidate => IsLayerDisplayed(candidate))
                .OrderByDescending(candidate => candidate.ZOrder)
                .FirstOrDefault();

        if (layer is null ||
            layer.Columns <= 0 ||
            layer.Rows <= 0 ||
            Zoom <= 0)
        {
            return;
        }

        CancelGesture();
        Focus();
        var world = ScreenToWorld(e.Location);
        if (!TryHitCell(layer, world, out var cell)) return;
        if (GestureStarting is not null && !GestureStarting(layer, cell.X, cell.Y)) return;

        _gestureLayer = layer;
        _lastGestureCell = null;
        _previousWorld = world;
        try
        {
            Capture = true;
            if (!Capture) { CancelGesture(); return; }
            VisitCell(layer, cell);
        }
        catch { CancelGesture(); throw; }
    }

    private PointF ScreenToWorld(Point point) => new((point.X - _offset.X) / Zoom, (point.Y - _offset.Y) / Zoom);

    private bool TryHitCell(SceneLayerDefinition layer, PointF world, out Point cell)
    {
        var grid = GetProjection(layer).WorldPxToGrid(world);
        cell = new((int)Math.Floor(grid.X + .0001f), (int)Math.Floor(grid.Y + .0001f));
        return cell.X >= 0 && cell.Y >= 0 && cell.X < layer.Columns && cell.Y < layer.Rows;
    }

    private void VisitCell(SceneLayerDefinition layer, Point cell)
    {
        if (_lastGestureCell == cell) return;
        _lastGestureCell = cell;
        _selectedLayer = layer;
        _selectedX = cell.X;
        _selectedY = cell.Y;
        TileSelected?.Invoke(layer, cell.X, cell.Y);
        Invalidate();
    }

    private void ContinueGesture(Point location)
    {
        if (_gestureLayer is not { } layer) return;
        if (_definition?.Layers.Contains(layer) != true || !IsLayerDisplayed(layer))
        {
            CancelGesture();
            return;
        }
        UpdateOffset();
        var world = ScreenToWorld(location);
        float dx = world.X - _previousWorld.X, dy = world.Y - _previousWorld.Y;
        // Sample in world pixels, then ask the same projection used by click hit-testing.
        // Sub-cell steps follow staggered hex polygons as well as affine grid projections.
        float step = Math.Clamp(Math.Min(layer.TileWidth, layer.TileHeight) / 16f, .0625f, .25f);
        int samples = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) / step));
        var start = _previousWorld;
        _previousWorld = world;
        for (int i = 1; i <= samples && ReferenceEquals(_gestureLayer, layer); i++)
        {
            var sample = new PointF(start.X + dx * i / samples, start.Y + dy * i / samples);
            if (TryHitCell(layer, sample, out var cell)) VisitCell(layer, cell);
        }
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!HasGesture) return;
        if ((e.Button & MouseButtons.Left) == 0) { CancelGesture(); return; }
        try { ContinueGesture(e.Location); }
        catch { CancelGesture(); throw; }
    }

    /// <inheritdoc/>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !HasGesture) return;
        try { ContinueGesture(e.Location); FinishGesture(cancel: false); }
        catch { CancelGesture(); throw; }
    }

    private void FinishGesture(bool cancel)
    {
        if (!HasGesture) return;
        _gestureLayer = null;
        try { GestureEnded?.Invoke(cancel); }
        finally { Capture = false; Invalidate(); }
    }

    /// <inheritdoc/>
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture) CancelGesture();
    }

    /// <inheritdoc/>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && HasGesture) { CancelGesture(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
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
            foreach (var tile in layer.Tiles.Where(tile =>
                tile.Visible &&
                tile.X >= 0 &&
                tile.Y >= 0 &&
                tile.X < layer.Columns &&
                tile.Y < layer.Rows))
            {
                var frame = ResolvePreviewFrame(tile);
                var source = frame is null ? null : ResolveTilesheetSource(frame, tile.AnimationKey);
                if (frame is not null && source?.TryResolve(frame, out var region, out _) == true)
                    rect = RectangleF.Union(rect, TileTransformGeometry.GetVisualBounds(
                        Rectangle.Round(TileBounds(projection, layer, tile.X, tile.Y)), region!.Overhang, tile.Transform));
            }
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
                IsLayerDisplayed(layer) &&
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
                !source.TryResolve(frame, out var region, out var sourceBounds))
            {
                DrawMissingFrame(graphics, projection, layer, tile.X, tile.Y);
                continue;
            }

            var cell = Rectangle.Round(TileBounds(projection, layer, tile.X, tile.Y));
            var world = TileTransformGeometry.GetVisualBounds(cell, region!.Overhang, tile.Transform);
            var screen = ToScreen(world);

            graphics.DrawImage(
                source.Image,
                new[]
                {
                    TileTransformGeometry.MapToDestination(new(0, 0), screen, tile.Transform),
                    TileTransformGeometry.MapToDestination(new(1, 0), screen, tile.Transform),
                    TileTransformGeometry.MapToDestination(new(0, 1), screen, tile.Transform)
                },
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

        if (ReferenceEquals(layer, _selectedLayer))
        {
            var cells = _selectedCells.IsEmpty ? new Rectangle(_selectedX, _selectedY, 1, 1) : _selectedCells;
            cells.Intersect(new Rectangle(0, 0, layer.Columns, layer.Rows));
            var corners = new[] { Point.Empty, new Point(ClientSize.Width, 0),
                new Point(0, ClientSize.Height), new Point(ClientSize.Width, ClientSize.Height) }
                .Select(point => projection.WorldPxToGrid(ScreenToWorld(point))).ToArray();
            // Cull off-screen selection cells using the existing inverse projection.
            cells.Intersect(Rectangle.FromLTRB((int)Math.Floor(corners.Min(point => point.X)) - 2,
                (int)Math.Floor(corners.Min(point => point.Y)) - 2, (int)Math.Ceiling(corners.Max(point => point.X)) + 3,
                (int)Math.Ceiling(corners.Max(point => point.Y)) + 3));
            using var selectedPen = new Pen(Color.Gold, 2f);

            for (int y = cells.Top; y < cells.Bottom; y++)
                for (int x = cells.Left; x < cells.Right; x++)
                {
                    var points = TileOutline(projection, layer, x, y).Select(ToScreen).ToArray();
                    if (points.Length >= 3)
                    {

                        graphics.DrawPolygon(selectedPen, points);
                    }
                }
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
        var animation = string.IsNullOrWhiteSpace(animationKey) ? null : _findAnimation?.Invoke(animationKey);
        // An animation's explicit source belongs to that animation, even when the
        // scene declares a different archive/loose source with the same logical name.
        if (animation?.Definition.TilesheetSources.Any(source => source.Tilesheet == frame.Tilesheet) == true)
            return animation.FindTilesheet(frame.Tilesheet);
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

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelGesture();
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

        /// <summary>
        /// Creates a new projection layer.
        /// </summary>
        /// <param name="definition">The persisted definition to process.</param>
        /// <returns>The resulting projection layer.</returns>
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
