namespace Gondwana.Tooling.Importers;

public enum ExternalImportSeverity { Info, Warning, Error }

public sealed record ExternalImportDiagnostic(
    ExternalImportSeverity Severity, string Code, string Message, string? SourcePath = null);

public sealed record ExternalImportRequest(
    string SourcePath, string OutputDirectory, bool Overwrite = false);

public sealed record ExternalImportArtifact(string Kind, string OutputPath, string? LogicalKey);

public sealed record ExternalImportAnalysis(
    string ProviderId,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<ExternalImportArtifact> Artifacts,
    IReadOnlyList<ExternalImportDiagnostic> Diagnostics)
{
    public bool CanImport => !Diagnostics.Any(d => d.Severity == ExternalImportSeverity.Error);
}

public sealed record ExternalImportResult(
    ExternalImportAnalysis Analysis, IReadOnlyList<string> WrittenFiles);

/// <summary>Headless conversion boundary. Analysis must not create or modify files.
/// Import must revalidate source data and output conflicts before writing.</summary>
public interface IExternalAssetImporter
{
    string Id { get; }
    string DisplayName { get; }
    IReadOnlyList<string> SupportedExtensions { get; }
    bool CanImport(string sourcePath);
    ExternalImportAnalysis Analyze(ExternalImportRequest request, CancellationToken cancellationToken = default);
    ExternalImportResult Import(ExternalImportRequest request, CancellationToken cancellationToken = default);
}
