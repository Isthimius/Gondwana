using Gondwana.Media;

namespace Gondwana.Media.EventArgs
{
    public class OpenFileEventArgs : System.EventArgs
    {
        public readonly MediaFile MediaFile;
        public readonly string FileName;

        public OpenFileEventArgs(MediaFile mediaFile, string filename)
        {
            this.MediaFile = mediaFile;
            FileName = filename;
        }
    }
}
