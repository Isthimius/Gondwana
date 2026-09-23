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

            var archiveBytes = File.ReadAllBytes(path);

            AssetsFile loaded;
            using (var stream = new NonSeekableReadOnlyStream(archiveBytes))
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

file sealed class NonSeekableReadOnlyStream(byte[] bytes) : Stream
{
    private readonly MemoryStream _inner = new(bytes, writable: false);

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
        => _inner.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer)
        => _inner.Read(buffer);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.ReadAsync(buffer, cancellationToken);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }
}
