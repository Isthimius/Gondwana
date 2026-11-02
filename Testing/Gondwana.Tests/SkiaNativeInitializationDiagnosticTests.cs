using System.Reflection;
using Gondwana.Blazor.Rendering;

namespace Gondwana.Tests;

public sealed class SkiaNativeInitializationDiagnosticTests
{
    [Fact]
    public void RecognizesNativeFailureThroughConstructorAndTypeInitializationWrappers()
    {
        var native = new DllNotFoundException("libSkiaSharp");
        var exception = new TargetInvocationException(new TypeInitializationException("SKBitmap", native));
        Assert.True(SkiaNativeInitializationDiagnostic.IsMissingSkiaNativeLibrary(exception));
        Assert.True(SkiaNativeInitializationDiagnostic.IsMissingSkiaNativeLibrary(
            new AggregateException(new InvalidOperationException(), exception)));
    }

    [Fact]
    public void DoesNotMisdiagnoseUnrelatedFailures()
    {
        Assert.False(SkiaNativeInitializationDiagnostic.IsMissingSkiaNativeLibrary(
            new DllNotFoundException("libHarfBuzzSharp")));
        Assert.False(SkiaNativeInitializationDiagnostic.IsMissingSkiaNativeLibrary(
            new InvalidOperationException("libSkiaSharp")));
        Assert.False(SkiaNativeInitializationDiagnostic.IsMissingSkiaNativeLibrary(
            new ArgumentNullException("renderSurfaceHost")));
    }
}
