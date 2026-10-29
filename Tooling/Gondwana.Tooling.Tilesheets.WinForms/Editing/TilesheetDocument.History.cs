using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Tilesheets.Editing;

public sealed partial class TilesheetDocument : IDisposable
{
    private readonly ReferenceOrigins _origins = new();

    private void AddMembers(IReadOnlyList<TilesheetDefinition> members, string description)
    {
        var selected = Definition;
        History.Execute(description, () =>
        {
            foreach (var member in members) if (!_definitions.Contains(member)) _definitions.Add(member);
            Definition = members[0];
        }, () =>
        {
            foreach (var member in members) _definitions.Remove(member);
            Definition = selected;
        });
    }

    private void HistoryChanged(object? sender, EventArgs args)
    {
        RestoreReferenceOrigins();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Edits the properties of one owned model object; child objects and collections must use their own operations.</summary>
    public bool Edit(object target, string description, Action change)
    {
        if (!Owns(target)) throw new ArgumentException("The object does not belong to this document.", nameof(target));
        return History.Edit(description, target, change);
    }

    /// <summary>Releases retained history and document listeners.</summary>
    public void Dispose()
    {
        History.Changed -= HistoryChanged;
        History.Dispose();
        Changed = null;
    }

    private bool Owns(object target) => _definitions.Any(d => ReferenceEquals(d, target) || ReferenceEquals(d.Mask, target) ||
        d.Regions.Any(r => ReferenceEquals(r, target) || r.Frames.Any(f => ReferenceEquals(f, target))));

    /// <summary>Adds a region as one reversible operation.</summary>
    public void AddRegion(TilesheetRegionDefinition region) => History.Insert("Add region", Definition.Regions, Definition.Regions.Count, region);

    /// <summary>Removes a region while retaining its frame metadata for undo.</summary>
    public bool RemoveRegion(TilesheetRegionDefinition region) => History.Remove("Remove region", Definition.Regions, region);

    /// <summary>Edits one frame, creating sparse metadata only for an actual change.</summary>
    public bool EditFrame(TilesheetRegionDefinition region, int x, int y, string description, Action<TilesheetFrameDefinition> change)
    {
        if (!Owns(region)) throw new ArgumentException("Region does not belong to this document.", nameof(region));
        var existing = FindFrame(region, x, y);
        var frame = existing ?? new TilesheetFrameDefinition { XTile = x, YTile = y };
        using var transaction = History.BeginTransaction(description);
        bool changed = History.Edit(description, frame, () => change(frame));
        if (changed && existing is null) History.Insert(description, region.Frames, region.Frames.Count, frame);
        transaction.Commit();
        return changed;
    }

    private void RememberReferenceOrigins()
    {
        foreach (var member in _definitions)
            if (member.Image is { } image) _origins.Remember(image, BaseDirectory, PackedLocation?.Path);
    }

    private void RestoreReferenceOrigins()
    {
        foreach (var member in _definitions)
        {
            if (member.Image is { } image)
                _origins.Restore(image, BaseDirectory, PackedLocation?.Path, image.AssetEntryName,
                    () => image.FilePath, value => image.FilePath = value,
                    () => image.AssetsFilePath, value => image.AssetsFilePath = value);
            if (FilePath is not null && !string.Equals(member.Source.GtsFilePath, FilePath, StringComparison.OrdinalIgnoreCase))
                member.Source = TilesheetDefinitionSource.LooseDefinitionFile(FilePath);
        }
    }
}
