using System.Reflection;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Gondwana;

/// <summary>Persistence policy shared by native definition documents.</summary>
internal static class DefinitionDocumentPersistence
{
    private static readonly IContractResolver Resolver = new DocumentResolver();

    /// <summary>Serializes a document without redundant physical self-origin metadata.</summary>
    /// <param name="document">The complete document.</param>
    /// <param name="settings">Format-specific serialization settings.</param>
    /// <returns>Canonical JSON. Generated authoring markers are retained.</returns>
    internal static string Serialize(object document, JsonSerializerSettings settings)
    {
        var serializer = JsonSerializer.Create(settings);
        serializer.ContractResolver = Resolver;
        using var writer = new StringWriter();
        serializer.Serialize(writer, document);
        return writer.ToString();
    }

    /// <summary>Writes already prepared JSON atomically at its final destination.</summary>
    /// <param name="path">The final absolute document path used when preparing the JSON.</param>
    /// <param name="json">The prepared document JSON.</param>
    internal static void WriteAtomic(string path, string json)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed class DocumentResolver : DefaultContractResolver
    {
        /// <summary>Omits reconstructed self locations only in document persistence.</summary>
        /// <param name="member">The serialized model member.</param>
        /// <param name="memberSerialization">The model serialization policy.</param>
        /// <returns>The document property contract.</returns>
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            if (member.Name == "Source")
                property.ShouldSerialize = value => value switch
                {
                    TilesheetDefinition definition => definition.Source.Kind == TilesheetDefinitionSourceKind.Generated,
                    AnimationDefinition definition => definition.Source.Kind == AnimationDefinitionSourceKind.Generated,
                    SceneDefinition definition => definition.Source.Kind == SceneDefinitionSourceKind.Generated,
                    _ => true
                };
            return property;
        }
    }
}
