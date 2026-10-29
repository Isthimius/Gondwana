using System.Collections;
using System.Reflection;

namespace Gondwana.Tooling.Editing;

/// <summary>A temporary snapshot of an affected collection editor's object graph. History retains only changed properties and collection membership.</summary>
public sealed class ModelSnapshot
{
    private readonly List<(object Target, PropertyInfo Property, object? Value)> _properties = [];
    private readonly List<(IList List, object?[] Items)> _lists = [];

    /// <summary>Captures an affected value before a modal editor can mutate it in place.</summary>
    public ModelSnapshot(object? value)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Visit(object? target)
        {
            if (target is null || target is string || target.GetType().IsValueType || !visited.Add(target)) return;
            if (target is IList list)
            {
                _lists.Add((list, list.Cast<object?>().ToArray()));
                foreach (var item in list) Visit(item);
                return;
            }
            foreach (var property in target.GetType().GetProperties().Where(p => p.CanRead && p.SetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0 && p.Name != "Source"))
            {
                var current = property.GetValue(target);
                _properties.Add((target, property, current));
                Visit(current);
            }
        }
        Visit(value);
    }

    /// <summary>Restores the captured values and membership while retaining original instances.</summary>
    public void Restore()
    {
        foreach (var (target, property, value) in _properties) property.SetValue(target, value);
        foreach (var (list, items) in _lists) Replace(list, items);
    }

    /// <summary>Records only actual differences as one reversible operation.</summary>
    public void Record(EditHistory history, string description, Func<object, string, object?, object?>? restoreValue = null)
    {
        history.RequireIdle(allowTransaction: true);
        var candidates = _properties.Select(p => (p.Target, p.Property, Before: p.Value, After: p.Property.GetValue(p.Target))).ToArray();
        var changedTargets = candidates.Where(p => !Equals(p.Before, p.After)).Select(p => p.Target).ToHashSet(ReferenceEqualityComparer.Instance);
        // A supplied value adapter may need companion fields (for example an implicit archive path).
        var properties = candidates.Where(p => restoreValue is null ? !Equals(p.Before, p.After) : changedTargets.Contains(p.Target)).ToArray();
        var lists = _lists.Select(p => (p.List, Before: p.Items, After: p.List.Cast<object?>().ToArray()))
            .Where(p => !p.Before.SequenceEqual(p.After)).ToArray();
        Restore();
        if (properties.Length == 0 && lists.Length == 0) return;
        history.Execute(description, () =>
        {
            foreach (var p in properties) p.Property.SetValue(p.Target, restoreValue is null ? p.After : restoreValue(p.Target, p.Property.Name, p.After));
            foreach (var p in lists) Replace(p.List, p.After);
        }, () =>
        {
            foreach (var p in properties) p.Property.SetValue(p.Target, restoreValue is null ? p.Before : restoreValue(p.Target, p.Property.Name, p.Before));
            foreach (var p in lists) Replace(p.List, p.Before);
        });
    }

    private static void Replace(IList list, object?[] items)
    {
        if (list.IsFixedSize) { for (int i = 0; i < items.Length; i++) list[i] = items[i]; }
        else { list.Clear(); foreach (var item in items) list.Add(item); }
    }
}
