using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Scenes.Editing;

public sealed partial class SceneDocument : IDisposable
{
    private readonly ReferenceOrigins _origins = new();

    private void AddMembers(IReadOnlyList<SceneDefinition> members, string description)
    {
        EditingContextChanging?.Invoke();
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
        EditingContextChanging?.Invoke();
        EditingContextChanging = null;
        History.Changed -= HistoryChanged;
        History.Dispose();
        Changed = null;
    }

    /// <summary>Commits changes from a focused collection editor, retaining path interpretation across Save As.</summary>
    public void RecordCollectionEdit(ModelSnapshot snapshot, string description)
    {
        string directory = BaseDirectory;
        string? archive = PackedLocation?.Path;
        snapshot.Record(History, description, (target, property, value) =>
        {
            // Adapted values are interpreted against the current save location, even
            // when the instance was removed and missed Save As's live-source rebasing.
            if (target is SceneTilesheetSourceDefinition or SceneAnimationSourceDefinition)
                _origins.Remember(target, BaseDirectory, PackedLocation?.Path);
            bool packed = target is SceneTilesheetSourceDefinition { Kind: SceneTilesheetSourceKind.PackedDefinitionFile } or
                SceneAnimationSourceDefinition { Kind: SceneAnimationSourceKind.PackedDefinitionFile };
            if (property == "AssetsFilePath" && packed && value is null or "" && archive is not null && PackedLocation?.Path != archive)
                value = archive;
            if (target is SceneTilesheetSourceDefinition or SceneAnimationSourceDefinition &&
                property is "GtsPath" or "GaniPath" or "AssetsFilePath" && value is string path && !string.IsNullOrWhiteSpace(path) &&
                !string.Equals(directory, BaseDirectory, StringComparison.OrdinalIgnoreCase))
                return Path.GetRelativePath(BaseDirectory, Path.GetFullPath(path, directory));
            return value;
        });
    }

    private bool Owns(object target) => _definitions.Any(d => ReferenceEquals(d, target) ||
        d.Layers.Any(l => ReferenceEquals(l, target) || l.Tiles.Any(t => ReferenceEquals(t, target))));

    /// <summary>Edits one sparse cell. Undo restores absence when the operation created the cell.</summary>
    public bool EditTile(SceneLayerDefinition layer, int x, int y, string description, Action<SceneLayerTileDefinition> change)
    {
        if (!Owns(layer)) throw new ArgumentException("Layer does not belong to this document.", nameof(layer));
        if (x < 0 || y < 0 || x >= layer.Columns || y >= layer.Rows) throw new ArgumentOutOfRangeException(nameof(x));
        var existing = FindTile(layer, x, y);
        var tile = existing ?? new SceneLayerTileDefinition { X = x, Y = y };
        using var transaction = History.BeginTransaction(description);
        bool changed = History.Edit(description, tile, () =>
        {
            var previousFrame = tile.Frame;
            change(tile);
            if (previousFrame is not null && tile.Frame is { } frame && previousFrame.Tilesheet == frame.Tilesheet &&
                previousFrame.RegionName == frame.RegionName && previousFrame.XTile == frame.XTile && previousFrame.YTile == frame.YTile)
                tile.Frame = previousFrame;
        });
        if (changed && existing is null) History.Insert(description, layer.Tiles, layer.Tiles.Count, tile);
        transaction.Commit();
        return changed;
    }

    private void RememberReferenceOrigins()
    {
        foreach (var member in _definitions)
        {
            foreach (var source in member.TilesheetSources) _origins.Remember(source, BaseDirectory, PackedLocation?.Path);
            foreach (var source in member.AnimationSources) _origins.Remember(source, BaseDirectory, PackedLocation?.Path);
        }
    }

    private void RestoreReferenceOrigins()
    {
        foreach (var member in _definitions)
        {
            foreach (var source in member.TilesheetSources)
                _origins.Restore(source, BaseDirectory, PackedLocation?.Path, source.AssetEntryName,
                    () => source.GtsPath, value => source.GtsPath = value,
                    () => source.AssetsFilePath, value => source.AssetsFilePath = value);
            foreach (var source in member.AnimationSources)
                _origins.Restore(source, BaseDirectory, PackedLocation?.Path, source.AssetEntryName,
                    () => source.GaniPath, value => source.GaniPath = value,
                    () => source.AssetsFilePath, value => source.AssetsFilePath = value);
            if (FilePath is not null && !string.Equals(member.Source.GscnFilePath, FilePath, StringComparison.OrdinalIgnoreCase))
                member.Source = SceneDefinitionSource.LooseDefinitionFile(FilePath);
        }
    }
}
