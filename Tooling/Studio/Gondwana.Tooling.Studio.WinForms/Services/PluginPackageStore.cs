using Gondwana.Tooling.Studio.Core.Extensibility;

namespace Gondwana.Tooling.Studio.WinForms.Services;

internal static class PluginPackageStore
{
    private static PluginPackageOperations? _session;

    // Non-collectible plugin assemblies outlive forms. Retain the reader lease until process exit.
    internal static PluginPackageOperations Open(Action<string> log)
    {
        if (_session is not null) return _session;
        _session = new PluginPackageOperations(Path.Combine(AppContext.BaseDirectory, "plugins"), log,
            typeof(Extensibility.IStudioPlugin).Assembly);
        _session.StartSession();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _session.Dispose();
        return _session;
    }
}
