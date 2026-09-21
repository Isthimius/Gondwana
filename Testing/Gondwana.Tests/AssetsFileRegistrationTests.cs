using Gondwana.Assets;

namespace Gondwana.Tests;

[Collection("Global engine state")]
public sealed class AssetsFileRegistrationTests
{
    [Fact]
    public void DetachedAuthoringPackagesDoNotChangeRuntimeRegistry()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gaf");
        try
        {
            using var runtime = AssetsFile.LoadOrCreate(path);
            Assert.Contains(runtime, AssetsFile.AllAssetsFiles);
            using var authoring = AssetsFile.LoadOrCreate(path, null, false, register: false);
            Assert.DoesNotContain(authoring, AssetsFile.AllAssetsFiles);
            authoring.Dispose();
            Assert.Contains(runtime, AssetsFile.AllAssetsFiles);
            runtime.Dispose();
            Assert.DoesNotContain(runtime, AssetsFile.AllAssetsFiles);
        }
        finally { File.Delete(path); }

    [Fact]
    public void FailedLoadDoesNotLeaveRegisteredPackage()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gaf");
        try
        {
            File.WriteAllText(path, "not a zip archive");
            Assert.ThrowsAny<Exception>(() => AssetsFile.LoadOrCreate(path));
            Assert.DoesNotContain(AssetsFile.AllAssetsFiles, package => package.FilePath == path);
        }
        finally { File.Delete(path); }
    }
}
