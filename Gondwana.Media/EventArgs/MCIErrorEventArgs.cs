using Gondwana.Media;

namespace Gondwana.Media.EventArgs
{
    public class MCIErrorEventArgs : System.EventArgs
    {
        public readonly string mciCommand;
        public readonly int ErrNumber;
        public readonly string ErrDescription;

        public MCIErrorEventArgs(string command, int errNum, string errDesc)
        {
            mciCommand = command;
            ErrNumber = errNum;
            ErrDescription = errDesc;
        }
    }
}
