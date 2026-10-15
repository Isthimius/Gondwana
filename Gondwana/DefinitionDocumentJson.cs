using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gondwana;

/// <summary>Shared envelope parsing and member selection for native definition documents.</summary>
internal static class DefinitionDocumentJson
{
    /// <summary>Reads an envelope or legacy singleton, rejecting malformed or mixed envelopes.</summary>
    /// <typeparam name="T">The definition type.</typeparam>
    /// <param name="json">The document JSON.</param>
    /// <param name="read">The format-specific singleton reader.</param>
    /// <param name="identity">The logical identity accessor.</param>
    /// <param name="legacy">Optional legacy normalization.</param>
    /// <returns>All members in document order.</returns>
    internal static List<T> Read<T>(string json, Func<string, T> read, Func<T, string> identity, Action<T>? legacy = null, StringComparer? identityComparer = null) where T : class
    {
        try
        {
            var root = JToken.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }) as JObject
                ?? throw new InvalidDataException("Definition document must be a JSON object.");
            var properties = root.Properties().Where(p => string.Equals(p.Name, "Definitions", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (properties.Length == 0)
            {
                var singleton = read(root.ToString());
                legacy?.Invoke(singleton);
                return [singleton];
            }
            if (properties.Length != 1 || root.Properties().Count() != 1)
                throw new InvalidDataException("Definition document cannot combine Definitions with singleton properties.");
            if (properties[0].Value is not JArray array)
                throw new InvalidDataException("Definitions must be a non-null JSON array.");
            var definitions = new List<T>();
            foreach (var token in array)
            {
                if (token is not JObject member || member.Properties().Any(p => string.Equals(p.Name, "Definitions", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException($"Definitions[{definitions.Count}] must be a non-null definition object.");
                definitions.Add(read(member.ToString()));
            }
            Check(definitions, identity, identityComparer: identityComparer);
            return definitions;
        }
        catch (JsonException ex) { throw new InvalidDataException("Failed to deserialize definition document.", ex); }
    }

    /// <summary>Checks structural integrity and ordinal logical identity uniqueness.</summary>
    /// <typeparam name="T">The definition type.</typeparam>
    /// <param name="definitions">The members.</param>
    /// <param name="identity">The identity accessor.</param>
    /// <param name="allowUnnamedSingleton">Whether historical singleton data may omit its identity.</param>
    internal static void Check<T>(IReadOnlyList<T>? definitions, Func<T, string> identity, bool allowUnnamedSingleton = false, StringComparer? identityComparer = null) where T : class
    {
        if (definitions is null) throw new InvalidDataException("Definitions must not be null.");
        var identities = new HashSet<string>(identityComparer ?? StringComparer.Ordinal);
        for (int i = 0; i < definitions.Count; i++)
        {
            var member = definitions[i] ?? throw new InvalidDataException($"Definitions[{i}] must not be null.");
            var key = identity(member);
            if (string.IsNullOrWhiteSpace(key) && !(allowUnnamedSingleton && definitions.Count == 1))
                throw new InvalidDataException($"Definitions[{i}] requires a non-empty logical identity.");
            if (!identities.Add(key)) throw new InvalidDataException($"Duplicate definition identity '{key}'.");
        }
    }

    /// <summary>Selects a member by identity, or requires a singleton when no selector is supplied.</summary>
    /// <typeparam name="T">The definition type.</typeparam>
    /// <param name="definitions">The members.</param>
    /// <param name="identity">The identity accessor.</param>
    /// <param name="key">The requested identity, or null for singleton compatibility.</param>
    /// <returns>The selected member.</returns>
    internal static T Select<T>(IReadOnlyList<T> definitions, Func<T, string> identity, string? key = null, StringComparer? identityComparer = null)
    {
        if (key is null)
        {
            if (definitions.Count != 1)
                throw new InvalidDataException($"Document contains {definitions.Count} definitions. Specify a logical identity or use LoadAll.");
            return definitions[0];
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        foreach (var member in definitions)
            if ((identityComparer ?? StringComparer.Ordinal).Equals(identity(member), key)) return member;
        throw new InvalidDataException($"Definition '{key}' was not found in the document.");
    }
}
