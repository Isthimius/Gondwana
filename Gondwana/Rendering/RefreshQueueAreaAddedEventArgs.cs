using Gondwana.Scenes;
using System.Drawing;

namespace Gondwana.Rendering;

internal delegate void RefreshQueueAreaAddedEventHandler(RefreshQueueAreaAddedEventArgs e);

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
