using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Templates;
using Dock.Model.Core;
using Gondwana.Tooling.Studio.Avalonia.ViewModels;
using Gondwana.Tooling.Studio.ViewModels;

namespace Gondwana.Tooling.Studio.Avalonia;

/// <summary>
/// Resolves view-model and dockable content objects to Avalonia views.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    private const string DockableContentPropertyName = "Content";
    private static readonly ConcurrentDictionary<(Type Type, string Property), PropertyInfo?> PropertyCache = new();

    /// <summary>
    /// Build.
    /// </summary>
    /// <param name="param">param.</param>
    /// <returns>The result.</returns>
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var source = ResolveSource(param);

        if (source is Control control)
            return control;

        var name = source.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type is not null && Activator.CreateInstance(type) is Control view)
        {
            // Ensure the generated view binds to the resolved source (e.g. dockable.Context)
            // rather than the outer dockable wrapper object.
            view.DataContext = source;
            return view;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    /// <summary>
    /// Match.
    /// </summary>
    /// <param name="data">data.</param>
    /// <returns>The result.</returns>
    public bool Match(object? data)
    {
        if (data is null)
            return false;

        return ResolveSource(data) is ViewModelBase or Control;
    }

    private static object ResolveSource(object data)
    {
        if (data is IDockable dockable)
        {
            if (dockable.Context is not null)
                return dockable.Context;

            // Dock wrappers can expose nested payload via a runtime "Content" property.
            if (TryGetPropertyValue(dockable, DockableContentPropertyName, out var content) && content is not null)
                return content!;
        }

        return data;
    }

    private static bool TryGetPropertyValue(object instance, string propertyName, out object? value)
    {
        var property = PropertyCache.GetOrAdd(
            (instance.GetType(), propertyName),
            key => key.Type.GetProperty(key.Property));

        if (property is null)
        {
            value = null;
            return false;
        }

        value = property.GetValue(instance);
        return true;
    }
}
