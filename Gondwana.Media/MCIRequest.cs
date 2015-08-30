using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gondwana.Media
{
    public class MCIRequest
    {
        public string MCICommand;
        public int retLen;

#if DEBUG
        public long Tick = Environment.TickCount;
#endif

        public MCIRequest(string command, int retlen)
        {
            MCICommand = command;
            retLen = retlen;
        }
    }
}
