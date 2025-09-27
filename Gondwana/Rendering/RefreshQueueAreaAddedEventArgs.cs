using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

internal class RefreshQueueAreaAddedEventArgs : EventArgs
{
    internal SceneLayer layer;
    internal Rectangle area;

    internal RefreshQueueAreaAddedEventArgs(SceneLayer _layer, Rectangle _area)
    {
        layer = _layer;
        area = _area;
    }
}