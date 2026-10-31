using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Scenes.WinForms;

public sealed partial class SceneEditorControl
{
    // Confirmation happens before any mutation or transaction. Tests can exercise both decisions.
    internal Func<long, bool>? ConfirmLargeEdit { get; set; }
    private bool ConfirmArea(long count) => count <= 10000 || (ConfirmLargeEdit?.Invoke(count) ??
        MessageBox.Show(this, $"This operation affects {count:N0} cells and may take time and evict older undo history. Continue?",
            "Large scene edit", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK);

    private static IEnumerable<Point> RectangleCells(Rectangle rectangle)
    {
        for (int y = rectangle.Top; y < rectangle.Bottom; y++)
            for (int x = rectangle.Left; x < rectangle.Right; x++) yield return new(x, y);
    }

    private void ApplyCells(SceneLayerDefinition layer, IEnumerable<Point> cells, long count, bool erase,
        Action<SceneLayerTileDefinition>? assignment, string description)
    {
        var definition = Definition;
        if (count <= 0 || !ConfirmArea(count) || IsDisposed ||
            !ReferenceEquals(Definition, definition) || !ReferenceEquals(layer, _selectedLayer) ||
            !definition.Layers.Contains(layer) || !IsLayerVisibleInPreview(layer)) return;
        using var transaction = Document.History.BeginTransaction(description);
        foreach (var cell in cells)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= layer.Columns || cell.Y >= layer.Rows) continue;
            if (erase) Document.RemoveTile(layer, cell.X, cell.Y);
            else Document.EditTile(layer, cell.X, cell.Y, description, assignment!);
        }
        transaction.Commit();
    }

    // Assignment identity deliberately excludes placement, collision, fog and persistent identity.
    private readonly record struct PaintIdentity(string? Sheet, string? Region, int X, int Y,
        string? Animation, bool Animator, bool Start)
    {
        internal static PaintIdentity Of(SceneLayerTileDefinition? tile) => new(
            tile?.Frame?.Tilesheet, tile?.Frame?.RegionName, tile?.Frame?.XTile ?? 0, tile?.Frame?.YTile ?? 0,
            tile?.AnimationKey, tile?.EnableAnimator ?? false, tile?.StartAnimation ?? false);
    }

    internal static IEnumerable<Point> FillNeighbors(CoordinateSystemTypes projection, Point cell)
    {
        int x = cell.X, y = cell.Y;
        if (projection == CoordinateSystemTypes.HexAxialFlatTop)
        {
            // Columns stagger down on odd indices: two vertical and four side neighbors.
            yield return new(x, y - 1); yield return new(x, y + 1);
            int top = y - ((x & 1) == 0 ? 1 : 0);
            yield return new(x - 1, top); yield return new(x - 1, top + 1);
            yield return new(x + 1, top); yield return new(x + 1, top + 1);
        }
        else if (projection == CoordinateSystemTypes.HexAxialPointedTop)
        {
            // Rows stagger right on odd indices.
            yield return new(x - 1, y); yield return new(x + 1, y);
            int left = x - ((y & 1) == 0 ? 1 : 0);
            yield return new(left, y - 1); yield return new(left + 1, y - 1);
            yield return new(left, y + 1); yield return new(left + 1, y + 1);
        }
        else
        {
            yield return new(x - 1, y); yield return new(x + 1, y);
            yield return new(x, y - 1); yield return new(x, y + 1);
        }
    }

    private void FillCells(SceneLayerDefinition layer, Point start, Action<SceneLayerTileDefinition> assignment)
    {
        // One temporary lookup avoids a linear tile scan for every inspected empty cell.
        var tiles = layer.Tiles.GroupBy(t => new Point(t.X, t.Y)).ToDictionary(g => g.Key, g => g.First());
        var identity = PaintIdentity.Of(tiles.GetValueOrDefault(start));
        var replacement = new SceneLayerTileDefinition();
        var original = tiles.GetValueOrDefault(start);
        if (original is not null)
        {
            replacement.Frame = original.Frame;
            replacement.AnimationKey = original.AnimationKey;
            replacement.EnableAnimator = original.EnableAnimator;
            replacement.StartAnimation = original.StartAnimation;
        }
        assignment(replacement);
        if (identity == PaintIdentity.Of(replacement)) return;
        var visited = new HashSet<Point>();
        var pending = new Queue<Point>();
        var region = new List<Point>();
        pending.Enqueue(start);
        while (pending.TryDequeue(out var cell))
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= layer.Columns || cell.Y >= layer.Rows || !visited.Add(cell)) continue;
            if (PaintIdentity.Of(tiles.GetValueOrDefault(cell)) != identity) continue;
            region.Add(cell);
            foreach (var neighbor in FillNeighbors(layer.CoordinateSystemType, cell)) pending.Enqueue(neighbor);
        }
        ApplyCells(layer, region, region.Count, false, assignment, "Fill region");
    }

    private bool NavigateSelection(Keys key)
    {
        if (_preview.HasGesture) return false;
        if (key == Keys.Escape) { ClearMultiSelection(); return true; }
        bool extend = (key & Keys.Modifiers) == Keys.Shift;
        if ((key & Keys.Modifiers) != Keys.None && !extend) return false;
        var direction = (key & Keys.KeyCode) switch
        {
            Keys.Left => new Point(-1, 0),
            Keys.Right => new Point(1, 0),
            Keys.Up => new Point(0, -1),
            Keys.Down => new Point(0, 1),
            _ => Point.Empty
        };
        if (direction == Point.Empty || _selectedLayer is not { Columns: > 0, Rows: > 0 } layer ||
            !Definition.Layers.Contains(layer) || !IsLayerVisibleInPreview(layer)) return false;
        var current = new Point((int)_tileX.Value, (int)_tileY.Value);
        if (!extend || _selectedCells.Width * (long)_selectedCells.Height <= 1) _selectionAnchor = current;
        var next = new Point(Math.Clamp(current.X + direction.X, 0, layer.Columns - 1),
            Math.Clamp(current.Y + direction.Y, 0, layer.Rows - 1));
        SetSelectedTile(layer, next.X, next.Y, preserveSelection: extend);
        if (extend)
            _selectedCells = Rectangle.FromLTRB(Math.Min(_selectionAnchor.X, next.X), Math.Min(_selectionAnchor.Y, next.Y),
                Math.Max(_selectionAnchor.X, next.X) + 1, Math.Max(_selectionAnchor.Y, next.Y) + 1);
        UpdateSelectionDisplay();
        return true;
    }

    private void EditSelectedProperties()
    {
        _preview.CancelGesture();
        if (_selectedLayer is not { } layer || !Definition.Layers.Contains(layer) || !IsLayerVisibleInPreview(layer)) return;
        var tiles = layer.Tiles.Where(t => _selectedCells.Contains(t.X, t.Y)).ToArray();
        if (tiles.Length == 0 || (long)_selectedCells.Width * _selectedCells.Height < 2) return;
        var definition = Definition;
        using var dialog = new BulkTilePropertiesDialog(tiles);
        if (dialog.ShowDialog(this) != DialogResult.OK || !ReferenceEquals(definition, Definition) ||
            !ReferenceEquals(layer, _selectedLayer) || !Definition.Layers.Contains(layer)) return;
        ApplyBulkProperties(layer, tiles, dialog.Changes);
    }

    internal void ApplyBulkProperties(SceneLayerDefinition layer, IReadOnlyList<SceneLayerTileDefinition> tiles,
        IReadOnlyDictionary<string, object?> changes)
    {
        _preview.CancelGesture();
        if (changes.Count == 0 || !ReferenceEquals(layer, _selectedLayer) || !Definition.Layers.Contains(layer) ||
            !IsLayerVisibleInPreview(layer)) return;
        var definition = Definition;
        var selection = _selectedCells;
        if (!ConfirmArea(tiles.Count) || _selectedCells != selection || IsDisposed || !ReferenceEquals(Definition, definition) ||
            !ReferenceEquals(layer, _selectedLayer) || !definition.Layers.Contains(layer) || !IsLayerVisibleInPreview(layer)) return;
        using (var transaction = Document.History.BeginTransaction("Edit selected properties"))
        {
            foreach (var tile in tiles)
            {
                if (!layer.Tiles.Contains(tile) || !_selectedCells.Contains(tile.X, tile.Y)) continue;
                var adapter = new TilePropertyAdapter(Document, layer, tile.X, tile.Y, () => { });
                foreach (var (name, value) in changes)
                    BulkTilePropertiesDialog.EditableProperties.Single(p => p.Name == name).SetValue(adapter, value);
            }
            transaction.Commit();
        }
        RefreshTileProperties(); RefreshPreview(); UpdateValidation();
    }
}
