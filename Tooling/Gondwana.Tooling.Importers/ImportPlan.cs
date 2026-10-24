using System.Text;
using Gondwana.Audio.GSND;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Importers;

/// <summary>Collects native definitions and raw outputs for shared serialization, validation, and conflict analysis.</summary>
/// <param name="request">Source, destination, and overwrite policy retained by the plan.</param>
public sealed class ImportPlan(ExternalImportRequest request)
{
    /// <summary>Gets validated output snapshots used by the staging transaction.</summary>
    internal List<(ExternalImportArtifact Artifact, byte[] Content)> Outputs { get; } = [];
    private readonly List<PlannedOutput> _planned = [];
    /// <summary>Gets the mutable source dependency paths checked for existence and protected from output replacement.</summary>
    public List<string> Dependencies { get; } = [];
    /// <summary>Gets the mutable conversion and validation messages accumulated by the provider and shared pipeline.</summary>
    public List<ExternalImportDiagnostic> Diagnostics { get; } = [];
    /// <summary>Gets the source and output options retained when this plan was constructed.</summary>
    public ExternalImportRequest Request { get; } = request;

    /// <summary>Appends a diagnostic associated with the request's source path.</summary>
    /// <param name="severity">Whether the diagnostic informs, warns, or blocks import.</param>
    /// <param name="code">Machine-readable diagnostic category.</param>
    /// <param name="message">Human-readable conversion or validation details.</param>
    public void Report(ExternalImportSeverity severity, string code, string message) =>
        Diagnostics.Add(new(severity, code, message, Request.SourcePath));

    /// <summary>Adds a tilesheet member to the output document at the provider-chosen filename.</summary>
    /// <param name="filename">Filename within the output directory; matching names and types form one document.</param>
    /// <param name="definition">Mutable member retained by the plan; its source is immediately set to Generated.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    /// <exception cref="InvalidDataException"><paramref name="filename"/> is empty, a directory path, or a traversal component.</exception>
    public void Add(string filename, TilesheetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Source = TilesheetDefinitionSource.Generated();
        AddNative(filename, "GTS", definition, member => member.Name,
            members => TilesheetDefinitionSerializer.ValidateDocument(new() { Definitions = members }),
            members => TilesheetDefinitionSerializer.ToDocumentJson(new() { Definitions = members }));
    }

    /// <summary>Adds an animation member to the output document at the provider-chosen filename.</summary>
    /// <param name="filename">Filename within the output directory; matching names and types form one document.</param>
    /// <param name="definition">Mutable member retained by the plan; its source is immediately set to Generated.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    /// <exception cref="InvalidDataException"><paramref name="filename"/> is empty, a directory path, or a traversal component.</exception>
    public void Add(string filename, AnimationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Source = AnimationDefinitionSource.Generated();
        AddNative(filename, "GANI", definition, member => member.Key,
            members => AnimationDefinitionSerializer.ValidateDocument(new() { Definitions = members }),
            members => AnimationDefinitionSerializer.ToDocumentJson(new() { Definitions = members }));
    }

    /// <summary>Adds a scene member to the output document at the provider-chosen filename.</summary>
    /// <param name="filename">Filename within the output directory; matching names and types form one document.</param>
    /// <param name="definition">Mutable member retained by the plan; its source is immediately set to Generated.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    /// <exception cref="InvalidDataException"><paramref name="filename"/> is empty, a directory path, or a traversal component.</exception>
    public void Add(string filename, SceneDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Source = SceneDefinitionSource.Generated();
        AddNative(filename, "GSCN", definition, member => member.ID,
            members => SceneDefinitionSerializer.ValidateDocument(new() { Definitions = members }),
            members => SceneDefinitionSerializer.ToDocumentJson(new() { Definitions = members }));
    }

    /// <summary>Adds a sprite member to the output document at the provider-chosen filename.</summary>
    /// <param name="filename">Filename within the output directory; matching names and types form one document.</param>
    /// <param name="definition">Mutable member retained by the plan; its source is immediately set to Generated.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    /// <exception cref="InvalidDataException"><paramref name="filename"/> is empty, a directory path, or a traversal component.</exception>
    public void Add(string filename, SpriteDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Source = SpriteDefinitionSource.Generated();
        AddNative(filename, "GSPR", definition, member => member.Key,
            members => SpriteDefinitionSerializer.ValidateDocument(new() { Definitions = members }),
            members => SpriteDefinitionSerializer.ToDocumentJson(new() { Definitions = members }));
    }

    /// <summary>Adds an audio member to the output document at the provider-chosen filename.</summary>
    /// <param name="filename">Filename within the output directory; matching names and types form one document.</param>
    /// <param name="definition">Mutable member retained by the plan; its source is immediately set to Generated.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    /// <exception cref="InvalidDataException"><paramref name="filename"/> is empty, a directory path, or a traversal component.</exception>
    public void Add(string filename, AudioDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Source = AudioDefinitionSource.Generated();
        AddNative(filename, "GSND", definition, member => member.Key,
            members => AudioDefinitionSerializer.ValidateDocument(new() { Definitions = members }),
            members => AudioDefinitionSerializer.ToDocumentJson(new() { Definitions = members }));
    }

    /// <summary>Adds raw bytes without native-document grouping, validation, or serialization.</summary>
    /// <param name="filename">Filename within the output directory, without directory components.</param>
    /// <param name="kind">Output format label, such as PNG.</param>
    /// <param name="key">Optional logical identity; null for an unkeyed artifact.</param>
    /// <param name="content">Byte array retained without copying; keep its contents stable until import finishes.</param>
    /// <exception cref="InvalidDataException">The filename is empty, a directory path, or a traversal component.</exception>
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
    /// <inheritdoc/>
    public abstract string Id { get; }
    /// <inheritdoc/>
    public abstract string DisplayName { get; }
    /// <inheritdoc/>
    public abstract IReadOnlyList<string> SupportedExtensions { get; }
    /// <summary>Recognizes a source by case-insensitive filename extension without opening it.</summary>
    /// <param name="sourcePath">Source filename or path to inspect.</param>
    /// <returns>True if the extension is listed in <see cref="SupportedExtensions"/>.</returns>
    public virtual bool CanImport(string sourcePath) => SupportedExtensions.Contains(Path.GetExtension(sourcePath), StringComparer.OrdinalIgnoreCase);
    /// <summary>Reads the source and populates outputs, dependencies, and diagnostics without writing files.</summary>
    /// <param name="plan">Mutable plan supplied by the pipeline for this conversion.</param>
    /// <param name="cancellationToken">Token the provider should check during expensive work.</param>
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

    /// <summary>Builds and validates a fresh in-memory plan without creating or modifying files.</summary>
    /// <param name="request">Source file, output directory, and overwrite policy.</param>
    /// <param name="cancellationToken">Token for cooperative cancellation during planning and validation.</param>
    /// <returns>Outputs and diagnostics, including handled source errors; no bytes are persisted.</returns>
    /// <exception cref="OperationCanceledException">Cancellation is observed.</exception>
    public ExternalImportAnalysis Analyze(ExternalImportRequest request, CancellationToken cancellationToken = default) => Prepare(request, cancellationToken).Analysis;

    /// <summary>Rebuilds the plan, stages validated output bytes, and installs generated files under a process-wide write gate.</summary>
    /// <param name="request">Source file, output directory, and permission to replace existing generated files.</param>
    /// <param name="cancellationToken">Token honored while waiting, staging, installing, and before successful completion.</param>
    /// <returns>Fresh analysis and installed paths, or empty paths when analysis reports errors.</returns>
    /// <remarks>Ordinary installation failures roll back installed files. A failed rollback retains staging recovery files; this is not a crash-atomic multi-file transaction.</remarks>
    /// <exception cref="OperationCanceledException">Cancellation is observed.</exception>
    /// <exception cref="IOException">Output I/O fails or rollback cannot complete.</exception>
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
