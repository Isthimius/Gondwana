namespace Gondwana.Tooling.Importers.Tests;

/// <summary>
/// Contains regression tests for import contract.
/// </summary>
public sealed class ImportContractTests
{
    /// <summary>
    /// Verifies names are portable and deterministic.
    /// </summary>
    /// <param name="input">The input value for this test case.</param>
    /// <param name="expected">The expected value for this test case.</param>
    [Theory]
    [InlineData("Hero Walk", "hero-walk")]
    [InlineData("../CON", "asset-con")]
    [InlineData("NUL.png", "asset-nul.png")]
    [InlineData("...", "asset")]
    [InlineData("Terrain/source:2", "terrain-source-2")]
    public void NamesArePortableAndDeterministic(string input, string expected) =>
        Assert.Equal(expected, ImportNaming.Sanitize(input));

    /// <summary>
    /// Verifies fatal diagnostics prevent import.
    /// </summary>
    /// <param name="severity">The severity value for this test case.</param>
    /// <param name="expected">The expected value for this test case.</param>
    [Theory]
    [InlineData(ExternalImportSeverity.Info, true)]
    [InlineData(ExternalImportSeverity.Warning, true)]
    [InlineData(ExternalImportSeverity.Error, false)]
    public void FatalDiagnosticsPreventImport(ExternalImportSeverity severity, bool expected)
    {
        var analysis = new ExternalImportAnalysis("test", [], [], [new(severity, "test", "Diagnostic")]);
        Assert.Equal(expected, analysis.CanImport);
    }
}
