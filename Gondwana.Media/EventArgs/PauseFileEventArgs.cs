using Gondwana.Media;

namespace Gondwana.Media.EventArgs
{
    public class PauseFileEventArgs : System.EventArgs
    {
        public readonly MediaFile MediaFile;
        public readonly string mciCommand;

        public PauseFileEventArgs(MediaFile mediaFile, string command)
        {
            this.MediaFile = mediaFile;
            mciCommand = command;
        }
    }
}
