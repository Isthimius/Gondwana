namespace Gondwana.Scenes.EventArgs;

public delegate void ShowGridLinesChangedEventHandler(ShowGridLinesChangedEventArgs e);

public class ShowGridLinesChangedEventArgs : EventArgs
{
    public SceneLayer Matrix;
    public bool oldValue;
    public bool newValue;

    protected internal ShowGridLinesChangedEventArgs(SceneLayer matrix, bool oldVal, bool newVal)
    {
        Matrix = matrix;
        oldValue = oldVal;
        newValue = newVal;
    }
}