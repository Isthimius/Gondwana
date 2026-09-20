using System.Drawing.Drawing2D;
using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Scenes.Editing;

namespace Gondwana.Tooling.Scenes.WinForms;

/// <summary>
/// Lightweight definition-driven GSCN preview. It uses SceneLayer's coordinate
/// conversion implementation but does not create or register a runtime Scene.
/// </summary>
internal sealed class ScenePreviewControl : UserControl
{
    private SceneDefinition? _definition;
    private Func<string, SceneTilesheetSource?>? _findTilesheet;
    private Func<string, SceneAnimationSource?>? _findAnimation;
    private SceneLayerDefinition? _selectedLayer;
    private int _selectedX;
    private int _selectedY;

    private RectangleF _worldBounds = RectangleF.Empty;
    private float _scale = 1f;
    private PointF _offset;
    private readonly Dictionary<SceneLayerDefinition, ProjectionCache> _projections = [];

    public event Action<SceneLayerDefinition, int, int>? TileSelected;

    public ScenePreviewControl()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(24, 24, 24);
        ForeColor = Color.Gainsboro;
        Resize += (_, _) => Invalidate();
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

        _worldBounds = CalculateWorldBounds(visibleLayers);
        if (_worldBounds.Width <= 0 || _worldBounds.Height <= 0)
        {
            DrawCenteredMessage(e.Graphics, "Scene bounds are empty.");
            return;
        }

        CalculateTransform(_worldBounds);

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
            _scale <= 0)
        {
            return;
        }

        float worldX = (e.X - _offset.X) / _scale;
        float worldY = (e.Y - _offset.Y) / _scale;

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

    private void CalculateTransform(RectangleF bounds)
    {
        const float margin = 28f;
        float availableWidth = Math.Max(1, ClientSize.Width - margin * 2);
        float availableHeight = Math.Max(1, ClientSize.Height - margin * 2);

        _scale = Math.Min(
            availableWidth / Math.Max(1, bounds.Width),
            availableHeight / Math.Max(1, bounds.Height));

        _scale = Math.Clamp(_scale, 0.05f, 8f);

        float renderedWidth = bounds.Width * _scale;
        float renderedHeight = bounds.Height * _scale;

        _offset = new PointF(
            (ClientSize.Width - renderedWidth) / 2f - bounds.Left * _scale,
            (ClientSize.Height - renderedHeight) / 2f - bounds.Top * _scale);
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

        if (layer.ShowGridLines || ReferenceEquals(layer, _selectedLayer))
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
            world.X * _scale + _offset.X,
            world.Y * _scale + _offset.Y,
            world.Width * _scale,
            world.Height * _scale);

    private PointF ToScreen(PointF world) =>
        new(
            world.X * _scale + _offset.X,
            world.Y * _scale + _offset.Y);

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
