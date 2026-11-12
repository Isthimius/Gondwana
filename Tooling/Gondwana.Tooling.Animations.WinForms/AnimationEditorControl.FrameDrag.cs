using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Tooling.Animations.Editing;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Animations.WinForms;

public sealed partial class AnimationEditorControl
{
    private TilesheetFrameDrag? _hoveredFrame;
    private AnimationDefinition? _hoveredDefinition;
    private string? _frameDropError;

    private void WireFrameDragDrop()
    {
        _sourceTree.ItemDrag += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && e.Item is TreeNode node && CreateSourceFrameDrag(node) is { } frame)
                _sourceTree.DoDragDrop(new ResourceDragData<TilesheetFrameDrag>(frame), DragDropEffects.Copy);
        };

        _frames.AllowDrop = true;
        _frames.DragEnter += FrameDragEnter;
        _frames.DragOver += FrameDragOver;
        _frames.DragLeave += (_, _) => ClearFrameDrag();
        _frames.DragDrop += FrameDragDrop;
    }

    internal static TilesheetFrameDrag? CreateSourceFrameDrag(TreeNode node) =>
        node.Tag is FrameTag frame
            ? new(new ResourceAddress(frame.Source.Resource, frame.Source.Definition.Name),
                frame.Region.Name, frame.X, frame.Y)
            : null;

    private void FrameDragEnter(object? sender, DragEventArgs e)
    {
        ClearFrameDrag();
        e.Effect = DragDropEffects.None;
        if (GetDraggedFrame(e) is not { } frame)
            return;

        var definition = Definition;
        var candidate = ResolveFrameDrop(frame);
        SetFrameDropError(candidate.Error);
        if (candidate.Frame is null || IsDisposed || !ReferenceEquals(definition, Definition))
            return;

        _hoveredFrame = frame;
        _hoveredDefinition = definition;
        FrameDragOver(sender, e);
    }

    private void FrameDragOver(object? sender, DragEventArgs e)
    {
        int index = HoveredInsertionIndex(e);
        _frames.InsertionIndex = index;
        e.Effect = index >= 0 ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void FrameDragDrop(object? sender, DragEventArgs e)
    {
        int index = HoveredInsertionIndex(e);
        var frame = _hoveredFrame;
        ClearFrameDrag();
        e.Effect = DragDropEffects.None;
        if (index < 0 || frame is null)
            return;

        // Resolve again at release. A file, archive, or target can change while hovering.
        var definition = Definition;
        var frames = definition.Frames.ToArray();
        var filePath = Document.FilePath;
        var packedLocation = Document.PackedLocation;
        var candidate = ResolveFrameDrop(frame);
        SetFrameDropError(candidate.Error);
        if (candidate.Frame is null || IsDisposed || Disposing ||
            !ReferenceEquals(definition, Definition) || !definition.Frames.SequenceEqual(frames) ||
            filePath != Document.FilePath || !ReferenceEquals(packedLocation, Document.PackedLocation))
            return;

        using (var transaction = Document.History.BeginTransaction("Drop tilesheet frame"))
        {
            if (candidate.NeedsSource)
            {
                var resource = frame.Address.Resource;
                if (resource.EntryName is { } entry)
                    Document.AddPackedTilesheetSource(candidate.Frame.Tilesheet, resource.Path, entry);
                else
                    Document.SetLooseTilesheetSource(candidate.Frame.Tilesheet, resource.Path);
            }

            Document.InsertFrame(index, candidate.Frame);
            transaction.Commit();
        }

        _preview.ClearSourceFrame();
        RefreshSelectedDefinition();

        SelectFrame(index);
        _frames.Focus();
        e.Effect = DragDropEffects.Copy;
    }

    private (AnimationFrameDefinition? Frame, bool NeedsSource, string? Error) ResolveFrameDrop(TilesheetFrameDrag drag)
    {
        try
        {
            var address = drag.Address;
            if (address.Location.AssetType != AssetTypes.TilesheetDefinition ||
                string.IsNullOrWhiteSpace(address.Member))
                return (null, false, "Drag a frame from a specific saved GTS definition.");

            var region = new AuthoringResourceResolver(_packages).ResolveTilesheetFrame(drag);
            var sources = Definition.TilesheetSources.Where(source => source.Tilesheet == address.Member).ToArray();
            if (sources.Length > 1)
                return (null, false, $"Multiple sources are registered for '{address.Member}'.");

            if (sources.Length == 1)
            {
                var resource = AuthoringResourceResolver.TilesheetSource(sources[0], Document.BaseDirectory);
                if (new ResourceAddress(resource, address.Member) != address with { FieldPath = null })
                    return (null, false, $"'{address.Member}' is already registered from a different GTS source.");
            }

            return (TilesheetSource.CreateFrame(address.Member, region.Name, drag.X, drag.Y),
                sources.Length == 0, null);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            return (null, false, ex.Message);
        }
    }

    private static TilesheetFrameDrag? GetDraggedFrame(DragEventArgs e) =>
        (e.AllowedEffect & DragDropEffects.Copy) != 0 &&
        e.Data is ResourceDragData<TilesheetFrameDrag> data ? data.Payload : null;

    private int HoveredInsertionIndex(DragEventArgs e) =>
        _hoveredFrame is not null && ReferenceEquals(GetDraggedFrame(e), _hoveredFrame) &&
        ReferenceEquals(_hoveredDefinition, Definition) && !IsDisposed && !Disposing && Enabled &&
        !Document.History.InTransaction && !Document.History.IsExecuting && _frames.Items.Count == Definition.Frames.Count
            ? _frames.GetInsertionIndex(_frames.PointToClient(new Point(e.X, e.Y)))
            : -1;

    private void ClearFrameDrag()
    {
        _hoveredFrame = null;
        _hoveredDefinition = null;
        _frames.InsertionIndex = -1;
    }

    private void SetFrameDropError(string? error)
    {
        if (IsDisposed || Disposing || _frameDropError == error)
            return;

        _frameDropError = error;
        UpdateValidation();
    }
}
