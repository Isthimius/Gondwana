using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;

namespace Gondwana.Tooling.SceneViewer.WinForms;

/// <summary>A non-recursive, deterministic index of the viewed scene's neighboring definitions.</summary>
internal sealed class ViewerContentCatalog
{
    internal sealed record Source(string Path, string? Entry = null)
    {
        internal bool Packed => Entry is not null;
        public override string ToString() => Packed ? $"{Path} :: {Entry}" : Path;
    }

    internal sealed record SpriteDocument(Source Source, SpriteDefinition Definition);
    internal sealed record SelectedSprite(SpriteDocument Document, SpriteInstanceDefinition Sprite);

    private readonly Dictionary<string, List<Source>> _sheets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Source>> _animations = new(StringComparer.Ordinal);
    private readonly List<SpriteDocument> _sprites = [];

    internal ViewerContentCatalog(string directory, Func<string, AssetsFile> archive)
    {
        foreach (var path in Directory.EnumerateFiles(directory).OrderBy(path => path, StringComparer.Ordinal))
        {
            var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".gaf")
            {
                try
                {
                    var file = archive(path);
                    foreach (var entry in file.GetAllEntries().OrderBy(entry => entry.AssetName, StringComparer.Ordinal).ThenBy(entry => entry.AssetType))
                    {
                        if (entry.AssetType is AssetTypes.TilesheetDefinition or AssetTypes.AnimationDefinition or AssetTypes.SpriteDefinition)
                            Add(new(path, entry.AssetName), entry.AssetType, file);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException($"Could not catalog GAF '{path}': {ex.Message}", ex);
                }
            }
            else if (extension is ".gts" or ".gani" or ".gspr")
            {
                Add(new(path), extension == ".gts" ? AssetTypes.TilesheetDefinition :
                    extension == ".gani" ? AssetTypes.AnimationDefinition : AssetTypes.SpriteDefinition, null);
            }
        }
    }

    private void Add(Source source, AssetTypes type, AssetsFile? archive)
    {
        try
        {
            if (type == AssetTypes.TilesheetDefinition)
            {
                using var stream = archive?.Get(type, source.Entry!);
                var definition = archive is null ? TilesheetDefinitionSerializer.Load(source.Path) :
                    TilesheetDefinitionSerializer.Load(stream ?? throw new InvalidDataException("Missing GTS entry."));
                AddIdentity(_sheets, definition.Name, source);
            }
            else if (type == AssetTypes.AnimationDefinition)
            {
                var definition = archive is null ? AnimationDefinitionSerializer.Load(source.Path) :
                    AnimationDefinitionSerializer.Load(archive, source.Entry!);
                AddIdentity(_animations, definition.Key, source);
            }
            else
            {
                var definition = archive is null ? SpriteDefinitionSerializer.Load(source.Path) :
                    SpriteDefinitionSerializer.Load(archive, source.Entry!);
                var errors = SpriteDefinitionValidator.Validate(definition);
                if (errors.Count > 0)
                    throw new InvalidDataException(string.Join("\n", errors));
                _sprites.Add(new(source, definition));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Could not catalog {type} '{source}': {ex.Message}", ex);
        }
    }

    private static void AddIdentity(Dictionary<string, List<Source>> index, string key, Source source)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidDataException("Definition logical identity is empty.");
        if (!index.TryGetValue(key, out var candidates))
            index.Add(key, candidates = []);
        candidates.Add(source);
    }

    internal Source? Tilesheet(string name) => Select(_sheets, name, "GTS");
    internal Source? Animation(string key) => Select(_animations, key, "GANI");

    private static Source? Select(Dictionary<string, List<Source>> index, string key, string format)
    {
        if (!index.TryGetValue(key, out var candidates)) return null;
        var loose = candidates.Where(source => !source.Packed).ToList();
        var selected = loose.Count > 0 ? loose : candidates;
        if (selected.Count != 1)
            throw new InvalidDataException($"Multiple {(loose.Count > 0 ? "loose" : "packed")} {format} definitions claim '{key}':\n" +
                string.Join("\n", selected));
        return selected[0];
    }

    internal IReadOnlyList<SelectedSprite> Sprites(string sceneId)
    {
        var candidates = _sprites.SelectMany(document => document.Definition.Sprites
            .Where(sprite => string.Equals(sprite.SceneId, sceneId, StringComparison.Ordinal))
            .Select(sprite => new SelectedSprite(document, sprite))).ToList();
        var loose = candidates.Where(candidate => !candidate.Document.Source.Packed).ToList();
        var selected = loose.Concat(candidates.Where(candidate => candidate.Document.Source.Packed &&
            !loose.Any(other => SameIdentity(candidate.Sprite, other.Sprite)))).ToList();
        for (int i = 0; i < selected.Count; i++)
        {
            var conflicts = selected.Where(other => SameIdentity(selected[i].Sprite, other.Sprite)).ToList();
            if (conflicts.Count > 1)
                throw new InvalidDataException($"Sprite '{selected[i].Sprite.Nickname}' / {selected[i].Sprite.Id} is defined by multiple " +
                    $"{(selected[i].Document.Source.Packed ? "packed" : "loose")} GSPR sources:\n" +
                    string.Join("\n", conflicts.Select(candidate => candidate.Document.Source)));
        }
        return selected;
    }

    private static bool SameIdentity(SpriteInstanceDefinition left, SpriteInstanceDefinition right) =>
        (left.Id != Guid.Empty && left.Id == right.Id) ||
        (!string.IsNullOrWhiteSpace(left.Nickname) && string.Equals(left.Nickname, right.Nickname, StringComparison.Ordinal));
}
