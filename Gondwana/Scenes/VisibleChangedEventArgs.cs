namespace Gondwana.Scenes;

public delegate void VisibleChangedEventHandler(VisibleChangedEventArgs e);

public class VisibleChangedEventArgs : EventArgs
{
    public SceneLayer Matrix;
    public bool oldVisibleValue;
    public bool newVisibleValue;

    protected internal VisibleChangedEventArgs(SceneLayer matrix, bool oldValue, bool newValue)
    {
        Matrix = matrix;
        oldVisibleValue = oldValue;
        newVisibleValue = newValue;
    }
}
