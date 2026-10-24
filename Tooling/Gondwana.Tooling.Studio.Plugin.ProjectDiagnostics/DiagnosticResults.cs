namespace Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics;

/// <summary>A problem tied to a source file and, when available, a model property.</summary>
/// <param name="SourcePath">Path of the affected file or directory relative to the scan root.</param>
/// <param name="Property">Member-qualified property label or scan phase such as Discovery, Load, or Inspection.</param>
/// <param name="Reason">Human-readable validation or failure details.</param>
public sealed record ProjectProblem(string SourcePath, string Property, string Reason);

/// <summary>An inspected serialized reference or unsupported source-kind marker.</summary>
/// <param name="Property">Logical member identity and property path within its definition.</param>
/// <param name="Value">Serialized path or URI, archive path and entry label, or unsupported kind value.</param>
/// <param name="ResolvedPath">Absolute loose-file or archive path when resolved; null for URIs or unresolved paths.</param>
/// <param name="Problem">Reference-check failure details, or null when no failure was recorded; URIs are not checked.</param>
public sealed record DefinitionReference(string Property, string Value, string? ResolvedPath, string? Problem);

/// <summary>Read-only scan output without retained engine models or open files.</summary>
/// <param name="RelativePath">Authoring file path relative to the scan root.</param>
/// <param name="Format">Uppercase extension without the dot, such as GTS or GAF.</param>
/// <param name="Loaded">Whether a definition member was loaded or GAF structure validation succeeded; subsequent inspection can still report problems.</param>
/// <param name="References">Explicit references collected across the file's logical members; GAF structure scans have none.</param>
public sealed record DefinitionResult(string RelativePath, string Format, bool Loaded,
    IReadOnlyList<DefinitionReference> References);

/// <summary>One complete scan whose results replace the preceding scan in the diagnostics pane.</summary>
/// <param name="Definitions">Inspected authoring files, ordered by relative path without case sensitivity.</param>
/// <param name="Problems">Discovery, load, validation, and reference problems collected during the scan.</param>
public sealed record ProjectScanResult(IReadOnlyList<DefinitionResult> Definitions, IReadOnlyList<ProjectProblem> Problems);
