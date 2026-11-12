using Gondwana.Assets;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.SceneViewer.WinForms;

/// <summary>Resolves local and authored dependencies, then delegates materialization to the runtime.</summary>
internal sealed class ViewerSceneLoader
{
    private readonly Dictionary<string, (string Path, string? Entry)> _tilesheets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AssetsFile> _archives = new(StringComparer.OrdinalIgnoreCase);
    private sealed record LoadedAnimation(ViewerContentCatalog.Source Source, AnimationDefinition Definition);

    private readonly Dictionary<string, LoadedAnimation> _animations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Cycle> _cycles = new(StringComparer.OrdinalIgnoreCase);
    private ViewerContentCatalog _catalog = null!;

    /// <summary>Loads a scene member and resolves its explicitly identified dependencies.</summary>
    /// <param name="path">The physical scene document path.</param>
    /// <param name="sceneId">The logical scene identity, or null for singleton compatibility.</param>
    /// <param name="contentRoot">Optional original directory for neighboring GTS/GANI/GSPR discovery.</param>
    /// <returns>The materialized scene.</returns>
    internal Scene Load(string path, string? sceneId = null, string? contentRoot = null)
    {
        var definition = SceneDefinitionSerializer.Load(path, sceneId);
        Validate(SceneDefinitionValidator.Validate(definition), "GSCN");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        _catalog = new ViewerContentCatalog(contentRoot ?? directory, Archive);

        foreach (var source in definition.TilesheetSources)
        {
            LoadTilesheet(source.Tilesheet, directory,
                source.Kind == SceneTilesheetSourceKind.PackedDefinitionFile,
                source.GtsPath, source.AssetsFilePath, source.AssetEntryName);
        }

        foreach (var source in definition.AnimationSources)
        {
            var description = source.GaniPath ?? $"{source.AssetsFilePath} :: {source.AssetEntryName}";
            try
            {
                var selected = _catalog.Animation(source.AnimationKey);
                if (selected is not null) description = selected.ToString();
                bool packed = source.Kind == SceneAnimationSourceKind.PackedDefinitionFile;
                LoadAnimation(source.AnimationKey, selected ??
                    new(Resolve(packed ? source.AssetsFilePath : source.GaniPath, directory), packed ? source.AssetEntryName : null));
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Animation source '{source.AnimationKey}' could not be loaded: {description}\n{ex.Message}", ex);
            }
        }

        Scene? scene = null;
        try
        {
            var selected = _catalog.Sprites(definition.ID);
            foreach (var candidate in selected)
            {
                if (candidate.Sprite.Frame is not { } frame) continue;
                var document = candidate.Document;
                try
                {
                    LoadSpriteTilesheet(document, frame.Tilesheet);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException($"GSPR source '{document.Source}', sprite '{candidate.Sprite.Nickname}' / {candidate.Sprite.Id}: {ex.Message}", ex);
                }
            }
            foreach (var candidate in selected)
                if (!string.IsNullOrWhiteSpace(candidate.Sprite.AnimationKey))
                    LoadSpriteAnimation(candidate.Document, candidate.Sprite.AnimationKey);
            RegisterCycles();
            scene = SceneDefinitionSerializer.ToScene(definition);

            try
            {
                SpriteDefinitionSerializer.ToSprites(new SpriteDefinition { Sprites = selected.Select(candidate => candidate.Sprite).ToList() });
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"GSPR materialization failed for sources:\n" +
                    string.Join("\n", selected.Select(candidate => candidate.Document.Source).Distinct()) + $"\n{ex.Message}", ex);
            }
            return scene;
        }
        catch (Exception ex)
        {
            scene?.Dispose();
            throw new InvalidDataException($"Scene '{path}' could not be materialized. Check its GTS/GANI/GSPR sources.\n{ex.Message}", ex);
        }
    }

    private void LoadSpriteAnimation(ViewerContentCatalog.SpriteDocument document, string key)
    {
        // Every sprite document may provide its own fallback sources, including for
        // cycles already loaded through the scene or another sprite document.
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? current = key;
        while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
        {
            var authored = document.Definition.AnimationSources.SingleOrDefault(source =>
                string.Equals(source.AnimationKey, current, StringComparison.OrdinalIgnoreCase));
            var selected = _catalog.Animation(current);
            if (selected is null && authored is not null)
            {
                bool packed = authored.Kind == SpriteAnimationSourceKind.PackedDefinitionFile;
                selected = new(Resolve(packed ? authored.AssetsFilePath : authored.GaniPath,
                    Path.GetDirectoryName(document.Source.Path)!), packed ? authored.AssetEntryName : null);
            }
            if (selected is null && _animations.TryGetValue(current, out var loaded))
                selected = loaded.Source;
            if (selected is null)
                throw new InvalidDataException($"GSPR source '{document.Source}' has no GANI source for '{current}'.");

            var animation = LoadAnimation(current, selected);
            foreach (string name in animation.Frames.Select(frame => frame.Tilesheet).Distinct(StringComparer.Ordinal))
                if (!animation.TilesheetSources.Any(source => source.Tilesheet == name))
                    LoadSpriteTilesheet(document, name);
            current = animation.NextCycleKey;
        }
    }

    private AnimationDefinition LoadAnimation(string key, ViewerContentCatalog.Source source)
    {
        if (_animations.TryGetValue(key, out var previous))
        {
            if (!string.Equals(previous.Source.Path, source.Path, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(previous.Source.Entry, source.Entry, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Conflicting sources for animation '{key}':\n{previous.Source}\n{source}");
            return previous.Definition;
        }

        var animation = source.Packed
            ? AnimationDefinitionSerializer.Load(Archive(source.Path), source.Entry!, key)
            : AnimationDefinitionSerializer.Load(source.Path, key);
        Validate(AnimationDefinitionValidator.Validate(animation), "GANI");
        if (!string.Equals(animation.Key, key, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Expected animation key '{key}', found '{animation.Key}'.");

        foreach (var sheet in animation.TilesheetSources)
            LoadTilesheet(sheet.Tilesheet, Path.GetDirectoryName(source.Path)!,
                sheet.Kind == AnimationTilesheetSourceKind.PackedDefinitionFile,
                sheet.GtsPath, sheet.AssetsFilePath, sheet.AssetEntryName);
        _animations.Add(animation.Key, new(source, animation));
        return animation;
    }

    private void LoadSpriteTilesheet(ViewerContentCatalog.SpriteDocument document, string name)
    {
        var source = document.Definition.TilesheetSources.SingleOrDefault(source => source.Tilesheet == name);
        if (source is null && _tilesheets.ContainsKey(name)) return;
        LoadTilesheet(name, Path.GetDirectoryName(document.Source.Path)!,
            source?.Kind == SpriteTilesheetSourceKind.PackedDefinitionFile,
            source?.GtsPath, source?.AssetsFilePath, source?.AssetEntryName);
    }

    private void RegisterCycles()
    {
        var definitions = _animations.Values.Select(loaded => loaded.Definition).ToList();
        foreach (var definition in definitions)
        {
            if (!string.IsNullOrWhiteSpace(definition.NextCycleKey) && !_animations.ContainsKey(definition.NextCycleKey))
                throw new InvalidDataException($"Animation '{definition.Key}' references '{definition.NextCycleKey}', which has no explicit GANI source in the scene.");
        }
        foreach (var cycle in AnimationDefinitionSerializer.ToAllCycles(
            new AnimationDefinitionDocument { Definitions = definitions }))
            _cycles.Add(cycle.CycleKey, cycle);
    }

    private void LoadTilesheet(
        string name,
        string directory,
        bool packed,
        string? loosePath,
        string? archivePath,
        string? entry)
    {
        var description = loosePath ?? $"{archivePath} :: {entry}";
        try
        {
            var selected = _catalog.Tilesheet(name);
            if (selected is not null)
            {
                packed = selected.Packed;
                entry = selected.Entry;
                description = selected.ToString();
            }
            if (selected is null && string.IsNullOrWhiteSpace(packed ? archivePath : loosePath))
                throw new InvalidDataException($"No local GTS or explicit authored source exists for tilesheet '{name}'.");
            var location = selected?.Path ?? Resolve(packed ? archivePath : loosePath, directory);
            var identity = (location, packed ? entry : null);
            if (_tilesheets.TryGetValue(name, out var previous))
            {
                if (!string.Equals(previous.Path, location, StringComparison.OrdinalIgnoreCase) || previous.Entry != identity.Item2)
                    throw new InvalidDataException($"Conflicting sources for tilesheet '{name}':\n" +
                        $"{new ViewerContentCatalog.Source(previous.Path, previous.Entry)}\n{new ViewerContentCatalog.Source(location, identity.Item2)}");
                return;
            }
            TilesheetDefinition definition;
            if (packed)
            {
                using var stream = Archive(location).Get(AssetTypes.TilesheetDefinition, entry!)
                    ?? throw new FileNotFoundException($"GTS entry '{entry}' was not found in '{location}'.");
                definition = TilesheetDefinitionSerializer.Load(Archive(location), entry!, name);
            }
            else
            {
                definition = TilesheetDefinitionSerializer.Load(location, name);
            }
            Validate(TilesheetDefinitionValidator.Validate(definition), "GTS");
            if (!string.Equals(definition.Name, name, StringComparison.Ordinal))
                throw new InvalidDataException($"Expected tilesheet '{name}', found '{definition.Name}'.");
            var sheet = packed
                ? TilesheetRegistry.Instance.LoadFromDefinitionAsset(Archive(location), entry!, name)
                : TilesheetRegistry.Instance.LoadFromDefinition(definition, Path.GetDirectoryName(location));
            Validate(TilesheetDefinitionValidator.Validate(definition, sheet.SkBitmap.Width, sheet.SkBitmap.Height), "GTS");
            _tilesheets.Add(name, identity);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Tilesheet source '{name}' could not be loaded: {description}\n{ex.Message}", ex);
        }
    }

    private AssetsFile Archive(string path)
    {
        if (_archives.TryGetValue(path, out var archive))
            return archive;
        // Never use LoadOrCreate on a missing authored dependency.
        if (!File.Exists(path))
            throw new FileNotFoundException($"Assets file not found: {path}", path);
        archive = AssetsFile.AllAssetsFiles.FirstOrDefault(file =>
            string.Equals(file.SourcePath, path, StringComparison.OrdinalIgnoreCase))
            ?? AssetsFile.LoadOrCreate(path);
        _archives.Add(path, archive);
        return archive;
    }

    private static string Resolve(string? path, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path, directory);
    }

    private static void Validate(IReadOnlyList<string> errors, string format)
    {
        if (errors.Count > 0)
            throw new InvalidDataException($"{format} validation failed:\n" + string.Join("\n", errors));
    }
}
