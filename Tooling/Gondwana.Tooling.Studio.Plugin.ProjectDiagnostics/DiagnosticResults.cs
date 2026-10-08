namespace Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics;

/// <summary>A problem tied to a source file and, when available, a model property.</summary>
/// <param name="SourcePath">The path to the external source file.</param>
/// <param name="Property">The property.</param>
/// <param name="Reason">The reason.</param>
public sealed record ProjectProblem(string SourcePath, string Property, string Reason);

/// <summary>An explicit serialized reference; logical runtime names are not inferred as paths.</summary>
/// <param name="Property">The property.</param>
/// <param name="Value">The value.</param>
/// <param name="ResolvedPath">The resolved path.</param>
/// <param name="Problem">The problem.</param>
public sealed record DefinitionReference(string Property, string Value, string? ResolvedPath, string? Problem);

/// <summary>Read-only scan output without retained engine models or open files.</summary>
/// <param name="RelativePath">The relative path.</param>
/// <param name="Format">The encoded image format.</param>
/// <param name="Loaded">Whether the plugin loaded successfully.</param>
/// <param name="References">The references.</param>
public sealed record DefinitionResult(string RelativePath, string Format, bool Loaded,
    IReadOnlyList<DefinitionReference> References);

/// <summary>One complete scan. Replaces, rather than appends to, the preceding result.</summary>
/// <param name="Definitions">The definitions.</param>
/// <param name="Problems">The problems.</param>
public sealed record ProjectScanResult(IReadOnlyList<DefinitionResult> Definitions, IReadOnlyList<ProjectProblem> Problems);
