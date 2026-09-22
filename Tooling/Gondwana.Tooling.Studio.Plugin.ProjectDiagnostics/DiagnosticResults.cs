namespace Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics;

/// <summary>A problem tied to a source file and, when available, a model property.</summary>
public sealed record ProjectProblem(string SourcePath, string Property, string Reason);

/// <summary>An explicit serialized reference; logical runtime names are not inferred as paths.</summary>
public sealed record DefinitionReference(string Property, string Value, string? ResolvedPath, string? Problem);

/// <summary>Read-only scan output without retained engine models or open files.</summary>
public sealed record DefinitionResult(string RelativePath, string Format, bool Loaded,
    IReadOnlyList<DefinitionReference> References);

/// <summary>One complete scan. Replaces, rather than appends to, the preceding result.</summary>
public sealed record ProjectScanResult(IReadOnlyList<DefinitionResult> Definitions, IReadOnlyList<ProjectProblem> Problems);
