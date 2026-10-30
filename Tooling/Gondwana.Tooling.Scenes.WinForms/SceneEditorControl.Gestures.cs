using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Editing;
using Gondwana.Tooling.Scenes.Editing;

namespace Gondwana.Tooling.Scenes.WinForms;

public sealed partial class SceneEditorControl
{
    private readonly ToolStripLabel _selectionLabel = new();
    private Rectangle _selectedCells;
    internal Rectangle SelectedCells => _selectedCells;
    private SceneLayerDefinition? _gestureLayer;
    private SceneDefinition? _gestureDefinition;
    private SceneEditorTool _gestureTool;
    private Action<SceneLayerTileDefinition>? _gestureAssignment;
    private EditTransaction? _stroke;
    private readonly HashSet<Point> _visitedCells = [];
    private Point _selectionAnchor;
    private Point _activeGestureCell;
    private Rectangle _previousSelection;
    private Point _previousActiveCell;

    // Capture logical values, not borrowed preview sources: reloads must not redirect a stroke.
    private static Action<SceneLayerTileDefinition> FrameAssignment(FrameTag frame)
    {
        var definition = frame.Source.CreateFrame(frame.Region, frame.X, frame.Y);
        return tile => SetFrame(tile, definition);
    }

    private static void SetFrame(SceneLayerTileDefinition tile, SceneFrameDefinition frame) =>
        tile.Frame = new SceneFrameDefinition
        {
            Tilesheet = frame.Tilesheet,
            RegionName = frame.RegionName,
            XTile = frame.XTile,
            YTile = frame.YTile
        };

    private static Action<SceneLayerTileDefinition> AnimationAssignment(SceneAnimationSource source)
    {
        string key = source.Definition.Key;
        var frame = source.FirstPreviewFrame();
        return tile =>
        {
            tile.AnimationKey = key;
            tile.EnableAnimator = true;
            tile.StartAnimation = true;
            if (frame is not null) SetFrame(tile, frame);
        };
    }

    private Action<SceneLayerTileDefinition>? CapturePaintAssignment() => _paintSourceKind switch
    {
        PaintSourceKind.Frame when SelectedPaintFrame() is { } frame => FrameAssignment(frame),
        PaintSourceKind.Animation when SelectedPaintAnimation() is { } source => AnimationAssignment(source),
        _ => null
    };

    private bool BeginPreviewGesture(SceneLayerDefinition layer, int x, int y)
    {
        if (!Definition.Layers.Contains(layer) || !IsLayerVisibleInPreview(layer)) return false;
        if (ActiveTool != SceneEditorTool.Select && !ReferenceEquals(layer, _selectedLayer)) return false;
        SelectLayerNode(layer);
        _previousSelection = _selectedCells;
        _previousActiveCell = new((int)_tileX.Value, (int)_tileY.Value);
        SetSelectedTile(layer, x, y);
        var assignment = ActiveTool == SceneEditorTool.Paint ? CapturePaintAssignment() : null;
        if (ActiveTool == SceneEditorTool.Paint && assignment is null) { UpdatePaintSourceLabel(); return false; }
        _gestureTool = ActiveTool;
        _gestureLayer = layer;
        _gestureDefinition = Definition;
        _gestureAssignment = assignment;
        _selectionAnchor = _activeGestureCell = new(x, y);
        _visitedCells.Clear();
        try
        {
            if (_gestureTool != SceneEditorTool.Select)
                _stroke = Document.History.BeginTransaction(_gestureTool == SceneEditorTool.Paint ? "Paint stroke" : "Erase stroke");
            return true;
        }
        catch { EndPreviewGesture(cancel: true); throw; }
    }

    private bool GestureTargetIsValid() => ReferenceEquals(Definition, _gestureDefinition) &&
        ReferenceEquals(_selectedLayer, _gestureLayer) && _gestureLayer is { } layer &&
        Definition.Layers.Contains(layer) && IsLayerVisibleInPreview(layer);

    private void PreviewCellClicked(SceneLayerDefinition layer, int x, int y)
    {
        if (!GestureTargetIsValid()) { _preview.CancelGesture(); return; }
        if (!ReferenceEquals(layer, _gestureLayer) || x < 0 || y < 0 || x >= layer.Columns || y >= layer.Rows) return;
        _activeGestureCell = new(x, y);
        if (_gestureTool == SceneEditorTool.Select)
        {
            _selectedCells = Rectangle.FromLTRB(Math.Min(_selectionAnchor.X, x), Math.Min(_selectionAnchor.Y, y),
                Math.Max(_selectionAnchor.X, x) + 1, Math.Max(_selectionAnchor.Y, y) + 1);
            UpdateSelectionDisplay();
        }
        else if (_visitedCells.Add(new(x, y)))
        {
            if (_gestureTool == SceneEditorTool.Paint)
                Document.EditTile(layer, x, y, "Assign source", _gestureAssignment!);
            else Document.RemoveTile(layer, x, y);
        }
    }

    private void EndPreviewGesture(bool cancel)
    {
        var transaction = _stroke;
        var layer = _gestureLayer;
        bool selecting = _gestureTool == SceneEditorTool.Select;
        _stroke = null;
        _gestureLayer = null;
        _gestureDefinition = null;
        _gestureAssignment = null;
        _visitedCells.Clear();
        try
        {
            if (!cancel) transaction?.Commit();
        }
        finally
        {
            transaction?.Dispose();
            if (!IsDisposed && !Disposing && layer is not null && Definition.Layers.Contains(layer))
            {
                if (cancel && selecting) _selectedCells = _previousSelection;
                var cell = cancel ? _previousActiveCell : _activeGestureCell;
                SetSelectedTile(layer, cell.X, cell.Y, preserveSelection: selecting);
                RefreshPreview();
                UpdateValidation();
            }
        }
    }

    private void EditingContextChanging()
    {
        _preview.CancelGesture();
        _selectedCells = Rectangle.Empty;
        UpdateSelectionDisplay();
    }

    private void ClearMultiSelection()
    {
        _selectedCells = _selectedLayer is { Columns: > 0, Rows: > 0 }
            ? new Rectangle((int)_tileX.Value, (int)_tileY.Value, 1, 1) : Rectangle.Empty;
        UpdateSelectionDisplay();
    }

    private void UpdateSelectionDisplay()
    {
        long count = (long)_selectedCells.Width * _selectedCells.Height;
        _selectionLabel.Text = count > 1
            ? $"{count} cells selected - Tile Properties edits the active cell only"
            : "Tile Properties edits the active cell only";
        _preview.SetSelectedCells(_selectedCells);
    }

    private void EditSelectedCells(bool erase)
    {
        _preview.CancelGesture();
        var layer = _selectedLayer;
        if (layer is null || !Definition.Layers.Contains(layer) || !IsLayerVisibleInPreview(layer)) return;
        var cells = Rectangle.Intersect(_selectedCells, new Rectangle(0, 0, layer.Columns, layer.Rows));
        if (cells.IsEmpty) return;
        var assignment = erase ? null : CapturePaintAssignment();
        if (!erase && assignment is null) { UpdatePaintSourceLabel(); return; }
        using (var transaction = Document.History.BeginTransaction(erase ? "Erase selection" : "Paint selection"))
        {
            for (int y = cells.Top; y < cells.Bottom; y++)
                for (int x = cells.Left; x < cells.Right; x++)
                {
                    if (erase) Document.RemoveTile(layer, x, y);
                    else Document.EditTile(layer, x, y, "Assign source", assignment!);
                }
            transaction.Commit();
        }
        RefreshTileProperties();
        RefreshPreview();
        UpdateValidation();
    }
}
