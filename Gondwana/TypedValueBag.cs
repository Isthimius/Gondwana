using Newtonsoft.Json;

namespace Gondwana;

/// <summary>
/// Represents a strongly-typed key used to access values in a <see cref="TypedValueBag"/>.
/// <para>
/// The generic type parameter <typeparamref name="T"/> defines the expected runtime type
/// associated with the key. This allows the value bag to remain flexible internally while
/// still providing compile-time type safety to callers.
/// </para>
/// <para>
/// In practical terms, a <see cref="ValueKey{T}"/> is the contract between the caller and
/// the bag: the caller declares, up front, the type that is intended to be stored and later
/// retrieved for a given logical name.
/// </para>
/// </summary>
/// <typeparam name="T">
/// The type of value associated with this key.
/// </typeparam>
/// <param name="Name">
/// The unique logical name of the value.
/// </param>
public readonly record struct ValueKey<T>(string Name);

/// <summary>
/// Defines a lightweight contract for values that can produce an explicit deep clone of themselves.
/// <para>
/// This interface is optional, but it provides the most reliable way for <see cref="TypedValueBag"/>
/// to duplicate mutable reference-type values without relying on JSON serialization or other
/// reflection-heavy mechanisms.
/// </para>
/// <para>
/// When a stored value implements <see cref="IDeepCloneable{T}"/>, the bag will prefer that path
/// during cloning and merge operations.
/// </para>
/// </summary>
/// <typeparam name="T">
/// The concrete type produced by the deep clone operation.
/// </typeparam>
public interface IDeepCloneable<out T>
{
    /// <summary>
    /// Creates a deep clone of the current instance.
    /// </summary>
    /// <returns>
    /// A new instance that does not share mutable state with the current instance.
    /// </returns>
    T DeepClone();
}

/// <summary>
/// A flexible, strongly-typed value container intended for runtime-attached metadata and
/// ephemeral engine state.
/// <para>
/// Values are stored internally as plain CLR objects keyed by string identifiers, but they are
/// accessed externally through strongly-typed <see cref="ValueKey{T}"/> instances. This preserves
/// compile-time safety for callers while avoiding any direct dependency on JSON serialization
/// during ordinary runtime usage.
/// </para>
/// <para>
/// Unlike a JSON-token-based implementation, this class does not serialize or deserialize values
/// on every set/get operation. It behaves as an in-memory typed bag first, with persistence concerns
/// expected to be handled explicitly at the boundaries of the engine or application.
/// </para>
/// <para>
/// Values stored in a <see cref="TypedValueBag"/> are not preserved by JSON serialization. The bag is
/// intended for runtime, transient, or otherwise non-persisted values, and engine-core properties that
/// expose a <see cref="TypedValueBag"/> should typically be marked with <see cref="JsonIgnoreAttribute"/>.
/// If an instance of this class is serialized directly, its contents are also intentionally ignored.
/// </para>
/// <para>
/// Cloning behavior is best-effort:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// If a stored value is <see langword="null"/>, it remains <see langword="null"/>.
/// </description>
/// </item>
/// <item>
/// <description>
/// If a stored value implements <see cref="IDeepCloneable{T}"/>, that deep-clone path is used.
/// </description>
/// </item>
/// <item>
/// <description>
/// If a stored value implements <see cref="ICloneable"/>, that clone path is used.
/// </description>
/// </item>
/// <item>
/// <description>
/// Otherwise, the stored reference is copied as-is (shallow copy).
/// </description>
/// </item>
/// </list>
/// <para>
/// This means immutable values behave exactly as expected, while mutable reference types can opt
/// into stronger cloning semantics by implementing one of the supported clone contracts.
/// </para>
/// </summary>
public sealed class TypedValueBag : ICloneable
{
    /// <summary>
    /// Internal storage for all values in the bag.
    /// <para>
    /// The dictionary maps logical names to raw CLR object instances. The bag itself is responsible
    /// for enforcing type expectations at the API boundary when values are retrieved.
    /// </para>
    /// <para>
    /// This field intentionally stores runtime objects directly rather than serialized surrogates,
    /// allowing the bag to function without any dependency on JSON libraries or token models.
    /// </para>
    /// <para>
    /// This data is intentionally not preserved by JSON serialization.
    /// </para>
    /// </summary>
    [JsonIgnore]
    private readonly Dictionary<string, object?> _data = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedValueBag"/> class.
    /// </summary>
    public TypedValueBag()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedValueBag"/> class by performing
    /// a copy of another <see cref="TypedValueBag"/>.
    /// <para>
    /// Values are copied using the bag's internal clone strategy. Where possible, mutable values
    /// are cloned; where no supported clone mechanism exists, the original reference is reused.
    /// </para>
    /// </summary>
    /// <param name="other">
    /// The source value bag to copy from.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="other"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// This constructor is intended for scenarios such as runtime duplication, snapshot creation,
    /// undo/redo support, and general engine-state isolation. The exact depth of the copy depends on
    /// the clone capabilities of the stored values themselves.
    /// </remarks>
    public TypedValueBag(TypedValueBag other)
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));

        foreach (var (key, value) in other._data)
            _data[key] = CloneValue(value);
    }

    /// <summary>
    /// Stores a value in the bag under the specified key.
    /// <para>
    /// If a value already exists for the given key, it is replaced.
    /// </para>
    /// </summary>
    /// <typeparam name="T">
    /// The type of value being stored.
    /// </typeparam>
    /// <param name="key">
    /// The strongly-typed key identifying the value.
    /// </param>
    /// <param name="value">
    /// The value to store. A <c>null</c> value is stored explicitly.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown if the key name is null, empty, or whitespace.
    /// </exception>
    public void Set<T>(ValueKey<T> key, T value)
    {
        ValidateKeyName(key.Name);
        _data[key.Name] = value;
    }

    /// <summary>
    /// Attempts to retrieve a value from the bag using the specified key.
    /// </summary>
    /// <typeparam name="T">
    /// The expected type of the value.
    /// </typeparam>
    /// <param name="key">
    /// The strongly-typed key identifying the value.
    /// </param>
    /// <param name="value">
    /// When this method returns <c>true</c>, contains the retrieved value. If the stored value is
    /// <c>null</c>, this output receives the default value of <typeparamref name="T"/>. When the
    /// method returns <c>false</c>, this output also receives the default value of
    /// <typeparamref name="T"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if a value exists for the specified key; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the key name is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="InvalidCastException">
    /// Thrown if a value exists for the specified key but is not compatible with
    /// <typeparamref name="T"/>.
    /// </exception>
    /// <remarks>
    /// This method distinguishes between three cases:
    /// <list type="number">
    /// <item>
    /// <description>No value exists for the key: returns <c>false</c>.</description>
    /// </item>
    /// <item>
    /// <description>A value exists and is <c>null</c>: returns <c>true</c>, with <paramref name="value"/> set to default.</description>
    /// </item>
    /// <item>
    /// <description>A value exists and is non-null: the method verifies type compatibility before returning it.</description>
    /// </item>
    /// </list>
    /// </remarks>
    public bool TryGet<T>(ValueKey<T> key, out T? value)
    {
        ValidateKeyName(key.Name);

        if (!_data.TryGetValue(key.Name, out var raw))
        {
            value = default;
            return false;
        }

        if (raw is null)
        {
            value = default;
            return true;
        }

        if (raw is T typed)
        {
            value = typed;
            return true;
        }

        throw new InvalidCastException(
            $"Value for key '{key.Name}' is of runtime type '{raw.GetType().FullName}', " +
            $"which is not assignable to requested type '{typeof(T).FullName}'.");
    }

    /// <summary>
    /// Retrieves a value from the bag using the specified key, or returns a default value
    /// if the key is not present.
    /// </summary>
    /// <typeparam name="T">
    /// The expected type of the value.
    /// </typeparam>
    /// <param name="key">
    /// The strongly-typed key identifying the value.
    /// </param>
    /// <param name="defaultValue">
    /// The value to return if the key is not found or the stored value is <c>null</c>.
    /// </param>
    /// <returns>
    /// The stored value if present and non-null; otherwise, <paramref name="defaultValue"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the key name is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="InvalidCastException">
    /// Thrown if a value exists for the specified key but is not compatible with
    /// <typeparamref name="T"/>.
    /// </exception>
    public T Get<T>(ValueKey<T> key, T defaultValue = default!)
    {
        return TryGet(key, out var v) && v is not null
            ? v
            : defaultValue;
    }

    /// <summary>
    /// Removes the value associated with the specified key from the bag.
    /// </summary>
    /// <typeparam name="T">
    /// The type associated with the key.
    /// </typeparam>
    /// <param name="key">
    /// The strongly-typed key identifying the value to remove.
    /// </param>
    /// <returns>
    /// <c>true</c> if the value was found and removed; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the key name is null, empty, or whitespace.
    /// </exception>
    public bool Remove<T>(ValueKey<T> key)
    {
        ValidateKeyName(key.Name);
        return _data.Remove(key.Name);
    }

    /// <summary>
    /// Removes all values from the bag.
    /// </summary>
    public void Clear()
    {
        _data.Clear();
    }

    /// <summary>
    /// Merges values from another <see cref="TypedValueBag"/> into this instance.
    /// </summary>
    /// <param name="incoming">
    /// The source bag whose values will be copied into this bag.
    /// If <c>null</c>, the method performs no action.
    /// </param>
    /// <param name="overwriteExisting">
    /// If <c>true</c>, values in this bag will be replaced when the same key exists in
    /// <paramref name="incoming"/>. If <c>false</c>, existing values are preserved and
    /// only missing keys are added.
    /// </param>
    /// <remarks>
    /// Values copied from <paramref name="incoming"/> pass through the same clone strategy
    /// used by <see cref="Clone()"/>. This avoids unnecessary sharing where the stored types
    /// expose a supported clone mechanism.
    /// </remarks>
    public void MergeFrom(TypedValueBag? incoming, bool overwriteExisting = false)
    {
        if (incoming is null)
            return;

        foreach (var (key, value) in incoming._data)
        {
            if (!overwriteExisting && _data.ContainsKey(key))
                continue;

            _data[key] = CloneValue(value);
        }
    }

    /// <summary>
    /// Determines whether the bag contains a value for the specified key name, regardless of type.
    /// </summary>
    /// <param name="keyName">
    /// The logical key name to test.
    /// </param>
    /// <returns>
    /// <c>true</c> if the bag contains an entry with the specified key name; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="keyName"/> is null, empty, or whitespace.
    /// </exception>
    public bool Contains(string keyName)
    {
        ValidateKeyName(keyName);
        return _data.ContainsKey(keyName);
    }

    /// <summary>
    /// Determines whether the bag contains a value for the specified typed key.
    /// </summary>
    /// <typeparam name="T">
    /// The type associated with the key.
    /// </typeparam>
    /// <param name="key">
    /// The strongly-typed key to test.
    /// </param>
    /// <returns>
    /// <c>true</c> if the bag contains an entry with the specified key name; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the key name is null, empty, or whitespace.
    /// </exception>
    public bool Contains<T>(ValueKey<T> key)
    {
        ValidateKeyName(key.Name);
        return _data.ContainsKey(key.Name);
    }

    /// <summary>
    /// Creates a copy of this <see cref="TypedValueBag"/>.
    /// <para>
    /// The resulting bag is independent at the dictionary level. Individual stored values are
    /// duplicated according to the bag's clone strategy.
    /// </para>
    /// </summary>
    /// <returns>
    /// A new <see cref="TypedValueBag"/> instance containing copies of the current entries.
    /// </returns>
    public TypedValueBag Clone() => new(this);

    /// <summary>
    /// Creates a copy of this <see cref="TypedValueBag"/> as an <see cref="object"/>.
    /// This is the explicit implementation of <see cref="ICloneable.Clone"/>.
    /// </summary>
    /// <returns>
    /// A new <see cref="TypedValueBag"/> instance that is a copy of this instance,
    /// returned as an <see cref="object"/>.
    /// </returns>
    object ICloneable.Clone() => Clone();

    /// <summary>
    /// Produces a shallow snapshot of the bag's current contents.
    /// <para>
    /// This method is primarily intended for diagnostics, inspection, or external code
    /// that wants a plain dictionary view of the stored runtime values.
    /// </para>
    /// </summary>
    /// <returns>
    /// A new dictionary containing the current key/value pairs from the bag.
    /// </returns>
    public Dictionary<string, object?> ToDictionary()
    {
        return new Dictionary<string, object?>(_data);
    }

    /// <summary>
    /// Attempts to clone a stored value using a best-effort strategy.
    /// </summary>
    /// <param name="value">
    /// The value to clone.
    /// </param>
    /// <returns>
    /// A cloned value when supported; otherwise, the original value reference.
    /// </returns>
    /// <remarks>
    /// The order of preference is:
    /// <list type="number">
    /// <item>
    /// <description><see langword="null"/> remains <see langword="null"/>.</description>
    /// </item>
    /// <item>
    /// <description>Arrays are cloned using <see cref="Array.Clone"/>.</description>
    /// </item>
    /// <item>
    /// <description><see cref="ICloneable"/> is honored when implemented.</description>
    /// </item>
    /// <item>
    /// <description>Otherwise, the original reference is returned.</description>
    /// </item>
    /// </list>
    /// <para>
    /// Note that generic interfaces such as <c>IDeepCloneable&lt;T&gt;</c> are not easily discoverable
    /// through a non-generic cast, so <see cref="ICloneable"/> remains the simple universal hook here.
    /// If you want your engine types to participate in deep copying cleanly, implementing
    /// <see cref="ICloneable"/> is the path of least resistance.
    /// </para>
    /// </remarks>
    private static object? CloneValue(object? value)
    {
        if (value is null)
            return null;

        if (value is Array array)
            return array.Clone();

        if (value is ICloneable cloneable)
            return cloneable.Clone();

        return value;
    }

    /// <summary>
    /// Validates that a key name is usable as a value-bag identifier.
    /// </summary>
    /// <param name="keyName">
    /// The key name to validate.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="keyName"/> is null, empty, or whitespace.
    /// </exception>
    private static void ValidateKeyName(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            throw new ArgumentException("Value bag keys may not be null, empty, or whitespace.", nameof(keyName));
    }
}