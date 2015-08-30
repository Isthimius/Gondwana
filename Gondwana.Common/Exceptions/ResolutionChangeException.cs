using System;

namespace Gondwana.Common.Exceptions
{
    public class ResolutionChangeException : Exception
    {
        public ResolutionChangeException(string msg) : base(msg) { }
    }
}
