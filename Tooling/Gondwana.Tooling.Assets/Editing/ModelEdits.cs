namespace Gondwana.Tooling.Editing;

/// <summary>Focused model operations retaining instances rather than serializing document graphs.</summary>
public static class ModelEdits
{
    /// <summary>Captures changes to writable properties of one object. Mutations must replace values, not mutate child objects or collections.</summary>
    public static bool Edit(this EditHistory history, string description, object target, Action change)
    {
        history.RequireIdle(allowTransaction: true);
        var properties = target.GetType().GetProperties().Where(p => p.CanRead && p.SetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0).ToArray();
        var before = properties.Select(p => p.GetValue(target)).ToArray();
        object?[] after;
        try { history.Capture(change); after = properties.Select(p => p.GetValue(target)).ToArray(); }
        finally { for (int i = 0; i < properties.Length; i++) properties[i].SetValue(target, before[i]); }
        var changed = Enumerable.Range(0, properties.Length).Where(i => !Equals(before[i], after[i])).ToArray();
        if (changed.Length == 0) return false;
        // Retain only modified properties. In particular, replay must not restore old save provenance.
        var edits = changed.Select(i => (Property: properties[i], Before: before[i], After: after[i])).ToArray();
        history.Execute(description,
            () => { foreach (var edit in edits) edit.Property.SetValue(target, edit.After); },
            () => { foreach (var edit in edits) edit.Property.SetValue(target, edit.Before); });
        return true;
    }

    /// <summary>Inserts the same instance at a stable position on redo.</summary>
    public static void Insert<T>(this EditHistory history, string description, IList<T> list, int index, T item)
    {
        bool inserted = false;
        history.Execute(description,
            () => { if (!inserted) { list.Insert(index, item); inserted = true; } },
            () => { if (inserted) { list.RemoveAt(index); inserted = false; } });
    }

    /// <summary>Removes an instance and restores its original position on undo.</summary>
    public static bool Remove<T>(this EditHistory history, string description, IList<T> list, T item)
    {
        int index = list.IndexOf(item);
        if (index < 0) return false;
        bool removed = false;
        history.Execute(description,
            () => { if (!removed) { list.RemoveAt(index); removed = true; } },
            () => { if (removed) { list.Insert(index, item); removed = false; } });
        return true;
    }

    /// <summary>Removes matching instances as one transaction, retaining their order.</summary>
    public static int RemoveWhere<T>(this EditHistory history, string description, IList<T> list, Func<T, bool> predicate)
    {
        var items = list.Where(predicate).ToArray();
        if (items.Length == 0) return 0;
        using var transaction = history.BeginTransaction(description);
        foreach (var item in items) history.Remove(description, list, item);
        transaction.Commit();
        return items.Length;
    }
}
