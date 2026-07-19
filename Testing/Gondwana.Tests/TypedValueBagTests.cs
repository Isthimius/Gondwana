using Gondwana;

namespace Gondwana.Tests;

/// <summary>
/// Contains unit tests for the <see cref="TypedValueBag"/> class.
/// </summary>
public sealed class TypedValueBagTests
{
    /// <summary>
    /// Verifies that a stored value can be retrieved with the same key.
    /// </summary>
    [Fact]
    public void SetAndGet_ReturnsStoredValue()
    {
        var bag = new TypedValueBag();
        var key = new ValueKey<int>("score");

        bag.Set(key, 42);

        Assert.Equal(42, bag.Get(key));
    }

    /// <summary>
    /// Verifies that attempting to retrieve a missing key returns <see langword="false"/>.
    /// </summary>
    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var bag = new TypedValueBag();

        var found = bag.TryGet(new ValueKey<string>("missing"), out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    /// <summary>
    /// Verifies that retrieving a stored null reference succeeds and returns a null value.
    /// </summary>
    [Fact]
    public void TryGet_NullStoredReference_ReturnsTrueWithNull()
    {
        var bag = new TypedValueBag();
        var key = new ValueKey<string?>("name");
        bag.Set(key, null);

        var found = bag.TryGet(key, out var value);

        Assert.True(found);
        Assert.Null(value);
    }

    /// <summary>
    /// Verifies that getting a missing or null value returns the provided default.
    /// </summary>
    [Fact]
    public void Get_MissingOrNull_ReturnsDefault()
    {
        var bag = new TypedValueBag();
        var missingKey = new ValueKey<string>("missing");
        var nullKey = new ValueKey<string?>("nullable");
        bag.Set(nullKey, null);

        Assert.Equal("fallback", bag.Get(missingKey, "fallback"));
        Assert.Equal("fallback", bag.Get(new ValueKey<string?>("nullable"), "fallback"));
    }

    /// <summary>
    /// Verifies that retrieving a value through an incompatible key type throws an <see cref="InvalidCastException"/>.
    /// </summary>
    [Fact]
    public void TryGet_IncompatibleType_ThrowsInvalidCastException()
    {
        var bag = new TypedValueBag();
        bag.Set(new ValueKey<int>("score"), 5);

        Assert.Throws<InvalidCastException>(() => bag.TryGet(new ValueKey<string>("score"), out _));
    }

    /// <summary>
    /// Verifies that contains and remove operations reflect the current contents of the bag.
    /// </summary>
    [Fact]
    public void ContainsAndRemove_WorkAsExpected()
    {
        var bag = new TypedValueBag();
        var key = new ValueKey<int>("hp");
        bag.Set(key, 12);

        Assert.True(bag.Contains("hp"));
        Assert.True(bag.Contains(key));
        Assert.True(bag.Remove(key));
        Assert.False(bag.Remove(key));
        Assert.False(bag.Contains("hp"));
    }

    /// <summary>
    /// Verifies that clearing the bag removes all stored values.
    /// </summary>
    [Fact]
    public void Clear_RemovesAllValues()
    {
        var bag = new TypedValueBag();
        bag.Set(new ValueKey<int>("a"), 1);
        bag.Set(new ValueKey<int>("b"), 2);

        bag.Clear();

        Assert.False(bag.Contains("a"));
        Assert.False(bag.Contains("b"));
    }

    /// <summary>
    /// Verifies that merging from a null bag leaves the current bag unchanged.
    /// </summary>
    [Fact]
    public void MergeFrom_NullInput_DoesNothing()
    {
        var bag = new TypedValueBag();
        bag.Set(new ValueKey<int>("x"), 1);

        bag.MergeFrom(null);

        Assert.Equal(1, bag.Get(new ValueKey<int>("x")));
    }

    /// <summary>
    /// Verifies that merge behavior respects the overwriteExisting flag.
    /// </summary>
    [Fact]
    public void MergeFrom_RespectsOverwriteFlag()
    {
        var target = new TypedValueBag();
        target.Set(new ValueKey<int>("x"), 1);
        target.Set(new ValueKey<int>("existingOnly"), 7);

        var incoming = new TypedValueBag();
        incoming.Set(new ValueKey<int>("x"), 2);
        incoming.Set(new ValueKey<int>("incomingOnly"), 9);

        target.MergeFrom(incoming, overwriteExisting: false);
        Assert.Equal(1, target.Get(new ValueKey<int>("x")));
        Assert.Equal(9, target.Get(new ValueKey<int>("incomingOnly")));
        Assert.Equal(7, target.Get(new ValueKey<int>("existingOnly")));

        target.MergeFrom(incoming, overwriteExisting: true);
        Assert.Equal(2, target.Get(new ValueKey<int>("x")));
    }

    /// <summary>
    /// Verifies that cloning copies arrays and cloneable values while preserving shared non-cloneable references.
    /// </summary>
    [Fact]
    public void Clone_CopiesArrayAndCloneableValues()
    {
        var bag = new TypedValueBag();
        var arrayKey = new ValueKey<int[]?>("arr");
        var cloneableKey = new ValueKey<CloneableCounter?>("cloneable");
        var holderKey = new ValueKey<NonCloneableHolder?>("holder");
        var numbers = new[] { 1, 2, 3 };
        var cloneable = new CloneableCounter(5);
        var nonCloneable = new NonCloneableHolder(9);
        bag.Set(arrayKey, numbers);
        bag.Set(cloneableKey, cloneable);
        bag.Set(holderKey, nonCloneable);

        var cloned = bag.Clone();
        var clonedArray = cloned.Get(arrayKey);
        var clonedCloneable = cloned.Get(cloneableKey);
        var clonedHolder = cloned.Get(holderKey);

        Assert.NotNull(clonedArray);
        Assert.NotNull(clonedCloneable);
        Assert.NotNull(clonedHolder);
        Assert.NotSame(numbers, clonedArray);
        Assert.Equal(numbers, clonedArray);
        Assert.NotSame(cloneable, clonedCloneable);
        Assert.Equal(5, clonedCloneable.Value);
        Assert.Same(nonCloneable, clonedHolder);
    }

    /// <summary>
    /// Verifies that <see cref="TypedValueBag.ToDictionary"/> returns a detached copy of the stored values.
    /// </summary>
    [Fact]
    public void ToDictionary_ReturnsDetachedCopy()
    {
        var bag = new TypedValueBag();
        bag.Set(new ValueKey<int>("score"), 10);

        var snapshot = bag.ToDictionary();
        snapshot["score"] = 99;

        Assert.Equal(10, bag.Get(new ValueKey<int>("score")));
    }

    /// <summary>
    /// Verifies that invalid key names throw an <see cref="ArgumentException"/> in bag operations.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void InvalidKeyName_ThrowsArgumentException(string invalidName)
    {
        var bag = new TypedValueBag();

        Assert.Throws<ArgumentException>(() => bag.Set(new ValueKey<int>(invalidName), 1));
        Assert.Throws<ArgumentException>(() => bag.TryGet(new ValueKey<int>(invalidName), out _));
        Assert.Throws<ArgumentException>(() => bag.Remove(new ValueKey<int>(invalidName)));
        Assert.Throws<ArgumentException>(() => bag.Contains(invalidName));
    }

    private sealed class CloneableCounter(int value) : ICloneable
    {
        /// <summary>Gets the integer value stored in this counter.</summary>
        public int Value { get; } = value;

        /// <summary>Creates a new <see cref="CloneableCounter"/> with the same value.</summary>
        /// <returns>A new <see cref="CloneableCounter"/> instance with an identical <see cref="Value"/>.</returns>
        public object Clone() => new CloneableCounter(Value);
    }

    private sealed class NonCloneableHolder(int value)
    {
        /// <summary>Gets the integer value stored in this holder.</summary>
        public int Value { get; } = value;
    }
}
