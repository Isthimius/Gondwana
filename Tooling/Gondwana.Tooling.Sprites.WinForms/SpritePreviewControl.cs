using System.Drawing.Drawing2D;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Sprites.Editing;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Sprites.WinForms;

/// <summary>Definition-only preview with an unregistered projection layer.</summary>
internal sealed class SpritePreviewControl : UserControl
{
    private readonly PreviewCanvas _canvas = new();
    internal float Zoom => _canvas.Zoom;
    /// <summary>Creates a sprite preview with explicit zoom and fit controls.</summary>
    public SpritePreviewControl()
    {
        Dock = DockStyle.Fill;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32 };
        var minus = new Button { Text = "−", Width = 32 };
        var plus = new Button { Text = "+", Width = 32 };
        var zoom = new ComboBox { Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
        var zoomSelector = new ZoomSelector(zoom, requested =>
        {
            if (requested is { } value) _canvas.SetZoom(value);
            else _canvas.Fit();
        });
        _canvas.ZoomChanged += () => zoomSelector.Show(_canvas.Zoom, _canvas.IsFit);
        minus.Click += (_, _) => _canvas.SetZoom(_canvas.Zoom / 1.25f);
        plus.Click += (_, _) => _canvas.SetZoom(_canvas.Zoom * 1.25f);
        bar.Controls.AddRange([minus, plus, zoom]);
        Controls.Add(_canvas);
        Controls.Add(bar);
    }
    /// <summary>Updates the sprite preview and its projection without modifying the sprite definition.</summary>
    /// <param name="entry">Sprite to preview, or null to clear the selection.</param>
    /// <param name="source">Borrowed tilesheet and bitmap for the selected frame, or null when unavailable.</param>
    /// <param name="layer">Layer geometry for an unregistered preview projection, or null when unavailable.</param>
    /// <param name="frame">Optional animation preview frame; null uses the sprite's assigned frame.</param>
    public void SetSelection(SpriteInstanceDefinition? entry, SpriteTilesheetSource? source, SceneLayerDefinition? layer, SpriteFrameDefinition? frame = null) => _canvas.SetSelection(entry, source, layer, frame);

    private sealed class PreviewCanvas : UserControl
    {
        private SpriteInstanceDefinition? _entry;
        private SpriteFrameDefinition? _frame;
        private SpriteTilesheetSource? _source;
        private ProjectionLayer? _projection;
        private Rectangle _bounds;
        private Point _anchor;
        private PointF _origin;
        private RectangleF _extent;
        private bool _fit;
        /// <summary>Gets displayed pixels per world pixel.</summary>
        public float Zoom { get; private set; } = 1;
        /// <summary>Gets whether automatic fitting is active.</summary>
        public bool IsFit => _fit;
        /// <summary>Occurs when magnification or Fit state changes.</summary>
        public event Action? ZoomChanged;
        /// <summary>Creates a scrollable sprite canvas that refits on resize when fit mode is active.</summary>
        public PreviewCanvas()
        {
            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            AutoScroll = true;
            BackColor = Color.FromArgb(24, 24, 24);
            Resize += (_, _) => { if (_fit) Fit(); };
        }
        /// <summary>Updates the sprite preview and its projection without modifying the sprite definition.</summary>
        /// <param name="entry">Sprite to preview, or null to clear the selection.</param>
        /// <param name="source">Borrowed tilesheet and bitmap for the selected frame, or null when unavailable.</param>
        /// <param name="layer">Layer geometry for an unregistered preview projection, or null when unavailable.</param>
        /// <param name="previewFrame">Optional animation preview frame; null uses the sprite's assigned frame.</param>
        public void SetSelection(SpriteInstanceDefinition? entry, SpriteTilesheetSource? source, SceneLayerDefinition? layer, SpriteFrameDefinition? previewFrame)
        {
            _projection?.Dispose();
            _projection = layer is null ? null : new ProjectionLayer(layer);
            _entry = entry;
            _frame = previewFrame ?? entry?.Frame;
            _source = source;
            if (entry is not null && (!float.IsFinite(entry.Position.X) || !float.IsFinite(entry.Position.Y) ||
                !float.IsFinite(entry.Rotation) || entry.RenderSize.Width < 0 || entry.RenderSize.Height < 0))
            {
                _entry = null;
                _bounds = Rectangle.Empty;
                SetZoom(Zoom);
                return;
            }
            _anchor = entry is null || _projection is null ? Point.Empty : Point.Round(_projection.GridToWorldPx(entry.Position));
            _origin = _projection?.GridToWorldPx(PointF.Empty) ?? PointF.Empty;
            Size frameSize = Size.Empty;
            if (_frame is { } frame && source?.TryResolve(frame, out var region, out _) == true && region is not null)
                frameSize = region.TileSize;
            var sceneLayerSize = _projection is null ? Size.Empty : new Size(_projection.TileWidth, _projection.TileHeight);
            var size = entry is null
                ? Size.Empty
                : entry.RenderSize != Size.Empty
                    ? entry.RenderSize
                    : SpriteManager.Instance.SizeNewSpritesToSceneLayer
                        ? sceneLayerSize
                        : frameSize;
            int x = _anchor.X + (entry?.NudgeX ?? 0), y = _anchor.Y + (entry?.NudgeY ?? 0);
            if (entry is not null && _projection is not null)
            {
                x += entry.HorizAlign switch { Gondwana.Drawing.Sprites.HorizontalAlignment.Center => (_projection.TileWidth - size.Width) / 2, Gondwana.Drawing.Sprites.HorizontalAlignment.Right => _projection.TileWidth - size.Width, _ => 0 };
                y += entry.VertAlign switch { VerticalAlignment.Middle => (_projection.TileHeight - size.Height) / 2, VerticalAlignment.Bottom => _projection.TileHeight - size.Height, _ => 0 };
            }
            _bounds = new Rectangle(x, y, size.Width, size.Height);
            // Include the layer origin and rotated corners so position changes stay
            // visible relative to the layer, and rotation cannot clip the preview.
            PointF[] corners = [new(_bounds.Left, _bounds.Top), new(_bounds.Right, _bounds.Top),
                new(_bounds.Right, _bounds.Bottom), new(_bounds.Left, _bounds.Bottom)];
            using var rotation = new Matrix();
            rotation.RotateAt(entry?.Rotation ?? 0, new PointF(_bounds.X + size.Width / 2f, _bounds.Y + size.Height / 2f));
            rotation.TransformPoints(corners);
            _extent = RectangleF.FromLTRB(
                Math.Min(Math.Min(corners.Min(point => point.X), _anchor.X), _origin.X) - 8,
                Math.Min(Math.Min(corners.Min(point => point.Y), _anchor.Y), _origin.Y) - 8,
                Math.Max(Math.Max(corners.Max(point => point.X), _anchor.X), _origin.X) + 8,
                Math.Max(Math.Max(corners.Max(point => point.Y), _anchor.Y), _origin.Y) + 8);
            if (_fit) Fit(); else SetZoom(Zoom);
        }
        /// <summary>Sets explicit magnification and disables automatic fitting.</summary>
        /// <param name="zoom">Scale factor clamped to 0.05 through 16.</param>
        public void SetZoom(float zoom)
        {
            _fit = false;
            Zoom = Math.Clamp(zoom, .05f, 16f);
            AutoScrollMinSize = new Size((int)Math.Ceiling(_extent.Width * Zoom + 80), (int)Math.Ceiling(_extent.Height * Zoom + 80));
            Invalidate();
            ZoomChanged?.Invoke();
        }
        /// <summary>
        /// Adjusts the preview zoom to fit its content inside the viewport.
        /// </summary>
        public void Fit()
        {
            SetZoom(Math.Min(Math.Max(1, ClientSize.Width - 80) / Math.Max(1, _extent.Width),
                Math.Max(1, ClientSize.Height - 80) / Math.Max(1, _extent.Height)));
            _fit = true;
            AutoScrollPosition = Point.Empty;
            ZoomChanged?.Invoke();
        }
        /// <inheritdoc/>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) != 0) SetZoom(Zoom * MathF.Pow(1.25f, e.Delta / 120f));
            else base.OnMouseWheel(e);
        }
        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_entry is null) return;
            var g = e.Graphics;
            g.TranslateTransform(AutoScrollPosition.X + 40, AutoScrollPosition.Y + 40);
            g.ScaleTransform(Zoom, Zoom);
            g.TranslateTransform(-_extent.Left, -_extent.Top);
            g.DrawLine(Pens.SlateGray, _origin, _anchor);
            g.DrawEllipse(Pens.SlateGray, _origin.X - 4, _origin.Y - 4, 8, 8);
            g.DrawLine(Pens.Gold, _anchor.X - 6, _anchor.Y, _anchor.X + 6, _anchor.Y);
            g.DrawLine(Pens.Gold, _anchor.X, _anchor.Y - 6, _anchor.X, _anchor.Y + 6);
            var state = g.Save();
            g.TranslateTransform(_bounds.X + _bounds.Width / 2f, _bounds.Y + _bounds.Height / 2f);
            g.RotateTransform(_entry.Rotation);
            g.TranslateTransform(-_bounds.X - _bounds.Width / 2f, -_bounds.Y - _bounds.Height / 2f);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            if (_entry.Visible && _frame is { } frame && _source?.Image is { } image &&
                _source.TryResolve(frame, out _, out var bounds) &&
                bounds.Width > 0 && bounds.Height > 0 && bounds.X >= 0 && bounds.Y >= 0 &&
                bounds.Right <= image.Width && bounds.Bottom <= image.Height)
                g.DrawImage(image, _bounds, bounds, GraphicsUnit.Pixel);
            g.DrawRectangle(Pens.Gray, _bounds);
            g.Restore(state);
            // Runtime Tile.CollisionArea is axis-aligned and uses the logical draw bounds.
            if (_entry.CollisionsEnabled)
            {
                var adjust = _entry.AdjustCollisionArea;
                if (_entry.AdjustCollisionAreaByFrame && _frame is { } collisionFrame &&
                    _source is not null && _source.TryResolve(collisionFrame, out var region, out _) && region is not null)
                {
                    adjust = region.Frames.FirstOrDefault(frame => frame.XTile == collisionFrame.XTile && frame.YTile == collisionFrame.YTile)?.CollisionAdjust
                        ?? region.CollisionAdjust;
                }
                var collision = adjust.ApplyTo(_bounds);
                if (collision.Width > 0 && collision.Height > 0) g.DrawRectangle(Pens.LimeGreen, collision);
            }
        }
        /// <inheritdoc/>
        protected override void Dispose(bool disposing) { if (disposing) _projection?.Dispose(); base.Dispose(disposing); }
    }
    private sealed class ProjectionLayer : SceneLayer
    {
        /// <summary>Creates a one-cell preview projection using the source layer's origin and coordinate system, with tile dimensions clamped to at least one pixel.</summary>
        /// <param name="layer">Layer supplying projection geometry.</param>
        public ProjectionLayer(SceneLayerDefinition layer) : base(1, 1, Math.Max(1, layer.TileWidth), Math.Max(1, layer.TileHeight), 1f, layer.CoordinateSystemType) { OriginPx = layer.OriginPx; }
    }
}
