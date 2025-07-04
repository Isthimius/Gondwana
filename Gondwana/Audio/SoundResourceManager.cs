namespace Gondwana.Audio;

public partial class SoundResourceManager : IDisposable
{
    private static readonly Lazy<SoundResourceManager> _instance = new(() => new SoundResourceManager());

    private bool _disposed = false;

    private SoundResourceManager() { }

    public static SoundResourceManager Instance { get; } = _instance.Value;

    public void Dispose()
    {
        if (!_disposed)
        {
            // Dispose of any resources here if necessary
            _disposed = true;
        }
    }
}
