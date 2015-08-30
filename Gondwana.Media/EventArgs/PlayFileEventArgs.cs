using Gondwana.Media;

namespace Gondwana.Media.EventArgs
{
    public class PlayFileEventArgs : System.EventArgs
    {
        public readonly MediaFile MediaFile;
        public readonly string mciCommand;

        public PlayFileEventArgs(MediaFile mediaFile, string command)
        {
            this.MediaFile = mediaFile;
            mciCommand = command;
        }
    }
}
