namespace Gondwana;

public interface IEngineDispatcher
{
    bool IsOnEngineThread { get; }
    void Post(Action action);
    void Drain();
    void BindToCurrentThread();
}
