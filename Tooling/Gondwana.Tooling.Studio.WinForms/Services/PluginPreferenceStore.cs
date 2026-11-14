using Gondwana.Tooling.Studio.Core.Extensibility;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Services;

internal static class PluginPreferenceStore
{
    internal static PluginPreferences Open(Action<string> log)
        => new(Path.Combine(DockLayoutStore.ApplicationDirectory(), "Studio", "plugin-preferences.json"), log);
}
