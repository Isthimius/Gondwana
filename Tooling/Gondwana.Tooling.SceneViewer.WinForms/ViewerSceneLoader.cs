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
    private readonly Dictionary<string, AnimationDefinition> _animations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Cycle> _cycles = new(StringComparer.Ordinal);
    private ViewerContentCatalog _catalog = null!;

    internal Scene Load(string path)
    {
        var definition = SceneDefinitionSerializer.Load(path);
        Validate(SceneDefinitionValidator.Validate(definition), "GSCN");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        _catalog = new ViewerContentCatalog(directory, Archive);

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
                bool packed = selected?.Packed ?? source.Kind == SceneAnimationSourceKind.PackedDefinitionFile;
                string location = selected?.Path ?? Resolve(packed ? source.AssetsFilePath : source.GaniPath, directory);
                var entry = selected?.Entry ?? source.AssetEntryName;
                description = selected?.ToString() ?? description;
                var animation = packed
                    ? AnimationDefinitionSerializer.Load(Archive(location), entry!)
                    : AnimationDefinitionSerializer.Load(location);
                Validate(AnimationDefinitionValidator.Validate(animation), "GANI");
                if (!string.Equals(animation.Key, source.AnimationKey, StringComparison.Ordinal))
                    throw new InvalidDataException($"Expected animation key '{source.AnimationKey}', found '{animation.Key}'.");
                _animations.Add(animation.Key, animation);
                foreach (var sheet in animation.TilesheetSources)
                {
                    LoadTilesheet(sheet.Tilesheet, Path.GetDirectoryName(location)!,
                        sheet.Kind == AnimationTilesheetSourceKind.PackedDefinitionFile,
                        sheet.GtsPath, sheet.AssetsFilePath, sheet.AssetEntryName);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Animation source '{source.AnimationKey}' could not be loaded: {description}\n{ex.Message}", ex);
            }
        }

        RegisterCycles();

        Scene? scene = null;
        try
        {
            scene = SceneDefinitionSerializer.ToScene(definition);
            var selected = _catalog.Sprites(scene.ID);
            foreach (var candidate in selected)
            {
                if (candidate.Sprite.Frame is not { } frame) continue;
                var document = candidate.Document;
                var source = document.Definition.TilesheetSources.SingleOrDefault(source => source.Tilesheet == frame.Tilesheet);
                // An already selected sheet can satisfy a frame without its own authoring metadata.
                if (source is null && _tilesheets.ContainsKey(frame.Tilesheet)) continue;
                LoadTilesheet(frame.Tilesheet, Path.GetDirectoryName(document.Source.Path)!,
                    source?.Kind == SpriteTilesheetSourceKind.PackedDefinitionFile,
                    source?.GtsPath, source?.AssetsFilePath, source?.AssetEntryName);
            }
            SpriteDefinitionSerializer.ToSprites(new SpriteDefinition { Sprites = selected.Select(candidate => candidate.Sprite).ToList() });
            return scene;
        }
        catch (Exception ex)
        {
            scene?.Dispose();
            throw new InvalidDataException($"Scene '{path}' could not be materialized. Check its GTS/GANI/GSPR sources.\n{ex.Message}", ex);
        }
    }

    private void RegisterCycles()
    {
        foreach (var definition in _animations.Values)
        {
            if (!string.IsNullOrWhiteSpace(definition.NextCycleKey) && !_animations.ContainsKey(definition.NextCycleKey))
                throw new InvalidDataException($"Animation '{definition.Key}' references '{definition.NextCycleKey}', which has no explicit GANI source in the scene.");
        }
        foreach (var definition in _animations.Values)
        {
            // Register through the public serializer before wiring cross-cycle links.
            // This also supports mutual NextCycleKey references without advancing frames.
            var next = definition.NextCycleKey;
            try
            {
                definition.NextCycleKey = null;
                _cycles.Add(definition.Key, AnimationDefinitionSerializer.ToCycle(definition));
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Animation '{definition.Key}' could not be registered: {ex.Message}", ex);
            }
            finally
            {
                definition.NextCycleKey = next;
            }
        }
        foreach (var definition in _animations.Values)
            if (!string.IsNullOrWhiteSpace(definition.NextCycleKey))
                _cycles[definition.Key].NextCycle = _cycles[definition.NextCycleKey];
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
            var location = selected?.Path ?? Resolve(packed ? archivePath : loosePath, directory);
            var identity = (location, packed ? entry : null);
            if (_tilesheets.TryGetValue(name, out var previous))
            {
                if (!string.Equals(previous.Path, location, StringComparison.OrdinalIgnoreCase) || previous.Entry != identity.Item2)
                    throw new InvalidDataException($"Conflicting sources for tilesheet '{name}'.");
                return;
            }
            TilesheetDefinition definition;
            if (packed)
            {
                using var stream = Archive(location).Get(AssetTypes.TilesheetDefinition, entry!)
                    ?? throw new FileNotFoundException($"GTS entry '{entry}' was not found in '{location}'.");
                definition = TilesheetDefinitionSerializer.Load(stream);
            }
            else
            {
                definition = TilesheetDefinitionSerializer.Load(location);
            }
            Validate(TilesheetDefinitionValidator.Validate(definition), "GTS");
            if (!string.Equals(definition.Name, name, StringComparison.Ordinal))
                throw new InvalidDataException($"Expected tilesheet '{name}', found '{definition.Name}'.");
            var sheet = packed
                ? TilesheetRegistry.Instance.LoadFromDefinitionAsset(Archive(location), entry!)
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
