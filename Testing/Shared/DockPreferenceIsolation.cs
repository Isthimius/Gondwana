using System.Reflection;
using Xunit;
using Xunit.Sdk;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: Gondwana.Tooling.Tests.DockPreferenceIsolation]

namespace Gondwana.Tooling.Tests;

/// <summary>Every test, including existing interaction tests, uses private preferences.</summary>
public sealed class DockPreferenceIsolationAttribute : BeforeAfterTestAttribute
{
    private string? _directory;

    public override void Before(MethodInfo methodUnderTest)
    {
        _directory = Path.Combine(Path.GetTempPath(), "GondwanaLayoutTests-" + Guid.NewGuid());
        Directory.CreateDirectory(_directory);
        AppContext.SetData("Gondwana.Tooling.SettingsRoot", _directory);
        AppContext.SetData("Gondwana.Tooling.ApplicationId", "test-host");
    }

    public override void After(MethodInfo methodUnderTest)
    {
        // Leave the override in place between tests so no late disposal can reach real settings.
        if (_directory is not null && Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
