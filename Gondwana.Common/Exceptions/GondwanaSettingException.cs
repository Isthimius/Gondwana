namespace Gondwana.Common.Exceptions;

public class GondwanaSettingException : Exception
{
    public GondwanaSettingException(string msg, System.Exception innerExc) : base(msg, innerExc) { }
}
