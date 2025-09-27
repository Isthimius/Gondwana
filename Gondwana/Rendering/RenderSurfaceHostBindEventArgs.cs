using Gondwana.Scenes;

namespace Gondwana.Rendering;

public class RenderSurfaceHostBindEventArgs : EventArgs
{
    public Scene? OldScene { get; }
    public Scene? NewScene { get; }

    private RenderSurfaceHostBindEventArgs() : this(null, null)
    {
    }

    public RenderSurfaceHostBindEventArgs(Scene? oldScene, Scene? newScene)
    {
        OldScene = oldScene;
        NewScene = newScene;
    }
}