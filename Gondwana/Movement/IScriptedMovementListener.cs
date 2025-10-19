namespace Gondwana.Movement;

/// <summary>
/// Implemented by movable objects that want to receive notifications
/// when scripted movement completes or is cancelled.
/// </summary>
public interface IScriptedMovementListener
{
    void OnScriptedMovementStopped();
}