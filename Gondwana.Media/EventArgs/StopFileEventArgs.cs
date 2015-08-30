using Gondwana.Media;

namespace Gondwana.Media.EventArgs
{
    public class StopFileEventArgs : System.EventArgs
    {
        public readonly MediaFile MediaFile;

        public StopFileEventArgs(MediaFile mediaFile)
        {
            this.MediaFile = mediaFile;
        }
    }
}
