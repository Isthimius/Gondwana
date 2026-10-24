namespace Gondwana.Tooling.Importers;

/// <summary>Classifies import diagnostics; only Error prevents a plan from being imported.</summary>
public enum ExternalImportSeverity
{
    /// <summary>Informational conversion details that do not block import.</summary>
    Info,
    /// <summary>A conversion limitation or omitted feature that does not block import.</summary>
    Warning,
    /// <summary>A validation or conversion failure that blocks import.</summary>
    Error
}

/// <summary>A conversion, validation, or output-planning diagnostic.</summary>
/// <param name="Severity">Whether the message informs, warns, or blocks import.</param>
/// <param name="Code">Machine-readable diagnostic category, such as output.exists.</param>
/// <param name="Message">Human-readable details for the import preview.</param>
/// <param name="SourcePath">Related external source path, or null when not supplied.</param>
public sealed record ExternalImportDiagnostic(
    ExternalImportSeverity Severity, string Code, string Message, string? SourcePath = null);

/// <summary>Input file, destination directory, and overwrite policy for one conversion.</summary>
/// <param name="SourcePath">External source file path; relative paths use the process current directory.</param>
/// <param name="OutputDirectory">Directory receiving generated files; dependencies may remain outside it.</param>
/// <param name="Overwrite">Whether existing generated output files may be replaced; source dependencies remain protected.</param>
public sealed record ExternalImportRequest(
    string SourcePath, string OutputDirectory, bool Overwrite = false);

/// <summary>One planned physical output, which may contain multiple logical definitions.</summary>
/// <param name="Kind">Native format or raw output label, such as GTS, GANI, GSCN, or PNG.</param>
/// <param name="OutputPath">Destination path proposed by the provider's plan.</param>
/// <param name="LogicalKey">The first logical member identity for legacy callers, or null for an unkeyed artifact.</param>
public sealed record ExternalImportArtifact(string Kind, string OutputPath, string? LogicalKey)
{
    /// <summary>Gets every logical member identity in document order, independently of the physical output path.</summary>
    /// <remarks>Legacy construction defaults to the single LogicalKey, when supplied.</remarks>
    public IReadOnlyList<string> LogicalKeys { get; init; } = LogicalKey is null ? [] : [LogicalKey];
}

/// <summary>Prepared output preview and diagnostics for an external source conversion.</summary>
/// <param name="ProviderId">Identifier of the importer that produced this analysis.</param>
/// <param name="Dependencies">Source and dependency paths consulted by the plan.</param>
/// <param name="Artifacts">Proposed physical outputs and their logical identities.</param>
/// <param name="Diagnostics">Conversion, validation, and conflict messages used to decide whether import may proceed.</param>
public sealed record ExternalImportAnalysis(
    string ProviderId,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<ExternalImportArtifact> Artifacts,
    IReadOnlyList<ExternalImportDiagnostic> Diagnostics)
{
    /// <summary>Gets whether diagnostics contain no errors; warnings and an empty artifact list do not block import.</summary>
    public bool CanImport => !Diagnostics.Any(d => d.Severity == ExternalImportSeverity.Error);
}

/// <summary>The import-time analysis and files successfully installed by that operation.</summary>
/// <param name="Analysis">Fresh analysis performed for the import attempt.</param>
/// <param name="WrittenFiles">Installed output paths; empty when analysis prevented writing.</param>
public sealed record ExternalImportResult(
    ExternalImportAnalysis Analysis, IReadOnlyList<string> WrittenFiles);

/// <summary>Headless conversion boundary. Analysis must not create or modify files.
/// Import must revalidate source data and output conflicts before writing.</summary>
public interface IExternalAssetImporter
{
    /// <summary>Gets the provider identifier included in analysis results.</summary>
    string Id { get; }
    /// <summary>Gets the human-readable format label shown in the import selector.</summary>
    string DisplayName { get; }
    /// <summary>Gets recognized filename extensions, including their leading dots.</summary>
    IReadOnlyList<string> SupportedExtensions { get; }
    /// <summary>Checks whether the provider recognizes a source filename.</summary>
    /// <param name="sourcePath">Source path to test without requiring a valid source document.</param>
    /// <returns>True if recognized; successful recognition does not imply that analysis will succeed.</returns>
    bool CanImport(string sourcePath);
    /// <summary>Analyzes source data, native outputs, and conflicts without creating or modifying files.</summary>
    /// <param name="request">Source file, output directory, and overwrite policy.</param>
    /// <param name="cancellationToken">Token used for cooperative cancellation.</param>
    /// <returns>The planned outputs, dependencies, and diagnostics.</returns>
    /// <exception cref="OperationCanceledException">Cancellation is observed.</exception>
    ExternalImportAnalysis Analyze(ExternalImportRequest request, CancellationToken cancellationToken = default);
    /// <summary>Revalidates the source and conflicts before writing native outputs.</summary>
    /// <param name="request">Source file, output directory, and overwrite policy.</param>
    /// <param name="cancellationToken">Token used for cooperative cancellation.</param>
    /// <returns>Import-time analysis and installed paths, or no paths when errors block import.</returns>
    /// <exception cref="OperationCanceledException">Cancellation is observed.</exception>
    ExternalImportResult Import(ExternalImportRequest request, CancellationToken cancellationToken = default);
}
