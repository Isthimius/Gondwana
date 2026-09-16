using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gondwana.Scenes;

// Json.NET cannot restore reference-preserved multidimensional arrays. Accept
// both the historical {$id,$values} envelope and a plain rectangular array.
internal sealed class SceneLayerTileArrayConverter : JsonConverter<SceneLayerTile[,]>
{
    public override SceneLayerTile[,] ReadJson(JsonReader reader, Type objectType, SceneLayerTile[,]? existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);
        var rows = (JArray)(token is JObject obj ? obj["$values"]! : token);
        int height = rows.Count == 0 ? 0 : ((JArray)rows[0]).Count;
        var result = new SceneLayerTile[rows.Count, height];
        if (token is JObject envelope && envelope.Value<string>("$id") is { } id)
            serializer.ReferenceResolver!.AddReference(serializer, id, result);
        for (int x = 0; x < rows.Count; x++)
        {
            if (rows[x] is not JArray column || column.Count != height)
                throw new JsonSerializationException("SceneLayerTileArray must be rectangular.");
            for (int y = 0; y < height; y++)
                result[x, y] = column[y].ToObject<SceneLayerTile>(serializer)!;
        }
        return result;
    }

    public override bool CanWrite => false;
    public override void WriteJson(JsonWriter writer, SceneLayerTile[,]? value, JsonSerializer serializer) =>
        throw new NotSupportedException();
}
