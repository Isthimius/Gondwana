using Gondwana.Assets;

namespace Gondwana.Tests;

/// <summary>
/// Contains regression tests for assets file registration.
/// </summary>
[Collection("Global engine state")]
public sealed class AssetsFileRegistrationTests
{
    /// <summary>
    /// Verifies detached authoring packages do not change runtime registry.
    /// </summary>
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

    /// <summary>
    /// Verifies failed load does not leave registered package.
    /// </summary>
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
    /// <summary>
    /// Verifies stream load buffers archive and does not require source lifetime.
    /// </summary>
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

    /// <summary>
    /// Verifies failed stream load does not leave registered package.
    /// </summary>
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

    /// <inheritdoc/>
    public override bool CanRead => true;
    /// <inheritdoc/>
    public override bool CanSeek => false;
    /// <inheritdoc/>
    public override bool CanWrite => false;
    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
        => _inner.Read(buffer, offset, count);

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
        => _inner.Read(buffer);

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.ReadAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }
}
