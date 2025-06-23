namespace Gondwana;

public class GondwanaSettingException : Exception
{
    public GondwanaSettingException(string msg, Exception innerExc) : base(msg, innerExc) { }
}
