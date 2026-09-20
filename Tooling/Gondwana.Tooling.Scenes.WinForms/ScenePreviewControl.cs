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

        using var projection = ProjectionLayer.Create(layer);
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
            using var projection = ProjectionLayer.Create(layer);

            for (int y = 0; y < layer.Rows; y++)
            for (int x = 0; x < layer.Columns; x++)
            {
                var rect = TileBounds(projection, layer, x, y);
                bounds = hasBounds
                    ? RectangleF.Union(bounds, rect)
                    : rect;
                hasBounds = true;
            }
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
        using var projection = ProjectionLayer.Create(layer);

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
        PointF anchor = projection.GridToWorldPx(new PointF(x, y));

        return definition.CoordinateSystemType is
            CoordinateSystemTypes.IsometricRhombic or
            CoordinateSystemTypes.IsometricAxial
            ? new RectangleF(
                anchor.X - definition.TileWidth / 2f,
                anchor.Y,
                definition.TileWidth,
                definition.TileHeight)
            : new RectangleF(
                anchor.X,
                anchor.Y,
                definition.TileWidth,
                definition.TileHeight);
    }

    private static PointF[] TileOutline(
        ProjectionLayer projection,
        SceneLayerDefinition definition,
        int x,
        int y)
    {
        var rect = TileBounds(projection, definition, x, y);
        float cx = rect.Left + rect.Width / 2f;
        float cy = rect.Top + rect.Height / 2f;

        return definition.CoordinateSystemType switch
        {
            CoordinateSystemTypes.IsometricRhombic or
            CoordinateSystemTypes.IsometricAxial =>
            [
                new(cx, rect.Top),
                new(rect.Right, cy),
                new(cx, rect.Bottom),
                new(rect.Left, cy)
            ],

            CoordinateSystemTypes.HexAxialFlatTop =>
            [
                new(rect.Left + rect.Width * .25f, rect.Top),
                new(rect.Left + rect.Width * .75f, rect.Top),
                new(rect.Right, cy),
                new(rect.Left + rect.Width * .75f, rect.Bottom),
                new(rect.Left + rect.Width * .25f, rect.Bottom),
                new(rect.Left, cy)
            ],

            CoordinateSystemTypes.HexAxialPointedTop =>
            [
                new(cx, rect.Top),
                new(rect.Right, rect.Top + rect.Height * .25f),
                new(rect.Right, rect.Top + rect.Height * .75f),
                new(cx, rect.Bottom),
                new(rect.Left, rect.Top + rect.Height * .75f),
                new(rect.Left, rect.Top + rect.Height * .25f)
            ],

            _ =>
            [
                new(rect.Left, rect.Top),
                new(rect.Right, rect.Top),
                new(rect.Right, rect.Bottom),
                new(rect.Left, rect.Bottom)
            ]
        };
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

    private sealed class ProjectionLayer : SceneLayer
    {
        private ProjectionLayer(
            int width,
            int height,
            CoordinateSystemTypes coordinateSystem)
            : base(1, 1, width, height, 1f, coordinateSystem)
        {
        }

        public static ProjectionLayer Create(SceneLayerDefinition definition)
        {
            var layer = new ProjectionLayer(
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
