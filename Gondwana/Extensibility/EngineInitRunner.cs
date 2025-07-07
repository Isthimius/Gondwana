using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Gondwana.Extensibility;

internal static class GondwanaInitRunner
{
    internal static void Run(InitTiming initTiming)
    {
        List<(MethodInfo Method, int Priority)> methods = [];

        var dllPaths = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory)
            .Where(f => Path.GetFileName(f).StartsWith("Gondwana.", StringComparison.OrdinalIgnoreCase) &&
                        Path.GetFileName(f).EndsWith(".dll", StringComparison.OrdinalIgnoreCase));

        foreach (var dllPath in dllPaths)
        {
            var assembly = Assembly.Load(dllPath);

            foreach (var type in assembly.GetTypes())
            {
                var initMethods = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Select(m => (Method: m, Attr: m.GetCustomAttribute<EngineInitAttribute>()))
                    .Where(pair =>
                        pair.Attr != null &&
                        pair.Attr.InitTiming == initTiming &&
                        pair.Method.ReturnType == typeof(void) &&
                        pair.Method.GetParameters().Length == 0);

                methods.AddRange(initMethods.Select(pair => (pair.Method, pair.Attr.Priority)));
            }
        }

        // sort by priority descending
        foreach (var (method, priority) in methods.OrderByDescending(m => m.Priority))
        {
            try
            {
                method.Invoke(null, null);
                Engine.Logger.LogInformation("[EngineInit] Ran {MethodFullName} (Priority {Priority})",
                    method.DeclaringType?.FullName + "." + method.Name, priority);
            }
            catch (Exception ex)
            {
                Engine.Logger.LogError(ex, "[EngineInit] Error invoking {MethodFullName}: {Message}",
                    method.DeclaringType?.FullName + "." + method.Name, ex.Message);
            }
        }
    }
}