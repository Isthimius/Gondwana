using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>
/// Verifies the one-render-surface-host-per-scene binding invariant.
/// </summary>
public sealed class RenderSurfaceHostSceneBindingTests
{
    /// <summary>
    /// Verifies bind when scene belongs to another host throws without changing either host.
    /// </summary>
    [Fact]
    public void Bind_WhenSceneBelongsToAnotherHost_ThrowsWithoutChangingEitherHost()
    {
        using var scene = new Scene();
        using var firstHost = CreateHost();
        using var secondHost = CreateHost();

        firstHost.Bind(scene);

        var exception = Assert.Throws<InvalidOperationException>(
            () => secondHost.Bind(scene));

        Assert.Contains(scene.ID, exception.Message);
        Assert.Same(firstHost, scene.BoundRenderSurfaceHost);
        Assert.Same(scene, firstHost.Scene);
        Assert.Same(Scene.Empty, secondHost.Scene);
    }

    /// <summary>
    /// Verifies bind when host changes scenes releases previous scene.
    /// </summary>
    [Fact]
    public void Bind_WhenHostChangesScenes_ReleasesPreviousScene()
    {
        using var firstScene = new Scene();
        using var secondScene = new Scene();
        using var firstHost = CreateHost();
        using var secondHost = CreateHost();

        firstHost.Bind(firstScene);
        firstHost.Bind(secondScene);
        secondHost.Bind(firstScene);

        Assert.Same(secondHost, firstScene.BoundRenderSurfaceHost);
        Assert.Same(firstHost, secondScene.BoundRenderSurfaceHost);
    }

    /// <summary>
    /// Verifies dispose releases scene binding.
    /// </summary>
    [Fact]
    public void Dispose_ReleasesSceneBinding()
    {
        using var scene = new Scene();
        var host = CreateHost();
        host.Bind(scene);

        host.Dispose();

        Assert.Null(scene.BoundRenderSurfaceHost);
    }

    /// <summary>
    /// Verifies scene dispose releases host and restores empty scene.
    /// </summary>
    [Fact]
    public void SceneDispose_ReleasesHostAndRestoresEmptyScene()
    {
        var scene = new Scene();
        using var host = CreateHost();
        host.Bind(scene);

        scene.Dispose();

        Assert.Null(scene.BoundRenderSurfaceHost);
        Assert.Same(Scene.Empty, host.Scene);
    }

    private static RenderSurfaceHost<BitmapBackbuffer> CreateHost() =>
        new(new TestAdapter(320, 200));

    private sealed class TestAdapter(int width, int height)
        : RenderSurfaceAdapterBase(width, height)
    {
        /// <inheritdoc/>
        public override void Present(
            SKImage bufferImage,
            SKRectI bufferRect,
            SKRect destRect)
        {
        }
    }
}
