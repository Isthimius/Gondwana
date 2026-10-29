using Gondwana.Drawing.Animation.GANI;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Animations.Editing;

public sealed partial class AnimationDocument : IDisposable
{
    private readonly ReferenceOrigins _origins = new();

    // Loading legacy files used to mark the inferred dependency dirty. Preserve
    // that migration behavior without manufacturing an authoring undo operation.
    internal void RecoverLooseTilesheetSource(string tilesheet, string path)
    {
        Definition.TilesheetSources.Add(AnimationTilesheetSourceDefinition.Loose(tilesheet, MakeReferencePath(path, BaseDirectory)));
        RememberReferenceOrigins();
        History.MarkUntracked();
    }

    private void AddMembers(IReadOnlyList<AnimationDefinition> members, string description)
    {
        var selected = Definition;
        History.Execute(description, () =>
        {
            foreach (var member in members) _definitions.Add(member);
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

    /// <summary>Records changed scalar or value-type properties before notifying the editor.</summary>
    public bool Edit(object target, string description, Action change)
    {
        if (!_definitions.Any(member => ReferenceEquals(member, target) || member.Frames.Any(frame => ReferenceEquals(frame, target))))
            throw new ArgumentException("The object does not belong to this document.", nameof(target));
        return History.Edit(description, target, change);
    }

    /// <summary>Releases retained history and document listeners.</summary>
    public void Dispose()
    {
        History.Changed -= HistoryChanged;
        History.Dispose();
        Changed = null;
    }

    private void RememberReferenceOrigins()
    {
        foreach (var member in _definitions)
        {
            foreach (var source in member.TilesheetSources) _origins.Remember(source, BaseDirectory, PackedLocation?.Path);
        }
    }

    private void RestoreReferenceOrigins()
    {
        foreach (var member in _definitions)
        {
            foreach (var source in member.TilesheetSources)
            {
                _origins.Restore(source, BaseDirectory, PackedLocation?.Path, source.AssetEntryName,
                    () => source.GtsPath, value => source.GtsPath = value,
                    () => source.AssetsFilePath, value => source.AssetsFilePath = value);
            }
            if (FilePath is not null && !string.Equals(member.Source.GaniFilePath, FilePath, StringComparison.OrdinalIgnoreCase))
                member.Source = AnimationDefinitionSource.LooseDefinitionFile(FilePath);
        }
    }

    /// <summary>Adds the same frame instance as one reversible operation.</summary>
    public void AddFrame(AnimationFrameDefinition frame) => History.Insert("Add animation frame", Definition.Frames, Definition.Frames.Count, frame);

    /// <summary>Removes a frame and restores its identity and index on undo.</summary>
    public bool RemoveFrame(AnimationFrameDefinition frame) => History.Remove("Remove animation frame", Definition.Frames, frame);

    /// <summary>Moves a frame by an offset; boundary moves do not create history.</summary>
    public bool MoveFrame(int index, int offset)
    {
        var frames = Definition.Frames;
        int target = index + offset;
        if (index < 0 || index >= frames.Count || target < 0 || target >= frames.Count || index == target) return false;
        var frame = frames[index];
        History.Execute("Move animation frame", () => { frames.RemoveAt(index); frames.Insert(target, frame); },
            () => { frames.RemoveAt(target); frames.Insert(index, frame); });
        return true;
    }
}
