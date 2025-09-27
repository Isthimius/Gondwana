namespace Gondwana;

public interface IUiDispatcher
{
    bool IsOnUIThread { get; }

    void Post(Action action);        // async, preferred

    void Send(Action action);        // sync; avoid on web platforms
}