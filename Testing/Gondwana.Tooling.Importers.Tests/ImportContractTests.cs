using Gondwana.Tooling.Importers;

namespace Gondwana.Tooling.Importers.Tests;

public sealed class ImportContractTests
{
    [Theory]
    [InlineData("Hero Walk", "hero-walk")]
    [InlineData("../CON", "asset-con")]
    [InlineData("NUL.png", "asset-nul.png")]
    [InlineData("...", "asset")]
    [InlineData("Terrain/source:2", "terrain-source-2")]
    public void NamesArePortableAndDeterministic(string input, string expected) =>
        Assert.Equal(expected, ImportNaming.Sanitize(input));

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
