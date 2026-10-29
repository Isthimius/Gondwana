using Gondwana.Tooling.Editing;

namespace Gondwana.Tests;

public class EditHistoryTests
{
    [Theory]
    [InlineData("dispose")]
    [InlineData("clear")]
    [InlineData("branch")]
    [InlineData("limit")]
    public void DiscardingCommandsReleasesRetainedModelObjects(string mode)
    {
        using var history = new EditHistory(limit: 1);
        var weak = Retain(history);
        switch (mode)
        {
            case "dispose": history.Dispose(); break;
            case "clear": history.Clear(); break;
            case "branch": history.Undo(); history.Execute("New branch", () => { }, () => { }); break;
            case "limit": history.Execute("Evict", () => { }, () => { }); break;
        }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(weak.IsAlive);
        GC.KeepAlive(history);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference Retain(EditHistory history)
    {
        var model = new object();
        history.Execute("Retained", () => GC.KeepAlive(model), () => GC.KeepAlive(model));
        return new WeakReference(model);
    }

    [Fact]
    public void CollectionCommandsPreserveDuplicateValuesAndSnapshotRestoresNestedObjects()
    {
        using var h = new EditHistory();
        var list = new List<string> { "same" };
        h.Insert("Duplicate", list, 1, "same"); Assert.Equal(2, list.Count);
        h.Undo(); Assert.Single(list); h.Redo(); Assert.Equal(2, list.Count);
        var item = new MutableValue { Value = 1 };
        var models = new List<MutableValue> { item };
        var snapshot = new ModelSnapshot(models);
        item.Value = 2; models.Add(new() { Value = 3 });
        snapshot.Record(h, "Collection");
        h.Undo(); Assert.Same(item, Assert.Single(models)); Assert.Equal(1, item.Value);
        h.Redo(); Assert.Equal(2, item.Value); Assert.Equal(2, models.Count);
    }

    private sealed class MutableValue { public int Value { get; set; } }

    [Fact]
    public void CheckpointsSurviveUndoRedoAndDivergence()
    {
        using var h = new EditHistory();
        int value = 0;
        h.Set("A", () => value, v => value = v, 1);
        h.MarkSaved();
        h.Set("B", () => value, v => value = v, 2);
        h.Undo(); Assert.Equal(1, value); Assert.False(h.IsDirty);
        h.Undo(); Assert.Equal(0, value); Assert.True(h.IsDirty);
        h.Redo(); Assert.False(h.IsDirty);
        h.Set("C", () => value, v => value = v, 3);
        Assert.False(h.CanRedo); Assert.Equal("C", h.UndoDescription);
        h.Undo(); h.Undo(); h.MarkSaved(); h.Redo(); Assert.True(h.IsDirty);
    }

    [Fact]
    public void DiscardedSavedBranchAndEvictedCheckpointNeverBecomeClean()
    {
        using var h = new EditHistory(limit: 2);
        int value = 0;
        void Set(int v) => h.Set("Set", () => value, n => value = n, v);
        Set(1); Set(2); h.MarkSaved(); h.Undo(); Set(3);
        Assert.True(h.IsDirty); h.Undo(); Assert.True(h.IsDirty);
        Set(4); Set(5); Set(6); h.Undo(); h.Undo();
        Assert.True(h.IsDirty); Assert.False(h.CanUndo);
    }

    [Fact]
    public void TransactionsGroupNotifyOnceAndCancelInReverseOrder()
    {
        using var h = new EditHistory();
        var values = new List<int>();
        int notifications = 0;
        h.Changed += (_, _) => notifications++;
        using (var group = h.BeginTransaction("Many edits"))
        {
            h.Insert("One", values, 0, 1);
            using (var child = h.BeginTransaction("Nested")) { h.Insert("Two", values, 1, 2); child.Commit(); }
            Assert.True(h.IsDirty); Assert.False(h.CanUndo); Assert.Equal(0, notifications);
            group.Commit();
        }
        Assert.Equal(1, notifications); Assert.Equal("Many edits", h.UndoDescription);
        h.Undo(); Assert.Empty(values); Assert.False(h.IsDirty);
        h.Redo(); Assert.Equal(new[] { 1, 2 }, values);
        using (h.BeginTransaction("Cancelled")) { h.Remove("Remove", values, 1); h.Insert("Three", values, 0, 3); }
        Assert.Equal(new[] { 1, 2 }, values);
    }

    [Fact]
    public void NoOpsAndEmptyTransactionsDoNotCreateEntries()
    {
        using var h = new EditHistory();
        int value = 0;
        Assert.False(h.Set("No change", () => value, v => value = v, 0));
        h.Execute("Known no-op", () => throw new Exception("Must not execute"), () => { }, hasChanges: false);
        using (var t = h.BeginTransaction("Empty")) t.Commit();
        Assert.False(h.CanUndo); Assert.False(h.IsDirty);
    }

    [Fact]
    public void FailureRollsBackCommandAndPendingGroup()
    {
        using var h = new EditHistory();
        int value = 0;
        using var t = h.BeginTransaction("Fails");
        h.Set("First", () => value, v => value = v, 1);
        Assert.Throws<InvalidOperationException>(() => h.Execute("Fail", () => { value = 2; throw new InvalidOperationException(); }, () => value = 1));
        Assert.Equal(0, value); Assert.False(h.InTransaction); Assert.False(h.IsDirty); Assert.False(h.CanUndo);
    }

    [Fact]
    public void FailedGroupRedoCompensatesOnlyAttemptedCommands()
    {
        using var h = new EditHistory();
        var values = new List<int>();
        bool fail = false;
        using (var t = h.BeginTransaction("Group"))
        {
            h.Execute("First", () => values.Add(1), () => values.RemoveAt(values.Count - 1));
            h.Execute("Second", () => { values.Add(2); if (fail) throw new InvalidOperationException(); }, () => values.RemoveAt(values.Count - 1));
            h.Execute("Third", () => values.Add(3), () => values.RemoveAt(values.Count - 1));
            t.Commit();
        }
        h.Undo(); fail = true;
        Assert.Throws<InvalidOperationException>(() => h.Redo());
        Assert.Empty(values); Assert.True(h.CanRedo); Assert.False(h.IsDirty);
        fail = false; h.Redo(); Assert.Equal(new[] { 1, 2, 3 }, values);
    }

    [Fact]
    public void ReplayCannotRecordCommandsAndFailureToRecoverInvalidatesHistory()
    {
        using var h = new EditHistory();
        Assert.Throws<InvalidOperationException>(() => h.Execute("Recursive", () => h.Execute("Inner", () => { }, () => { }), () => { }));
        Assert.False(h.CanUndo);
        using var other = new EditHistory();
        Assert.Throws<InvalidOperationException>(() => h.Execute("Cross-document", () => other.Execute("Other", () => { }, () => { }), () => { }));
        Assert.False(other.CanUndo);
        Assert.ThrowsAny<AggregateException>(() => h.Execute("Broken", () => throw new Exception(), () => throw new Exception()));
        Assert.True(h.IsDirty); Assert.False(h.CanUndo);
    }

    [Fact]
    public void HistoriesAreIndependentAndDisposalCancelsPendingChanges()
    {
        using var other = new EditHistory();
        var h = new EditHistory(initiallySaved: false);
        int value = 0;
        h.BeginTransaction("Pending"); h.Set("Set", () => value, v => value = v, 1);
        h.Dispose(); h.Dispose();
        Assert.Equal(0, value); Assert.False(h.CanUndo); Assert.False(other.IsDirty);
        Assert.Throws<ObjectDisposedException>(() => h.MarkSaved());
    }
}
