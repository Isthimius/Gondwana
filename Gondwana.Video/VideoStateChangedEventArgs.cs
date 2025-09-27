namespace Gondwana.Video;

public sealed class VideoStateChangedEventArgs : EventArgs
{
    public string State { get; }

    public VideoStateChangedEventArgs(string state) => State = state;
}