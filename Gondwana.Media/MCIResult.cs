using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gondwana.Media
{
    public class MCIResult
    {
        public int ReturnValue;
        public string ReturnMessage;

        public bool IsError()
        {
            return (ReturnValue != 0);
        }
    }
}
