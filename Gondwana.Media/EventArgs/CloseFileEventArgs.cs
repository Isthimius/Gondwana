using Gondwana.Media;

namespace Gondwana.Media.EventArgs
{
    public class CloseFileEventArgs : System.EventArgs
    {
        public readonly MediaFile MediaFile; 
        public readonly string mciCommand;

        public CloseFileEventArgs(MediaFile mediaFile, string command)
        {
            this.MediaFile = mediaFile;
            mciCommand = command;
        }
    }
}
