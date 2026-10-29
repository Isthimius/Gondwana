using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Sprites.Editing;

public sealed partial class SpriteDocument : IDisposable
{
    private readonly ReferenceOrigins _origins = new();

    private void AddMembers(IReadOnlyList<SpriteDefinition> members, string description)
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
        if (!_definitions.Any(member => ReferenceEquals(member, target) || member.Sprites.Any(sprite => ReferenceEquals(sprite, target))))
            throw new ArgumentException("The object does not belong to this document.", nameof(target));
        return History.Edit(description, target, change);
    }

    /// <summary>Assigns a frame, suppressing assignments of the same logical frame.</summary>
    public bool AssignFrame(SpriteInstanceDefinition sprite, SpriteFrameDefinition frame)
    {
        if (sprite.Frame is { } previous && previous.Tilesheet == frame.Tilesheet && previous.RegionName == frame.RegionName &&
            previous.XTile == frame.XTile && previous.YTile == frame.YTile) return false;
        return Edit(sprite, "Assign sprite frame", () => sprite.Frame = frame);
    }

    /// <summary>Removes matching tilesheet references, retaining their exact source metadata and positions.</summary>
    public int RemoveTilesheetSource(string name) => History.RemoveWhere("Remove tilesheet source", Definition.TilesheetSources, source => source.Tilesheet == name);
    /// <summary>Removes matching animation references using the existing case-insensitive key convention.</summary>
    public int RemoveAnimationSource(string key) => History.RemoveWhere("Remove animation source", Definition.AnimationSources, source => string.Equals(source.AnimationKey, key, StringComparison.OrdinalIgnoreCase));
    /// <summary>Removes matching scene references without modifying any scene document.</summary>
    public int RemoveSceneSource(string id) => History.RemoveWhere("Remove scene source", Definition.SceneSources, source => source.SceneId == id);

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
            foreach (var source in member.AnimationSources) _origins.Remember(source, BaseDirectory, PackedLocation?.Path);
            foreach (var source in member.SceneSources) _origins.Remember(source, BaseDirectory, PackedLocation?.Path);
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
            foreach (var source in member.AnimationSources)
            {
                _origins.Restore(source, BaseDirectory, PackedLocation?.Path, source.AssetEntryName,
                    () => source.GaniPath, value => source.GaniPath = value,
                    () => source.AssetsFilePath, value => source.AssetsFilePath = value);
            }
            foreach (var source in member.SceneSources)
            {
                _origins.Restore(source, BaseDirectory, PackedLocation?.Path, source.AssetEntryName,
                    () => source.GscnPath, value => source.GscnPath = value,
                    () => source.AssetsFilePath, value => source.AssetsFilePath = value);
            }
            if (FilePath is not null && !string.Equals(member.Source.GsprFilePath, FilePath, StringComparison.OrdinalIgnoreCase))
                member.Source = SpriteDefinitionSource.LooseDefinitionFile(FilePath);
        }
    }
}
