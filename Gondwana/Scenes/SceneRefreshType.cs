namespace Gondwana.Scenes;

/// <summary>
/// None -> no refresh needed
/// Queue -> draw from refresh queue
/// All -> redraw layer
/// </summary>

public enum SceneRefreshType
{ None, Queue, All }