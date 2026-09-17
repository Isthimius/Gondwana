using System.Collections.Concurrent;
using Gondwana.Scenes;

namespace Gondwana.Tests;

[Collection("Global engine state")]
public sealed class SceneRegistryConcurrencyTests
{
    [Fact]
    public async Task GlobalSceneRegistry_SupportsConcurrentCreateDisposeAndLookup()
    {
        Scene.ClearAllScenes();

        try
        {
            var exceptions = new ConcurrentQueue<Exception>();
            var start = new ManualResetEventSlim(false);

            var tasks = Enumerable.Range(0, Environment.ProcessorCount)
                .Select(workerIndex => Task.Run(() =>
                {
                    start.Wait();

                    try
                    {
                        for (int i = 0; i < 50; i++)
                        {
                            using var scene = new Scene();
                            scene.AddLayer(4, 4);

                            _ = Scene.GetSceneByID(scene.ID);
                            _ = Scene.GetAllSceneIDs();
                            _ = Scene.GetAllScenes();
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Enqueue(ex);
                    }
                }))
                .ToArray();

            start.Set();
            await Task.WhenAll(tasks);

            Assert.Empty(exceptions);
        }
        finally
        {
            Scene.ClearAllScenes();
        }
    }
}
