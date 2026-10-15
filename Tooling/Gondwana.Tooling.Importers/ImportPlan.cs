using System.Text;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Importers;

/// <summary>Shared native serialization, validation and conflict analysis.</summary>
/// <param name="request">The request.</param>
public sealed class ImportPlan(ExternalImportRequest request)
{
    /// <summary>Gets validated output snapshots used by the staging transaction.</summary>
    internal List<(ExternalImportArtifact Artifact, byte[] Content)> Outputs { get; } = [];
    private readonly List<PlannedOutput> _planned = [];
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

    /// <summary>Adds a typed member to the document at the provider-chosen filename.</summary>
    /// <param name="filename">The output filename; matching filenames and types form one document.</param>
    /// <param name="definition">The generated member. The plan owns it until analysis completes.</param>
    public void Add(string filename, TilesheetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Source = TilesheetDefinitionSource.Generated();
        AddNative(filename, "GTS", definition, member => member.Name,
            members => TilesheetDefinitionSerializer.ValidateDocument(new() { Definitions = members }),
            members => TilesheetDefinitionSerializer.ToDocumentJson(new() { Definitions = members }));
    }

    /// <summary>Adds a typed member to the document at the provider-chosen filename.</summary>
    /// <param name="filename">The output filename; matching filenames and types form one document.</param>
    /// <param name="definition">The generated member. The plan owns it until analysis completes.</param>
    public void Add(string filename, AnimationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Source = AnimationDefinitionSource.Generated();
        AddNative(filename, "GANI", definition, member => member.Key,
            members => AnimationDefinitionSerializer.ValidateDocument(new() { Definitions = members }),
            members => AnimationDefinitionSerializer.ToDocumentJson(new() { Definitions = members }));
    }

    /// <summary>Adds a typed member to the document at the provider-chosen filename.</summary>
    /// <param name="filename">The output filename; matching filenames and types form one document.</param>
    /// <param name="definition">The generated member. The plan owns it until analysis completes.</param>
    public void Add(string filename, SceneDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Source = SceneDefinitionSource.Generated();
        AddNative(filename, "GSCN", definition, member => member.ID,
            members => SceneDefinitionSerializer.ValidateDocument(new() { Definitions = members }),
            members => SceneDefinitionSerializer.ToDocumentJson(new() { Definitions = members }));
    }

    /// <summary>Adds a typed member to the document at the provider-chosen filename.</summary>
    /// <param name="filename">The output filename; matching filenames and types form one document.</param>
    /// <param name="definition">The generated member. The plan owns it until analysis completes.</param>
    public void Add(string filename, SpriteDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Source = SpriteDefinitionSource.Generated();
        AddNative(filename, "GSPR", definition, member => member.Key,
            members => SpriteDefinitionSerializer.ValidateDocument(new() { Definitions = members }),
            members => SpriteDefinitionSerializer.ToDocumentJson(new() { Definitions = members }));
    }

    /// <summary>Adds a typed member to the document at the provider-chosen filename.</summary>
    /// <param name="filename">The output filename; matching filenames and types form one document.</param>
    /// <param name="definition">The generated member. The plan owns it until analysis completes.</param>
    public void Add(string filename, AudioDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Source = AudioDefinitionSource.Generated();
        AddNative(filename, "GSND", definition, member => member.Key,
            members => AudioDefinitionSerializer.ValidateDocument(new() { Definitions = members }),
            members => AudioDefinitionSerializer.ToDocumentJson(new() { Definitions = members }));
    }

    /// <summary>Adds a raw artifact without native-document grouping or serialization.</summary>
    /// <param name="filename">The output filename.</param>
    /// <param name="kind">The artifact kind.</param>
    /// <param name="key">An optional legacy logical key.</param>
    /// <param name="content">The raw output bytes.</param>
    public void Add(string filename, string kind, string? key, byte[] content) =>
        _planned.Add(new RawOutput(OutputPath(filename), kind, key, content));

    private string OutputPath(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename) || filename != Path.GetFileName(filename) || filename is "." or "..")
            throw new InvalidDataException("Generated output must be a filename within the output directory.");
        return Path.Combine(Path.GetFullPath(Request.OutputDirectory), filename);
    }

    private void AddNative<T>(string filename, string kind, T definition, Func<T, string> identity,
        Func<List<T>, IReadOnlyList<string>> validate, Func<List<T>, string> serialize)
    {
        var path = OutputPath(filename);
        var group = _planned.OfType<NativeOutput<T>>().FirstOrDefault(output =>
            string.Equals(output.Path, path, StringComparison.OrdinalIgnoreCase));
        if (group is null)
        {
            group = new NativeOutput<T>(path, kind, identity, validate, serialize);
            _planned.Add(group);
        }
        group.Members.Add(definition);
    }

    private abstract class PlannedOutput(string path, string kind)
    {
        /// <summary>Gets the physical output path.</summary>
        public string Path { get; } = path;
        /// <summary>Gets the native format or raw artifact kind.</summary>
        public string Kind { get; } = kind;
        /// <summary>Gets all logical identities in member order.</summary>
        public abstract IReadOnlyList<string> Identities { get; }
        /// <summary>Validates the complete output.</summary>
        /// <returns>Validation diagnostics.</returns>
        public abstract IReadOnlyList<string> Validate();
        /// <summary>Serializes the completed output once.</summary>
        /// <returns>The bytes to stage.</returns>
        public abstract byte[] Serialize();
    }

    private sealed class RawOutput(string path, string kind, string? key, byte[] content) : PlannedOutput(path, kind)
    {
        /// <inheritdoc/>
        public override IReadOnlyList<string> Identities => key is null ? [] : [key];
        /// <inheritdoc/>
        public override IReadOnlyList<string> Validate() => [];
        /// <inheritdoc/>
        public override byte[] Serialize() => content;
    }

    private sealed class NativeOutput<T>(string path, string kind, Func<T, string> identity,
        Func<List<T>, IReadOnlyList<string>> validate, Func<List<T>, string> serialize) : PlannedOutput(path, kind)
    {
        /// <summary>Gets the provider-owned typed members.</summary>
        public List<T> Members { get; } = [];
        /// <inheritdoc/>
        public override IReadOnlyList<string> Identities => Members.Select(identity).ToArray();
        /// <inheritdoc/>
        public override IReadOnlyList<string> Validate() => validate(Members);
        /// <inheritdoc/>
        public override byte[] Serialize() => Encoding.UTF8.GetBytes(serialize(Members));
    }

    /// <summary>Validates completed documents and produces immutable output byte snapshots.</summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>Physical output and logical member analysis.</returns>
    internal ExternalImportAnalysis Analyze(string providerId, CancellationToken token)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        Outputs.Clear();
        foreach (var output in _planned)
        {
            token.ThrowIfCancellationRequested();
            var identities = Array.AsReadOnly(output.Identities.ToArray());
            var artifact = new ExternalImportArtifact(output.Kind, output.Path, identities.FirstOrDefault()) { LogicalKeys = identities };
            byte[] content = [];
            try
            {
                var errors = output.Validate();
                foreach (var error in errors) Report(ExternalImportSeverity.Error, "native.validation", $"{output.Path}: {error}");
                if (errors.Count == 0) content = output.Serialize();
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException or Newtonsoft.Json.JsonException)
            {
                Report(ExternalImportSeverity.Error, "native.validation", $"{output.Path}: {ex.Message}");
            }
            Outputs.Add((artifact, content));
            if (!paths.Add(artifact.OutputPath)) Report(ExternalImportSeverity.Error, "output.duplicate", $"Duplicate output: {artifact.OutputPath}");
            foreach (var key in artifact.LogicalKeys)
                if (!keys.Add(artifact.Kind + ":" + key))
                    Report(ExternalImportSeverity.Error, "key.duplicate", $"{artifact.OutputPath}: Duplicate {artifact.Kind} key: {key}");
            if (Directory.Exists(artifact.OutputPath) || (File.Exists(artifact.OutputPath) && !Request.Overwrite))
                Report(ExternalImportSeverity.Error, "output.exists", $"Output already exists: {artifact.OutputPath}. Enable overwrite to replace generated files.");
            if (Dependencies.Append(Path.GetFullPath(Request.SourcePath)).Any(p => string.Equals(Path.GetFullPath(p), artifact.OutputPath, StringComparison.OrdinalIgnoreCase)))
                Report(ExternalImportSeverity.Error, "output.source", $"Output would replace a source dependency: {artifact.OutputPath}");
        }
        foreach (var dependency in Dependencies.Distinct(StringComparer.OrdinalIgnoreCase))
            if (!File.Exists(dependency)) Report(ExternalImportSeverity.Error, "dependency.missing", $"Missing source dependency: {dependency}");
        CheckExistingKeys(keys, token);
        token.ThrowIfCancellationRequested();
        return new(providerId, Dependencies.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), Outputs.Select(x => x.Artifact).ToArray(), Diagnostics.ToArray());
    }

    private void CheckExistingKeys(HashSet<string> plannedKeys, CancellationToken token)
    {
        if (!Directory.Exists(Request.OutputDirectory)) return;
        var plannedPaths = Outputs.Select(o => o.Artifact.OutputPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string file in Directory.EnumerateFiles(Request.OutputDirectory).OrderBy(p => p, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                if (plannedPaths.Contains(Path.GetFullPath(file))) continue;
                IEnumerable<string> existingKeys;
                try
                {
                    existingKeys = Path.GetExtension(file).ToLowerInvariant() switch
                    {
                        ".gts" => TilesheetDefinitionSerializer.LoadAll(file).Select(member => "GTS:" + member.Name),
                        ".gani" => AnimationDefinitionSerializer.LoadAll(file).Select(member => "GANI:" + member.Key),
                        ".gscn" => SceneDefinitionSerializer.LoadAll(file).Select(member => "GSCN:" + member.ID),
                        ".gspr" => SpriteDefinitionSerializer.LoadAll(file).Select(member => "GSPR:" + member.Key),
                        ".gsnd" => AudioDefinitionSerializer.LoadAll(file).Select(member => "GSND:" + member.Key),
                        _ => []
                    };
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
                {
                    Report(ExternalImportSeverity.Warning, "key.unchecked", $"Cannot inspect existing native keys in {file}: {ex.Message}");
                    continue;
                }
                foreach (var key in existingKeys)
                    if (plannedKeys.Contains(key))
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
        return (plan, plan.Analyze(Id, token));
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
