using System.Text;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Importers;

/// <summary>Shared native serialization, validation and conflict analysis.</summary>
/// <param name="request">The request.</param>
public sealed class ImportPlan(ExternalImportRequest request)
{
    internal List<(ExternalImportArtifact Artifact, byte[] Content)> Outputs { get; } = [];
    /// <summary>
    /// Gets the dependencies.
    /// </summary>
    public List<string> Dependencies { get; } = [];
    /// <summary>
    /// Gets the diagnostics.
    /// </summary>
    public List<ExternalImportDiagnostic> Diagnostics { get; } = [];
    /// <summary>
    /// Gets the request.
    /// </summary>
    public ExternalImportRequest Request { get; } = request;

    /// <summary>
    /// Adds a diagnostic to the import plan.
    /// </summary>
    /// <param name="severity">The severity.</param>
    /// <param name="code">The code.</param>
    /// <param name="message">The message.</param>
    public void Report(ExternalImportSeverity severity, string code, string message) =>
        Diagnostics.Add(new(severity, code, message, Request.SourcePath));

    /// <summary>
    /// Adds the supplied entry to the import plan.
    /// </summary>
    /// <param name="filename">The filename.</param>
    /// <param name="definition">The persisted definition to process.</param>
    public void Add(string filename, TilesheetDefinition definition)
    {
        foreach (var error in TilesheetDefinitionValidator.Validate(definition)) Report(ExternalImportSeverity.Error, "native.validation", error);
        definition.Source = TilesheetDefinitionSource.Generated();
        Add(filename, "GTS", definition.Name, Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(definition)));
    }

    /// <summary>
    /// Adds the supplied entry to the import plan.
    /// </summary>
    /// <param name="filename">The filename.</param>
    /// <param name="definition">The persisted definition to process.</param>
    public void Add(string filename, AnimationDefinition definition)
    {
        foreach (var error in AnimationDefinitionValidator.Validate(definition)) Report(ExternalImportSeverity.Error, "native.validation", error);
        definition.Source = AnimationDefinitionSource.Generated();
        Add(filename, "GANI", definition.Key, Encoding.UTF8.GetBytes(AnimationDefinitionSerializer.ToJson(definition)));
    }

    /// <summary>
    /// Adds the supplied entry to the import plan.
    /// </summary>
    /// <param name="filename">The filename.</param>
    /// <param name="definition">The persisted definition to process.</param>
    public void Add(string filename, SceneDefinition definition)
    {
        foreach (var error in SceneDefinitionValidator.Validate(definition)) Report(ExternalImportSeverity.Error, "native.validation", error);
        definition.Source = SceneDefinitionSource.Generated();
        Add(filename, "GSCN", definition.ID, Encoding.UTF8.GetBytes(SceneDefinitionSerializer.ToJson(definition)));
    }

    /// <summary>
    /// Adds the supplied entry to the import plan.
    /// </summary>
    /// <param name="filename">The filename.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="key">The lookup key for the resource.</param>
    /// <param name="content">The content.</param>
    public void Add(string filename, string kind, string? key, byte[] content)
    {
        if (filename != Path.GetFileName(filename) || filename is "." or "..")
            throw new InvalidDataException("Generated output must be a filename within the output directory.");
        Outputs.Add((new(kind, Path.Combine(Path.GetFullPath(Request.OutputDirectory), filename), key), content));
    }

    internal ExternalImportAnalysis Analyze(string providerId)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (artifact, _) in Outputs)
        {
            if (!paths.Add(artifact.OutputPath)) Report(ExternalImportSeverity.Error, "output.duplicate", $"Duplicate output: {artifact.OutputPath}");
            if (artifact.LogicalKey is { } key && !keys.Add(artifact.Kind + ":" + key))
                Report(ExternalImportSeverity.Error, "key.duplicate", $"Duplicate {artifact.Kind} key: {key}");
            if (Directory.Exists(artifact.OutputPath) || (File.Exists(artifact.OutputPath) && !Request.Overwrite))
                Report(ExternalImportSeverity.Error, "output.exists", $"Output already exists: {artifact.OutputPath}. Enable overwrite to replace generated files.");
            if (Dependencies.Append(Path.GetFullPath(Request.SourcePath)).Any(p => string.Equals(Path.GetFullPath(p), artifact.OutputPath, StringComparison.OrdinalIgnoreCase)))
                Report(ExternalImportSeverity.Error, "output.source", $"Output would replace a source dependency: {artifact.OutputPath}");
        }
        foreach (var dependency in Dependencies.Distinct(StringComparer.OrdinalIgnoreCase))
            if (!File.Exists(dependency)) Report(ExternalImportSeverity.Error, "dependency.missing", $"Missing source dependency: {dependency}");
        CheckExistingKeys();
        return new(providerId, Dependencies.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), Outputs.Select(x => x.Artifact).ToArray(), Diagnostics.ToArray());
    }

    private void CheckExistingKeys()
    {
        if (!Directory.Exists(Request.OutputDirectory)) return;
        var plannedPaths = Outputs.Select(o => o.Artifact.OutputPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var plannedKeys = Outputs.Where(o => o.Artifact.LogicalKey is not null)
            .Select(o => o.Artifact.Kind + ":" + o.Artifact.LogicalKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string file in Directory.EnumerateFiles(Request.OutputDirectory).OrderBy(p => p, StringComparer.Ordinal))
            {
                if (plannedPaths.Contains(Path.GetFullPath(file))) continue;
                string? key;
                try
                {
                    key = Path.GetExtension(file).ToLowerInvariant() switch
                    {
                        ".gts" => "GTS:" + TilesheetDefinitionSerializer.Load(file).Name,
                        ".gani" => "GANI:" + AnimationDefinitionSerializer.Load(file).Key,
                        ".gscn" => "GSCN:" + SceneDefinitionSerializer.Load(file).ID,
                        _ => null
                    };
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
                {
                    Report(ExternalImportSeverity.Warning, "key.unchecked", $"Cannot inspect existing native keys in {file}: {ex.Message}");
                    continue;
                }
                if (key is not null && plannedKeys.Contains(key))
                    Report(ExternalImportSeverity.Error, "key.exists", $"Logical key {key} already exists in {file}.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Report(ExternalImportSeverity.Error, "output.unreadable", ex.Message); }
    }
}

/// <summary>Providers build an in-memory plan. Only this shared pipeline writes files.</summary>
public abstract class ExternalAssetImporter : IExternalAssetImporter
{
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    public abstract string Id { get; }
    /// <summary>
    /// Gets the display name.
    /// </summary>
    public abstract string DisplayName { get; }
    /// <summary>
    /// Gets the supported extensions.
    /// </summary>
    public abstract IReadOnlyList<string> SupportedExtensions { get; }
    /// <summary>
    /// Checks whether the provider supports the requested import source.
    /// </summary>
    /// <param name="sourcePath">The path to the external source file.</param>
    /// <returns><see langword="true"/> if the provider supports the source; otherwise, <see langword="false"/>.</returns>
    public virtual bool CanImport(string sourcePath) => SupportedExtensions.Contains(Path.GetExtension(sourcePath), StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Builds the planned output files and diagnostics for an external import.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    protected abstract void BuildPlan(ImportPlan plan, CancellationToken cancellationToken);

    private (ImportPlan Plan, ExternalImportAnalysis Analysis) Prepare(ExternalImportRequest request, CancellationToken token)
    {
        var plan = new ImportPlan(request);
        try
        {
            token.ThrowIfCancellationRequested();
            var outputDirectory = Path.GetFullPath(request.OutputDirectory);
            if (File.Exists(outputDirectory)) throw new InvalidDataException("Output directory points to an existing file.");
            plan.Dependencies.Add(Path.GetFullPath(request.SourcePath));
            BuildPlan(plan, token);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or FormatException or OverflowException or ArgumentException or System.Xml.XmlException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            plan.Report(ExternalImportSeverity.Error, "source.invalid", ex.Message);
        }
        return (plan, plan.Analyze(Id));
    }

    /// <summary>
    /// Analyzes the external source and planned import outputs.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The planned import outputs and diagnostics without writing output files.</returns>
    public ExternalImportAnalysis Analyze(ExternalImportRequest request, CancellationToken cancellationToken = default) => Prepare(request, cancellationToken).Analysis;

    /// <summary>
    /// Imports the external source into Gondwana asset definitions.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The generated output paths and diagnostics from the import.</returns>
    public ExternalImportResult Import(ExternalImportRequest request, CancellationToken cancellationToken = default)
    {
        WriteGate.Wait(cancellationToken);
        try { return ImportCore(request, cancellationToken); }
        finally { WriteGate.Release(); }
    }

    private ExternalImportResult ImportCore(ExternalImportRequest request, CancellationToken cancellationToken)
    {
        var (plan, analysis) = Prepare(request, cancellationToken);
        if (!analysis.CanImport) return new(analysis, []);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(request.OutputDirectory);
        var stage = Path.Combine(Path.GetFullPath(request.OutputDirectory), ".gondwana-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var committed = new List<(string Target, string? Backup, bool Installed)>();
        bool cleanup = true;
        try
        {
            for (int i = 0; i < plan.Outputs.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.WriteAllBytes(Path.Combine(stage, i.ToString()), plan.Outputs[i].Content);
            }
            for (int i = 0; i < plan.Outputs.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = plan.Outputs[i].Artifact.OutputPath;
                string? backup = null;
                if (File.Exists(target) && request.Overwrite)
                {
                    backup = Path.Combine(stage, i + ".backup");
                    File.Move(target, backup);
                }
                // Journal the backup before installing the replacement so recovery also
                // covers a failure between those two operations.
                committed.Add((target, backup, false));
                File.Move(Path.Combine(stage, i.ToString()), target);
                committed[^1] = (target, backup, true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new(analysis, committed.Select(x => x.Target).ToArray());
        }
        catch
        {
            try
            {
                foreach (var (target, backup, installed) in committed.AsEnumerable().Reverse())
                {
                    if (installed) File.Delete(target);
                    if (backup is not null) File.Move(backup, target);
                }
            }
            catch { cleanup = false; throw new IOException($"Import rollback could not complete. Recovery files remain in {stage}."); }
            throw;
        }
        finally { if (cleanup) Directory.Delete(stage, recursive: true); }
    }
}
