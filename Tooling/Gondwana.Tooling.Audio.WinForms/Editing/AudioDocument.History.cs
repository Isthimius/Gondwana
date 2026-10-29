using Gondwana.Audio.GSND;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Audio.Editing;

public sealed partial class AudioDocument : IDisposable
{
    private readonly ReferenceOrigins _origins = new();

    private void AddMembers(IReadOnlyList<AudioDefinition> members, string description)
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
        if (!_definitions.Any(member => ReferenceEquals(member, target)))
            throw new ArgumentException("Use EditResource for an owned audio resource.", nameof(target));
        return History.Edit(description, target, change);
    }

    /// <summary>Captures an audio resource edit, preserving path interpretation across later Save As operations.</summary>
    public void EditResource(AudioResourceDefinition resource, string description, Action change)
    {
        if (!_definitions.Any(member => member.Resources.Contains(resource))) throw new ArgumentException("Resource does not belong to this document.", nameof(resource));
        using var transaction = History.BeginTransaction(description);
        string directory = BaseDirectory;
        string? archive = PackedLocation?.Path;
        var before = new ModelSnapshot(resource);
        try { change(); }
        catch { before.Restore(); throw; }
        before.Record(History, description, (target, property, value) =>
        {
            _origins.Remember(target, BaseDirectory, PackedLocation?.Path);
            if (resource.SourceKind == AudioResourceSourceKind.Uri) return value;
            if (property == nameof(resource.AssetsFilePath) && resource.SourceKind == AudioResourceSourceKind.PackedAsset &&
                value is null or "" && archive is not null && PackedLocation?.Path != archive) value = archive;
            if (property is nameof(resource.FilePath) or nameof(resource.AssetsFilePath) && value is string path &&
                !string.IsNullOrWhiteSpace(path) && !string.Equals(directory, BaseDirectory, StringComparison.OrdinalIgnoreCase))
                return Path.GetRelativePath(BaseDirectory, Path.GetFullPath(path, directory));
            return value;
        });
        transaction.Commit();
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
            foreach (var source in member.Resources) _origins.Remember(source, BaseDirectory, PackedLocation?.Path);
        }
    }

    private void RestoreReferenceOrigins()
    {
        foreach (var member in _definitions)
        {
            foreach (var source in member.Resources)
            {
                if (source.SourceKind == AudioResourceSourceKind.Uri) continue;
                _origins.Restore(source, BaseDirectory, PackedLocation?.Path, source.AssetEntryName,
                    () => source.FilePath, value => source.FilePath = value,
                    () => source.AssetsFilePath, value => source.AssetsFilePath = value);
            }
            if (FilePath is not null && !string.Equals(member.Source.GsndFilePath, FilePath, StringComparison.OrdinalIgnoreCase))
                member.Source = AudioDefinitionSource.LooseDefinitionFile(FilePath);
        }
    }
}
