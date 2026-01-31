using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gondwana;

/// <summary>
/// Represents a strongly-typed key used to access values in a <see cref="TypedValueBag"/>.
/// <para>
/// The generic type parameter <typeparamref name="T"/> defines the expected value type
/// associated with the key, enabling compile-time type safety while allowing the underlying
/// storage to remain flexible.
/// </para>
/// </summary>
/// <typeparam name="T">
/// The type of value associated with this key.
/// </typeparam>
public readonly record struct ValueKey<T>(string Name);

/// <summary>
/// A flexible, strongly-typed value container intended for save-state extensibility.
/// <para>
/// Values are stored internally as JSON tokens keyed by string identifiers, but accessed
/// externally via strongly-typed <see cref="ValueKey{T}"/> instances to ensure compile-time
/// safety.
/// </para>
/// <para>
/// This class is designed to be serialized as part of an engine or game save file,
/// while allowing individual projects or modules to attach arbitrary structured data
/// without modifying core engine state.
/// </para>
/// </summary>
public sealed class TypedValueBag : ICloneable
{
    /// <summary>
    /// Internal storage for all values in the bag.
    /// <para>
    /// This dictionary is serialized directly. Each entry represents a named value
    /// stored as a JSON token.
    /// </para>
    /// </summary>
    [JsonProperty]
    private readonly Dictionary<string, JToken> _data = new();

    /// <summary>
    /// The JSON serializer used to convert values to and from <see cref="JToken"/> instances.
    /// <para>
    /// This field is not serialized and exists only to ensure consistent serialization
    /// behavior during runtime access.
    /// </para>
    /// </summary>
    [JsonIgnore]
    private readonly JsonSerializer _serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedValueBag"/> class using the default
    /// JSON serializer settings.
    /// </summary>
    public TypedValueBag()
        : this(JsonSerializer.CreateDefault())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedValueBag"/> class using a custom
    /// JSON serializer.
    /// </summary>
    /// <param name="serializer">
    /// The serializer to use when converting values to and from JSON tokens.
    /// </param>
    public TypedValueBag(JsonSerializer serializer)
    {
        _serializer = serializer;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypedValueBag"/> class by performing
    /// a deep copy of another <see cref="TypedValueBag"/>.
    /// </summary>
    /// <param name="other">
    /// The source value bag to copy from.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="other"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// All values are cloned at the underlying JSON token level, ensuring that the
    /// newly created bag does not share mutable state with the source instance.
    /// <para>
    /// This constructor is intended for scenarios such as engine state cloning,
    /// snapshot restoration, or asset duplication where isolation between value bags
    /// is required.
    /// </para>
    /// </remarks>
    public TypedValueBag(TypedValueBag other)
        : this(other?._serializer ?? throw new ArgumentNullException(nameof(other)))
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));

        foreach (var (key, token) in other._data)
            _data[key] = token.DeepClone();
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
    /// The value to store. A <c>null</c> value is stored explicitly as JSON null.
    /// </param>
    public void Set<T>(ValueKey<T> key, T value)
    {
        _data[key.Name] = value is null
            ? JValue.CreateNull()
            : JToken.FromObject(value, _serializer);
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
    /// When this method returns <c>true</c>, contains the retrieved value or
    /// the default value of <typeparamref name="T"/> if the stored value is JSON null.
    /// </param>
    /// <returns>
    /// <c>true</c> if a value exists for the specified key; otherwise, <c>false</c>.
    /// </returns>
    public bool TryGet<T>(ValueKey<T> key, out T? value)
    {
        if (_data.TryGetValue(key.Name, out var token))
        {
            value = token.Type == JTokenType.Null
                ? default
                : token.ToObject<T>(_serializer);

            return true;
        }

        value = default;
        return false;
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
    /// The value to return if the key is not found or the stored value is JSON null.
    /// </param>
    /// <returns>
    /// The stored value if present; otherwise, <paramref name="defaultValue"/>.
    /// </returns>
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
    public bool Remove<T>(ValueKey<T> key)
    {
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
    /// If <c>true</c>, values in this bag will be replaced when the same key
    /// exists in <paramref name="incoming"/>. If <c>false</c>, existing values
    /// are preserved and only missing keys are added.
    /// </param>
    /// <remarks>
    /// Values are merged at the underlying token level and cloned before insertion,
    /// ensuring the two bags do not share mutable state.
    /// </remarks>
    public void MergeFrom(TypedValueBag? incoming, bool overwriteExisting = false)
    {
        if (incoming is null)
            return;

        foreach (var (key, token) in incoming._data)
        {
            if (!overwriteExisting && _data.ContainsKey(key))
                continue;

            _data[key] = token.DeepClone();
        }
    }

    /// <summary>
    /// Creates a deep copy of this <see cref="TypedValueBag"/> (tokens are cloned).
    /// </summary>
    public TypedValueBag Clone() => new(this);

    /// <summary>
    /// Creates a deep copy of this <see cref="TypedValueBag"/> as an <see cref="object"/>.
    /// This is the explicit implementation of <see cref="ICloneable.Clone"/>.
    /// </summary>
    /// <returns>
    /// A new <see cref="TypedValueBag"/> instance that is a deep copy of this instance,
    /// returned as an <see cref="object"/>.
    /// </returns>
    /// <remarks>
    /// This method delegates to the strongly-typed <see cref="Clone"/> method.
    /// All values are cloned at the underlying JSON token level, ensuring that the
    /// cloned bag does not share mutable state with this instance.
    /// </remarks>
    object ICloneable.Clone() => Clone();
}
