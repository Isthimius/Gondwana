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
    }

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
    [Fact]
    public void StreamLoadBuffersArchiveAndDoesNotRequireSourceLifetime()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gaf");
        try
        {
            using (var source = AssetsFile.LoadOrCreate(path, null, false, register: false))
            {
                source.Add(AssetTypes.Image, "sprite.bin", new MemoryStream([1, 2, 3, 4]));
                source.Save();
            }

            AssetsFile loaded;
            using (var stream = File.OpenRead(path))
                loaded = AssetsFile.Load(stream, register: false);

            using (loaded)
            {
                Assert.DoesNotContain(loaded, AssetsFile.AllAssetsFiles);
                using var entry = Assert.IsType<MemoryStream>(loaded.Get(AssetTypes.Image, "sprite.bin"));
                Assert.Equal([1, 2, 3, 4], ((MemoryStream)entry).ToArray());
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FailedStreamLoadDoesNotLeaveRegisteredPackage()
    {
        using var stream = new MemoryStream("not a zip archive"u8.ToArray());
        var before = AssetsFile.AllAssetsFiles.ToArray();

        Assert.ThrowsAny<Exception>(() => AssetsFile.Load(stream));

        Assert.Equal(before, AssetsFile.AllAssetsFiles);
    }

}
