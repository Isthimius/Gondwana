using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Tooling.Animations.Editing;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Animations.WinForms;

public sealed partial class AnimationEditorControl
{
    private ResourceDragData<TilesheetFrameDrag>? _hoveredFrame;
    private AnimationDefinition? _hoveredDefinition;
    private string? _frameDropError;

    internal AnimationDrag? CreateAnimationDrag()
    {
        if (Document.IsDirty || string.IsNullOrWhiteSpace(Definition.Key))
            return null;

        var location = Document.PackedLocation;
        if (location is null && Document.FilePath is { } path)
            location = new AssetLocation(AssetTypes.AnimationDefinition, path);

        return location is null ? null : new(new ResourceAddress(location, Definition.Key));
    }

    private ToolStripControlHost CreateAnimationDragHandle()
    {
        var label = new Label { Text = "Drag animation", AutoSize = true, Cursor = Cursors.Hand };
        var handle = new ToolStripControlHost(label)
        {
            Name = "DragAnimation",
            ToolTipText = "Save the GANI, then drag its selected animation to a scene cell or sprite."
        };
        Rectangle threshold = Rectangle.Empty;
        AnimationDrag? pending = null;
        label.MouseDown += (_, e) =>
        {
            pending = e.Button == MouseButtons.Left ? CreateAnimationDrag() : null;
            if (pending is null)
                return;

            var size = SystemInformation.DragSize;
            threshold = new Rectangle(e.X - size.Width / 2, e.Y - size.Height / 2, size.Width, size.Height);
            label.Capture = true;
        };
        label.MouseMove += (_, e) =>
        {
            if (pending is not { } drag || e.Button != MouseButtons.Left || threshold.Contains(e.Location))
                return;

            pending = null;
            label.Capture = false;
            label.DoDragDrop(new ResourceDragData<AnimationDrag>(drag), DragDropEffects.Copy);
        };
        label.MouseUp += (_, _) => pending = null;
        label.MouseCaptureChanged += (_, _) =>
        {
            if (!label.Capture)
                pending = null;
        };
        return handle;
    }

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
        try
        {
            if (frame.Address.Location.AssetType != AssetTypes.TilesheetDefinition)
                throw new InvalidDataException("Drag a saved GTS frame.");
            var reads = frame.BeginRead();
            if (frame.Payload is { } payload)
                ResolveFrameDrop(payload, new AuthoringResourceResolver(_packages, reads.Observe));
            SetFrameDropError(null);
        }
        catch (Exception ex) when (IsFrameDropException(ex))
        {
            SetFrameDropError(ex.Message);
            return;
        }

        if (IsDisposed || Disposing || !ReferenceEquals(definition, Definition))
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
        TilesheetFrameDrag payload;
        (AnimationFrameDefinition Frame, bool NeedsSource) candidate;
        try
        {
            var reads = frame.BeginRead();
            var resolver = new AuthoringResourceResolver(_packages, reads.Observe);
            if (frame.Resolve(this, resolver) is not TilesheetFrameDrag selected)
                return;
            if (!TargetUnchanged())
                return;
            payload = selected;
            candidate = ResolveFrameDrop(payload, resolver);
            reads.Verify();
        }
        catch (Exception ex) when (IsFrameDropException(ex))
        {
            SetFrameDropError(ex.Message);
            return;
        }

        if (!TargetUnchanged())
            return;

        using (var transaction = Document.History.BeginTransaction("Drop tilesheet frame"))
        {
            if (candidate.NeedsSource)
            {
                var resource = payload.Address.Resource;
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

        bool TargetUnchanged() => !IsDisposed && !Disposing && Enabled &&
            !Document.History.InTransaction && !Document.History.IsExecuting &&
            ReferenceEquals(definition, Definition) && definition.Frames.SequenceEqual(frames) &&
            filePath == Document.FilePath && ReferenceEquals(packedLocation, Document.PackedLocation);
    }

    private (AnimationFrameDefinition Frame, bool NeedsSource) ResolveFrameDrop(TilesheetFrameDrag drag, AuthoringResourceResolver resolver)
    {
        var address = drag.Address;
        if (address.Location.AssetType != AssetTypes.TilesheetDefinition || string.IsNullOrWhiteSpace(address.Member))
            throw new InvalidDataException("Drag a frame from a specific saved GTS definition.");

        var region = resolver.ResolveTilesheetFrame(drag);
        var sources = Definition.TilesheetSources.Where(source => source.Tilesheet == address.Member).ToArray();
        if (sources.Length > 1)
            throw new InvalidDataException($"Multiple sources are registered for '{address.Member}'.");

        if (sources.Length == 1)
        {
            var resource = AuthoringResourceResolver.TilesheetSource(sources[0], Document.BaseDirectory);
            if (new ResourceAddress(resource, address.Member) != address with { FieldPath = null })
                throw new InvalidDataException($"'{address.Member}' is already registered from a different GTS source.");
        }

        return (TilesheetSource.CreateFrame(address.Member, region.Name, drag.X, drag.Y), sources.Length == 0);
    }

    private static bool IsFrameDropException(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException
        or ArgumentException or NotSupportedException or InvalidOperationException or OperationCanceledException;

    private static ResourceDragData<TilesheetFrameDrag>? GetDraggedFrame(DragEventArgs e) =>
        (e.AllowedEffect & DragDropEffects.Copy) != 0 ? e.Data as ResourceDragData<TilesheetFrameDrag> : null;

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
