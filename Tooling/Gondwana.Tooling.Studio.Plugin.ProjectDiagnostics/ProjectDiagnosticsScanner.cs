using Gondwana.Assets;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics;

/// <summary>Inspects authoring files through public definition APIs without materializing runtime objects.</summary>
public sealed class ProjectDiagnosticsScanner
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".gaf", ".gts", ".gani", ".gsnd", ".gscn", ".gspr" };
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
        { "bin", "obj", ".git", ".vs", "node_modules" };

    /// <summary>Scans synchronously on the caller's thread. The plugin runs this on a worker.</summary>
    public ProjectScanResult Scan(string projectPath, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(projectPath);
        var definitions = new List<DefinitionResult>();
        var problems = new List<ProjectProblem>();
        var pending = new Stack<string>();
        var packages = new Dictionary<string, AssetsFile>(StringComparer.OrdinalIgnoreCase);
        pending.Push(root);
        try
        {
            while (pending.TryPop(out var directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var attributes = File.GetAttributes(entry);
                            // Do not follow links/junctions outside the project or into cycles.
                            if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                            if ((attributes & FileAttributes.Directory) != 0)
                            {
                                if (!IgnoredDirectories.Contains(Path.GetFileName(entry))) pending.Push(entry);
                            }
                            else if (Extensions.Contains(Path.GetExtension(entry)))
                                definitions.Add(Inspect(root, entry, problems, packages, cancellationToken));
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            problems.Add(new(Path.GetRelativePath(root, entry), "Discovery", ex.Message));
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    problems.Add(new(Path.GetRelativePath(root, directory), "Discovery", ex.Message));
                }
            }
            return new(definitions.OrderBy(d => d.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray(), problems.ToArray());
        }
        finally
        {
            foreach (var package in packages.Values)
            {
                package.Dispose();
            }
        }
    }

    private static DefinitionResult Inspect(string root, string path, List<ProjectProblem> problems, Dictionary<string, AssetsFile> packages, CancellationToken token)
    {
        var relative = Path.GetRelativePath(root, path);
        var references = new List<DefinitionReference>();
        var format = Path.GetExtension(path).ToLowerInvariant();
        var loaded = false;
        try
        {
            switch (format)
            {
                case ".gaf":
                    // Structure/key validation avoids loading every media payload into memory.
                    AssetsFile.Validate(path, testData: false);
                    loaded = true;
                    break;
                case ".gts":
                    var tilesheet = TilesheetDefinitionSerializer.Load(path);
                    loaded = true;
                    Validate(TilesheetDefinitionValidator.Validate(tilesheet));
                    var image = tilesheet.Image;
                    if (image is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(image.FilePath)) Reference("Image.FilePath", image.FilePath);
                        if (!string.IsNullOrWhiteSpace(image.AssetsFilePath) || !string.IsNullOrWhiteSpace(image.AssetEntryName))
                            Reference("Image.AssetsFilePath", image.AssetsFilePath, image.AssetEntryName, AssetTypes.Image);
                    }
                    break;
                case ".gani":
                    var animation = AnimationDefinitionSerializer.Load(path);
                    loaded = true;
                    Validate(AnimationDefinitionValidator.Validate(animation));
                    for (int i = 0; i < animation.TilesheetSources.Count; i++)
                    {
                        var source = animation.TilesheetSources[i];
                        Source($"TilesheetSources[{i}]", (int)source.Kind, source.GtsPath, source.AssetsFilePath, source.AssetEntryName, AssetTypes.TilesheetDefinition, "GtsPath");
                    }
                    break;
                case ".gscn":
                    var scene = SceneDefinitionSerializer.Load(path);
                    loaded = true;
                    Validate(SceneDefinitionValidator.Validate(scene));
                    for (int i = 0; i < scene.TilesheetSources.Count; i++)
                    {
                        var source = scene.TilesheetSources[i];
                        Source($"TilesheetSources[{i}]", (int)source.Kind, source.GtsPath, source.AssetsFilePath, source.AssetEntryName, AssetTypes.TilesheetDefinition, "GtsPath");
                    }
                    for (int i = 0; i < scene.AnimationSources.Count; i++)
                    {
                        var source = scene.AnimationSources[i];
                        Source($"AnimationSources[{i}]", (int)source.Kind, source.GaniPath, source.AssetsFilePath, source.AssetEntryName, AssetTypes.AnimationDefinition, "GaniPath");
                    }
                    break;
                case ".gspr":
                    var sprites = SpriteDefinitionSerializer.Load(path);
                    loaded = true;
                    Validate(SpriteDefinitionValidator.Validate(sprites));
                    for (int i = 0; i < sprites.TilesheetSources.Count; i++)
                    {
                        var source = sprites.TilesheetSources[i];
                        Source($"TilesheetSources[{i}]", (int)source.Kind, source.GtsPath, source.AssetsFilePath, source.AssetEntryName, AssetTypes.TilesheetDefinition, "GtsPath");
                    }
                    for (int i = 0; i < sprites.SceneSources.Count; i++)
                    {
                        var source = sprites.SceneSources[i];
                        Source($"SceneSources[{i}]", (int)source.Kind, source.GscnPath, source.AssetsFilePath, source.AssetEntryName, AssetTypes.SceneDefinition, "GscnPath");
                    }
                    break;
                case ".gsnd":
                    var audio = AudioDefinitionSerializer.Load(path);
                    loaded = true;
                    Validate(AudioDefinitionValidator.Validate(audio));
                    for (int i = 0; i < audio.Resources.Count; i++)
                    {
                        var resource = audio.Resources[i];
                        var property = $"Resources[{i}]";
                        switch (resource.SourceKind)
                        {
                            case AudioResourceSourceKind.LooseFile: Reference(property + ".FilePath", resource.FilePath); break;
                            case AudioResourceSourceKind.PackedAsset: Reference(property + ".AssetsFilePath", resource.AssetsFilePath, resource.AssetEntryName, AssetTypes.Audio); break;
                            case AudioResourceSourceKind.Uri:
                                // The public validator accepts both relative and absolute URIs.
                                // Their resolution belongs to the audio backend, not the project filesystem.
                                references.Add(new(property + ".SourceUri", resource.SourceUri ?? "", null, null));
                                break;
                            default: Unsupported(property, (int)resource.SourceKind); break;
                        }
                    }
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            problems.Add(new(relative, loaded ? "Inspection" : "Load", ex.GetBaseException().Message));
        }
        return new(relative, format.TrimStart('.').ToUpperInvariant(), loaded, references.ToArray());

        void Validate(IReadOnlyList<string> errors)
        {
            foreach (var error in errors) problems.Add(new(relative, "Validation", error));
        }

        void Unsupported(string property, int kind)
        {
            var reason = $"Unsupported source kind: {kind}.";
            references.Add(new(property + ".Kind", kind.ToString(), null, reason));
            problems.Add(new(relative, property + ".Kind", reason));
        }

        void Source(string property, int kind, string? loose, string? archive, string? entry, AssetTypes type, string looseProperty)
        {
            // Each current authoring source enum uses LooseDefinitionFile=0, PackedDefinitionFile=1.
            if (kind == 0) Reference(property + "." + looseProperty, loose);
            else if (kind == 1) Reference(property + ".AssetsFilePath", archive, entry, type);
            else Unsupported(property, kind);
        }

        void Reference(string property, string? value, string? entry = null, AssetTypes? assetType = null)
        {
            token.ThrowIfCancellationRequested();
            string? resolved = null;
            string? error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Reference path is empty.");
                resolved = Path.GetFullPath(value, Path.GetDirectoryName(path)!);
                if (!File.Exists(resolved)) throw new FileNotFoundException($"Referenced file not found: {resolved}");
                if (assetType is not null)
                {
                    if (string.IsNullOrWhiteSpace(entry)) throw new InvalidDataException("Packed reference AssetEntryName is empty.");
                    if (!packages.TryGetValue(resolved, out var package))
                    {
                        package = AssetsFile.LoadOrCreate(resolved, null, false, register: false);
                        packages.Add(resolved, package);
                    }
                    using var stream = package.Get(assetType.Value, entry);
                    if (stream is null) throw new FileNotFoundException($"Packed {assetType} entry not found: {entry}");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                error = ex.GetBaseException().Message;
                problems.Add(new(relative, property, error));
            }
            references.Add(new(property, assetType is null ? value ?? "" : $"{value} :: {entry}", resolved, error));
        }
    }
}
