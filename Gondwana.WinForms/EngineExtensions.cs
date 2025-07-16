using Gondwana.Audio.WinForms;

namespace Gondwana.WinForms;

public static class EngineExtensions
{
    public static void InitializeWinFormsEngine(this Engine engine)
    {
        WinFormsAudioSupport.RegisterExtendedAudioFormats();
        // TODO: Register other WinForms-specific features as needed
    }
}
