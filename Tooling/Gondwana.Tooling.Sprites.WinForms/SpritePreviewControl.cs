using System.Drawing.Drawing2D;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Sprites.Editing;

namespace Gondwana.Tooling.Sprites.WinForms;

/// <summary>Definition-only preview with an unregistered projection layer.</summary>
internal sealed class SpritePreviewControl : UserControl
{
    private readonly PreviewCanvas _canvas = new();
    internal float Zoom => _canvas.Zoom;
    public SpritePreviewControl()
    {
        Dock = DockStyle.Fill;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32 };
        var minus = new Button { Text = "−", Width = 32 };
        var plus = new Button { Text = "+", Width = 32 };
        var zoom = new ComboBox { Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
        zoom.Items.AddRange(new object[] { "25%", "50%", "100%", "200%", "400%", "Fit" });
        zoom.SelectedIndex = 2;
        minus.Click += (_, _) => _canvas.SetZoom(_canvas.Zoom / 1.25f);
        plus.Click += (_, _) => _canvas.SetZoom(_canvas.Zoom * 1.25f);
        zoom.SelectedIndexChanged += (_, _) => { if (zoom.SelectedIndex == 5) _canvas.Fit(); else _canvas.SetZoom(new[] { .25f, .5f, 1f, 2f, 4f }[zoom.SelectedIndex]); };
        bar.Controls.AddRange([minus, plus, zoom]);
        Controls.Add(_canvas);
        Controls.Add(bar);
    }
    public void SetSelection(SpriteInstanceDefinition? entry, SpriteTilesheetSource? source, SceneLayerDefinition? layer) => _canvas.SetSelection(entry, source, layer);

    private sealed class PreviewCanvas : UserControl
    {
        private SpriteInstanceDefinition? _entry;
        private SpriteTilesheetSource? _source;
        private ProjectionLayer? _projection;
        private Rectangle _bounds;
        private Point _anchor;
        public float Zoom { get; private set; } = 1;
        public PreviewCanvas() { Dock = DockStyle.Fill; DoubleBuffered = true; AutoScroll = true; BackColor = Color.FromArgb(24, 24, 24); }
        public void SetSelection(SpriteInstanceDefinition? entry, SpriteTilesheetSource? source, SceneLayerDefinition? layer)
        {
            _projection?.Dispose();
            _projection = layer is null ? null : new ProjectionLayer(layer);
            _entry = entry;
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
            var size = entry?.RenderSize ?? Size.Empty;
            int x = _anchor.X + (entry?.NudgeX ?? 0), y = _anchor.Y + (entry?.NudgeY ?? 0);
            if (entry is not null && _projection is not null)
            {
                x += entry.HorizAlign switch { Gondwana.Drawing.Sprites.HorizontalAlignment.Center => (_projection.TileWidth - size.Width) / 2, Gondwana.Drawing.Sprites.HorizontalAlignment.Right => _projection.TileWidth - size.Width, _ => 0 };
                y += entry.VertAlign switch { VerticalAlignment.Middle => (_projection.TileHeight - size.Height) / 2, VerticalAlignment.Bottom => _projection.TileHeight - size.Height, _ => 0 };
            }
            _bounds = new Rectangle(x, y, size.Width, size.Height);
            SetZoom(Zoom);
        }
        public void SetZoom(float zoom)
        {
            Zoom = Math.Clamp(zoom, .05f, 16f);
            AutoScrollMinSize = new Size((int)((Math.Abs(_bounds.X - _anchor.X) + _bounds.Width * 2 + 80) * Zoom), (int)((Math.Abs(_bounds.Y - _anchor.Y) + _bounds.Height * 2 + 80) * Zoom));
            Invalidate();
        }
        public void Fit() => SetZoom(Math.Min(ClientSize.Width / (float)Math.Max(1, _bounds.Width * 2 + 80), ClientSize.Height / (float)Math.Max(1, _bounds.Height * 2 + 80)));
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) != 0) SetZoom(Zoom * MathF.Pow(1.25f, e.Delta / 120f));
            else base.OnMouseWheel(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_entry is null) return;
            var g = e.Graphics;
            g.TranslateTransform(AutoScrollPosition.X + 40 + Math.Max(0, _anchor.X - _bounds.X) * Zoom, AutoScrollPosition.Y + 40 + Math.Max(0, _anchor.Y - _bounds.Y) * Zoom);
            g.ScaleTransform(Zoom, Zoom);
            g.TranslateTransform(-_anchor.X, -_anchor.Y);
            g.DrawLine(Pens.Gold, _anchor.X - 6, _anchor.Y, _anchor.X + 6, _anchor.Y);
            g.DrawLine(Pens.Gold, _anchor.X, _anchor.Y - 6, _anchor.X, _anchor.Y + 6);
            var state = g.Save();
            g.TranslateTransform(_bounds.X + _bounds.Width / 2f, _bounds.Y + _bounds.Height / 2f);
            g.RotateTransform(_entry.Rotation);
            g.TranslateTransform(-_bounds.X - _bounds.Width / 2f, -_bounds.Y - _bounds.Height / 2f);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            if (_entry.Visible && _entry.Frame is { } frame && _source?.Image is { } image && _source.TryResolve(frame, out _, out var bounds))
                g.DrawImage(image, _bounds, bounds, GraphicsUnit.Pixel);
            g.DrawRectangle(Pens.Gray, _bounds);
            g.Restore(state);
            // Runtime Tile.CollisionArea is axis-aligned and uses the logical draw bounds.
            if (_entry.CollisionsEnabled)
            {
                var adjust = _entry.AdjustCollisionArea;
                if (_entry.AdjustCollisionAreaByFrame && _entry.Frame is { } collisionFrame &&
                    _source is not null && _source.TryResolve(collisionFrame, out var region, out _) && region is not null)
                {
                    adjust = region.Frames.FirstOrDefault(frame => frame.XTile == collisionFrame.XTile && frame.YTile == collisionFrame.YTile)?.CollisionAdjust
                        ?? region.CollisionAdjust;
                }
                var collision = adjust.ApplyTo(_bounds);
                if (collision.Width > 0 && collision.Height > 0) g.DrawRectangle(Pens.LimeGreen, collision);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) _projection?.Dispose(); base.Dispose(disposing); }
    }
    private sealed class ProjectionLayer : SceneLayer
    {
        public ProjectionLayer(SceneLayerDefinition layer) : base(1, 1, Math.Max(1, layer.TileWidth), Math.Max(1, layer.TileHeight), 1f, layer.CoordinateSystemType) { OriginPx = layer.OriginPx; }
    }
}
