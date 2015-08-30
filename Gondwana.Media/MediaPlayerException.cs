using System;

namespace Gondwana.Media
{
    public class MediaPlayerException : Exception
    {
        public string MCICommand;
        public int MCIErrNum;

        public MediaPlayerException(string mciCommand, int mciErr, string mciErrorString)
            : base(mciErrorString)
        {
            MCICommand = mciCommand;
            MCIErrNum = mciErr;
        }
    }
}
