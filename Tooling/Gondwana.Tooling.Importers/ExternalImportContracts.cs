namespace Gondwana.Tooling.Importers;

/// <summary>
/// Represents external import severity.
/// </summary>
public enum ExternalImportSeverity
{
    /// <summary>
    /// Specifies info.
    /// </summary>
    Info,
    /// <summary>
    /// Specifies warning.
    /// </summary>
    Warning,
    /// <summary>
    /// Specifies error.
    /// </summary>
    Error
}

/// <summary>
/// Represents external import diagnostic.
/// </summary>
/// <param name="Severity">The severity.</param>
/// <param name="Code">The code.</param>
/// <param name="Message">The message.</param>
/// <param name="SourcePath">The path to the external source file.</param>
public sealed record ExternalImportDiagnostic(
    ExternalImportSeverity Severity, string Code, string Message, string? SourcePath = null);

/// <summary>
/// Represents external import request.
/// </summary>
/// <param name="SourcePath">The path to the external source file.</param>
/// <param name="OutputDirectory">The directory receiving the generated files.</param>
/// <param name="Overwrite">Whether existing output files may be replaced.</param>
public sealed record ExternalImportRequest(
    string SourcePath, string OutputDirectory, bool Overwrite = false);

/// <summary>
/// Represents external import artifact.
/// </summary>
/// <param name="Kind">The kind.</param>
/// <param name="OutputPath">The output path.</param>
/// <param name="LogicalKey">The logical key.</param>
public sealed record ExternalImportArtifact(string Kind, string OutputPath, string? LogicalKey);

/// <summary>
/// Represents external import analysis.
/// </summary>
/// <param name="ProviderId">The provider id.</param>
/// <param name="Dependencies">The dependencies.</param>
/// <param name="Artifacts">The artifacts.</param>
/// <param name="Diagnostics">The diagnostics.</param>
public sealed record ExternalImportAnalysis(
    string ProviderId,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<ExternalImportArtifact> Artifacts,
    IReadOnlyList<ExternalImportDiagnostic> Diagnostics)
{
    /// <summary>
    /// Gets whether the operation can import.
    /// </summary>
    public bool CanImport => !Diagnostics.Any(d => d.Severity == ExternalImportSeverity.Error);
}

/// <summary>
/// Represents external import result.
/// </summary>
/// <param name="Analysis">The analysis.</param>
/// <param name="WrittenFiles">The written files.</param>
public sealed record ExternalImportResult(
    ExternalImportAnalysis Analysis, IReadOnlyList<string> WrittenFiles);

/// <summary>Headless conversion boundary. Analysis must not create or modify files.
/// Import must revalidate source data and output conflicts before writing.</summary>
public interface IExternalAssetImporter
{
    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    string Id { get; }
    /// <summary>
    /// Gets the display name.
    /// </summary>
    string DisplayName { get; }
    /// <summary>
    /// Gets the supported extensions.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }
    /// <summary>
    /// Checks whether the provider supports the requested import source.
    /// </summary>
    /// <param name="sourcePath">The path to the external source file.</param>
    /// <returns><see langword="true"/> if the provider supports the source; otherwise, <see langword="false"/>.</returns>
    bool CanImport(string sourcePath);
    /// <summary>
    /// Analyzes the external source and planned import outputs.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The planned import outputs and diagnostics without writing output files.</returns>
    ExternalImportAnalysis Analyze(ExternalImportRequest request, CancellationToken cancellationToken = default);
    /// <summary>
    /// Imports the external source into Gondwana asset definitions.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The generated output paths and diagnostics from the import.</returns>
    ExternalImportResult Import(ExternalImportRequest request, CancellationToken cancellationToken = default);
}
